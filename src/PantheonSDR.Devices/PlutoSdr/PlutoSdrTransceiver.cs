using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PantheonSDR.Devices.PlutoSdr;

/// <summary>
/// ITransceiver adapter voor ADALM-PLUTO / PlutoSDR Plus met F5OEO firmware.
///
/// F5OEO firmware kenmerken:
///  - AD9364 chip ontgrendeld: 70 MHz – 6 GHz (vs standaard 325 MHz – 3.8 GHz)
///  - Hogere bandbreedte: tot 56 MHz
///  - TCXO correctie via xo_correction attribuut
///  - Dual-input antenna select (A/B)
///  - Full duplex TX + RX simultaan
///
/// Verbinding: Ethernet (standaard 192.168.2.1) of USB (uri: "usb:")
/// Vereist: libiio geïnstalleerd (apt install libiio-dev / brew install libiio)
/// </summary>
public sealed class PlutoSdrTransceiver : ITransceiver
{
    // ── PlutoSDR IIO device-namen ─────────────────────────────────────────────
    private const string RxDevName  = "cf-ad9361-lpc";      // RX ADC DMA
    private const string TxDevName  = "cf-ad9361-dds-core-lpc"; // TX DAC DMA
    private const string PhyDevName = "ad9361-phy";         // RF configuratie

    private readonly string _uri;
    private readonly ILogger<PlutoSdrTransceiver> _logger;

    private nint _ctx;
    private nint _rxDev, _txDev, _phyDev;
    private nint _rxCh0I, _rxCh0Q;    // RX IQ kanalen
    private nint _txCh0I, _txCh0Q;    // TX IQ kanalen
    private nint _rxPhyCh, _txPhyCh;  // RF configuratiekanalen

    private nint _rxBuf, _txBuf;
    private nuint _bufferSamples = 1024;

    private long _rxFrequencyHz = 14_200_000;
    private long _txFrequencyHz = 14_200_000;
    private int  _sampleRateHz  = 2_500_000;  // 2.5 MHz — decimeer naar 48 kHz via SampleRateBridge
    private bool _mox;

    private Channel<IqBlock>? _iqChannel;
    private CancellationTokenSource? _streamCts;

    public PlutoSdrTransceiver(string uri, string friendlyName, ILogger<PlutoSdrTransceiver> logger)
    {
        _uri = uri;
        FriendlyName = friendlyName;
        _logger = logger;
    }

    public string DeviceId    => $"pluto:{_uri}";
    public string FriendlyName { get; }

    /// <summary>
    /// F5OEO firmware: 70 MHz – 6 GHz + full duplex + WideFreq.
    /// PureSignal: TX loopback via RX2 input (toekomstig).
    /// </summary>
    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive   |
        DeviceCapabilities.Transmit  |
        DeviceCapabilities.FullDuplex|
        DeviceCapabilities.WideFreq;

    public FrequencyRange[] SupportedRanges =>
        [new FrequencyRange(70_000_000, 6_000_000_000)]; // 70 MHz – 6 GHz (F5OEO)

    public int[] SupportedSampleRates =>
        [520_833, 1_000_000, 2_083_333, 2_500_000, 5_000_000,
         10_000_000, 25_000_000, 50_000_000, 61_440_000];

    // ── Open ──────────────────────────────────────────────────────────────────

