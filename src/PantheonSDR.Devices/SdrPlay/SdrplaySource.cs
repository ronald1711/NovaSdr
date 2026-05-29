using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PantheonSDR.Devices.SdrPlay;

/// <summary>
/// IDeviceSource adapter voor SDRplay RSP1A (en andere RSP-radios).
/// Vereist SDRplay API 3.x geïnstalleerd door de gebruiker.
/// https://www.sdrplay.com/api/
/// </summary>
public sealed class SdrplaySource : IDeviceSource
{
    private readonly SdrplayDeviceT _device;
    private readonly ILogger<SdrplaySource> _logger;

    // Native callback delegates — bewaar referenties om GC te voorkomen
    private SdrplayStreamCallback? _streamCb;
    private SdrplayEventCallback? _eventCb;
    private GCHandle _callbacksHandle;

    private Channel<IqBlock>? _iqChannel;
    private long _centerFrequencyHz;
    private int _sampleRateHz = 2_000_000; // 2 MHz default

    // Delegate types voor native callbacks
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SdrplayStreamCallback(
        nint xi, nint xq,
        nint parameters,
        uint numSamples,
        uint reset,
        nint cbContext);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SdrplayEventCallback(
        int eventId,
        SdrplayTunerSelect tuner,
        nint parameters,
        nint cbContext);

    internal SdrplaySource(SdrplayDeviceT device, ILogger<SdrplaySource> logger)
    {
        _device = device;
        _logger = logger;
    }

    public string DeviceId => $"sdrplay:{_device.SerNo}";
    public string FriendlyName => $"SDRplay RSP (HW {_device.HwVer}) — {_device.SerNo}";

    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive |
        DeviceCapabilities.HwAGC |
        DeviceCapabilities.HardwareAtt;

    public FrequencyRange[] SupportedRanges =>
        [new FrequencyRange(1_000, 2_000_000_000)]; // 1 kHz – 2 GHz

    public int[] SupportedSampleRates =>
        [250_000, 500_000, 1_000_000, 2_000_000, 6_000_000, 8_000_000, 10_000_000];

    public Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct = default)
    {
        _centerFrequencyHz = options.InitialFrequencyHz > 0 ? options.InitialFrequencyHz : 14_200_000;
        _sampleRateHz = options.PreferredSampleRateHz > 0
            ? ClosestSupportedRate(options.PreferredSampleRateHz)
            : 2_000_000;

        _logger.LogInformation(
            "SDRplay {Serial} geopend: {Freq:F3} MHz @ {Rate} kHz",
            _device.SerNo, _centerFrequencyHz / 1e6, _sampleRateHz / 1000);

        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<IqBlock> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _iqChannel = Channel.CreateBounded<IqBlock>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

        // Initialiseer native callbacks
        _streamCb = OnSamples;
        _eventCb = OnEvent;

        var cbFns = new SdrplayCallbackFns
        {
            StreamACbFn = Marshal.GetFunctionPointerForDelegate(_streamCb),
            StreamBCbFn = nint.Zero,
            EventCbFn = Marshal.GetFunctionPointerForDelegate(_eventCb),
        };

        var err = SdrplayNative.Init(_device.Dev, ref cbFns, nint.Zero);
        if (err != SdrplayError.Success)
            throw new InvalidOperationException($"SDRplay Init mislukt: {err}");

        _logger.LogInformation("SDRplay streaming gestart");

        try
        {
            await foreach (var block in _iqChannel.Reader.ReadAllAsync(ct))
                yield return block;
        }
        finally
        {
            SdrplayNative.Uninit(_device.Dev);
            _logger.LogInformation("SDRplay streaming gestopt");
        }
    }

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default)
    {
        _centerFrequencyHz = hz;
        if (_device.Dev != nint.Zero)
        {
            // Schrijf nieuwe frequentie naar RF Params via GetDeviceParams + Update
            // (vereenvoudigd — volledige implementatie via unsafe pointer naar DeviceParams struct)
            SdrplayNative.Update(_device.Dev,
                SdrplayTunerSelect.A,
                SdrplayReasonForUpdate.Tuner_Frf,
                SdrplayReasonForUpdateExt1.None);
        }
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double db, CancellationToken ct = default)
    {
        if (_device.Dev != nint.Zero)
        {
            SdrplayNative.Update(_device.Dev,
                SdrplayTunerSelect.A,
                SdrplayReasonForUpdate.Tuner_Gr,
                SdrplayReasonForUpdateExt1.None);
        }
        return Task.CompletedTask;
    }

    public Task SetSampleRateAsync(int hz, CancellationToken ct = default)
    {
        _sampleRateHz = ClosestSupportedRate(hz);
        if (_device.Dev != nint.Zero)
        {
            SdrplayNative.Update(_device.Dev,
                SdrplayTunerSelect.A,
                SdrplayReasonForUpdate.Dev_Fs,
                SdrplayReasonForUpdateExt1.None);
        }
        return Task.CompletedTask;
    }

    private unsafe void OnSamples(nint xi, nint xq, nint parameters,
        uint numSamples, uint reset, nint cbContext)
    {
        if (_iqChannel is null || numSamples == 0) return;

        // SDRplay levert short[] xi en short[] xq afzonderlijk — interleave naar double[]
        var iPtr = (short*)xi;
        var qPtr = (short*)xq;
        var interleaved = new double[numSamples * 2];

        const double scale = 1.0 / 32768.0;
        for (int i = 0; i < (int)numSamples; i++)
        {
            interleaved[i * 2]     = iPtr[i] * scale;
            interleaved[i * 2 + 1] = qPtr[i] * scale;
        }

        var block = new IqBlock(interleaved, _sampleRateHz, _centerFrequencyHz, DateTimeOffset.UtcNow);
        _iqChannel.Writer.TryWrite(block);
    }

    private void OnEvent(int eventId, SdrplayTunerSelect tuner, nint parameters, nint cbContext)
    {
        _logger.LogDebug("SDRplay event: {EventId}", eventId);
    }

    private int ClosestSupportedRate(int requested) =>
        SupportedSampleRates.MinBy(r => Math.Abs(r - requested));

    public ValueTask DisposeAsync()
    {
        _iqChannel?.Writer.TryComplete();
        if (_device.Dev != nint.Zero)
        {
            var dev = _device;
            SdrplayNative.ReleaseDevice(ref dev);
        }
        if (_callbacksHandle.IsAllocated) _callbacksHandle.Free();
        return ValueTask.CompletedTask;
    }
}