    public Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct = default)
    {
        _ctx = _uri.StartsWith("ip:") || !_uri.Contains(':')
            ? IioNative.CreateNetworkContext(_uri.Replace("ip:", ""))
            : IioNative.CreateContextFromUri(_uri);

        if (_ctx == nint.Zero)
        {
            _logger.LogError("PlutoSDR: verbinding met {Uri} mislukt", _uri);
            return Task.FromResult(false);
        }

        _rxDev  = IioNative.ContextFindDevice(_ctx, RxDevName);
        _txDev  = IioNative.ContextFindDevice(_ctx, TxDevName);
        _phyDev = IioNative.ContextFindDevice(_ctx, PhyDevName);

        if (_rxDev == nint.Zero || _txDev == nint.Zero || _phyDev == nint.Zero)
        {
            _logger.LogError("PlutoSDR: ADC/DAC/PHY device niet gevonden. Controleer firmware.");
            IioNative.ContextDestroy(_ctx);
            _ctx = nint.Zero;
            return Task.FromResult(false);
        }

        // RX-kanalen activeren
        _rxCh0I  = IioNative.DeviceFindChannel(_rxDev, "voltage0", false);
        _rxCh0Q  = IioNative.DeviceFindChannel(_rxDev, "voltage1", false);
        _rxPhyCh = IioNative.DeviceFindChannel(_phyDev, "voltage0", false);

        IioNative.ChannelEnable(_rxCh0I);
        IioNative.ChannelEnable(_rxCh0Q);

        // TX-kanalen activeren
        _txCh0I  = IioNative.DeviceFindChannel(_txDev, "voltage0", true);
        _txCh0Q  = IioNative.DeviceFindChannel(_txDev, "voltage1", true);
        _txPhyCh = IioNative.DeviceFindChannel(_phyDev, "voltage0", true);

        IioNative.ChannelEnable(_txCh0I);
        IioNative.ChannelEnable(_txCh0Q);

        // Initiële RF-configuratie
        if (options.InitialFrequencyHz > 0) _rxFrequencyHz = options.InitialFrequencyHz;
        if (options.InitialFrequencyHz > 0) _txFrequencyHz = options.InitialFrequencyHz;
        _sampleRateHz = options.PreferredSampleRateHz > 100_000
            ? options.PreferredSampleRateHz
            : 2_500_000;

        ConfigureRf();

        _logger.LogInformation(
            "PlutoSDR Plus verbonden: {Uri} | {Freq:F3} MHz @ {Rate} kHz",
            _uri, _rxFrequencyHz / 1e6, _sampleRateHz / 1000);

        return Task.FromResult(true);
    }

    // ── RX streaming ──────────────────────────────────────────────────────────

    public async IAsyncEnumerable<IqBlock> StreamAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _iqChannel = Channel.CreateBounded<IqBlock>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

        _rxBuf = IioNative.DeviceCreateBuffer(_rxDev, _bufferSamples, false);
        if (_rxBuf == nint.Zero)
            throw new InvalidOperationException("PlutoSDR: RX buffer aanmaken mislukt");

        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var streamTask = Task.Run(() => RxLoop(_streamCts.Token), _streamCts.Token);

        try
        {
            await foreach (var block in _iqChannel.Reader.ReadAllAsync(ct))
                yield return block;
        }
        finally
        {
            _streamCts.Cancel();
            _iqChannel.Writer.TryComplete();
            await streamTask.ConfigureAwait(false);
            if (_rxBuf != nint.Zero) { IioNative.BufferDestroy(_rxBuf); _rxBuf = nint.Zero; }
        }
    }

    private unsafe void RxLoop(CancellationToken ct)
    {
        _logger.LogInformation("PlutoSDR RX loop gestart @ {Rate} kHz", _sampleRateHz / 1000);
        while (!ct.IsCancellationRequested)
        {
            var filled = IioNative.BufferRefill(_rxBuf);
            if (filled < 0) { _logger.LogWarning("PlutoSDR BufferRefill fout: {Err}", filled); break; }

            var first = IioNative.BufferFirst(_rxBuf, _rxCh0I);
            var end   = IioNative.BufferEnd(_rxBuf);
            var step  = (int)IioNative.BufferStep(_rxBuf);

            // PlutoSDR levert interleaved int16: [I0, Q0, I1, Q1, ...]
            var sampleCount = (int)((end.ToInt64() - first.ToInt64()) / step);
            var interleaved = new double[sampleCount * 2];
            const double scale = 1.0 / 32768.0;

            var ptr = (short*)first;
            for (int i = 0; i < sampleCount; i++)
            {
                interleaved[i * 2]     = ptr[i * 2]     * scale;  // I
                interleaved[i * 2 + 1] = ptr[i * 2 + 1] * scale;  // Q
            }

            var block = new IqBlock(interleaved, _sampleRateHz, _rxFrequencyHz, DateTimeOffset.UtcNow);
            _iqChannel?.Writer.TryWrite(block);
        }
        _logger.LogInformation("PlutoSDR RX loop gestopt");
    }

    // ── TX / Transceiver ──────────────────────────────────────────────────────

    public Task SetMoxAsync(bool keyed, CancellationToken ct = default)
    {
        _mox = keyed;
        // PlutoSDR: TX is altijd actief wanneer samples worden gestuurd.
        // Bij _mox=false stuur je nullen (silence) via TX buffer.
        _logger.LogInformation("PlutoSDR MOX: {State}", keyed ? "TX" : "RX");
        return Task.CompletedTask;
    }

    public Task SetTxFrequencyAsync(long hz, CancellationToken ct = default)
    {
        _txFrequencyHz = hz;
        if (_txPhyCh != nint.Zero)
            IioNative.ChannelAttrWriteLonglong(_txPhyCh, "RF_port_select_available", hz);
        // Volledige implementatie: schrijf naar "lo_freq" op TX phy channel
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<TxFeedbackBlock> TxFeedbackAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // PlutoSDR: TX feedback via tweede RX ingang (toekomstige PS implementatie)
        // Momenteel niet ondersteund
        await Task.CompletedTask;
        yield break;
    }

    // ── Frequentie / Gain / Rate ──────────────────────────────────────────────

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default)
    {
        _rxFrequencyHz = hz;
        if (_rxPhyCh != nint.Zero)
            IioNative.ChannelAttrWriteLonglong(_rxPhyCh, "rf_port_select_available", hz);
        // Volledig: schrijf naar "lo_freq" op RX phy channel
        ApplyRxLo(hz);
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double db, CancellationToken ct = default)
    {
        if (_rxPhyCh != nint.Zero)
        {
            // AGC-mode: "manual" voor handmatige gain, "slow_attack" voor auto
            IioNative.ChannelAttrWrite(_rxPhyCh, "gain_control_mode", "manual");
            IioNative.ChannelAttrWriteLonglong(_rxPhyCh, "hardwaregain", (long)db);
        }
        return Task.CompletedTask;
    }

    public Task SetSampleRateAsync(int hz, CancellationToken ct = default)
    {
        _sampleRateHz = hz;
        if (_phyDev != nint.Zero)
            IioNative.DeviceAttrWriteLonglong(_phyDev, "sampling_frequency", hz);
        return Task.CompletedTask;
    }

    // ── Private RF configuratie ───────────────────────────────────────────────

    private void ConfigureRf()
    {
        if (_phyDev == nint.Zero) return;

        // Sample rate
        IioNative.DeviceAttrWriteLonglong(_phyDev, "sampling_frequency", _sampleRateHz);

        // RX LO frequentie (F5OEO: 70 MHz – 6 GHz)
        ApplyRxLo(_rxFrequencyHz);

        // TX LO frequentie
        if (_txPhyCh != nint.Zero)
            IioNative.ChannelAttrWriteLonglong(_txPhyCh, "rf_port_select_available", _txFrequencyHz);

        // RF bandbreedte (iets smaller dan sample rate)
        var bw = Math.Min(_sampleRateHz * 9 / 10, 56_000_000);
        if (_rxPhyCh != nint.Zero)
            IioNative.ChannelAttrWriteLonglong(_rxPhyCh, "rf_bandwidth", bw);
        if (_txPhyCh != nint.Zero)
            IioNative.ChannelAttrWriteLonglong(_txPhyCh, "rf_bandwidth", bw);

        // AGC: slow_attack als default (stabielste voor HF SSB)
        if (_rxPhyCh != nint.Zero)
            IioNative.ChannelAttrWrite(_rxPhyCh, "gain_control_mode", "slow_attack");
    }

    private void ApplyRxLo(long hz)
    {
        // IIO attribuut voor RX LO via phy device
        if (_phyDev != nint.Zero)
            IioNative.DeviceAttrWriteLonglong(_phyDev, "out_altvoltage0_RX_LO_frequency", hz);
    }

    public ValueTask DisposeAsync()
    {
        _streamCts?.Cancel();
        _iqChannel?.Writer.TryComplete();

        if (_rxBuf != nint.Zero) { IioNative.BufferDestroy(_rxBuf); _rxBuf = nint.Zero; }
        if (_txBuf != nint.Zero) { IioNative.BufferDestroy(_txBuf); _txBuf = nint.Zero; }
        if (_ctx   != nint.Zero) { IioNative.ContextDestroy(_ctx);  _ctx   = nint.Zero; }

        _streamCts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
