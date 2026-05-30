// OpenHPSDR Protocol 2 device adapter.
// The actual P2 connection is managed by Zeus.Server.Hosting via
// Protocol2Client + DspPipelineService. This class represents the device
// in the RadioSession model (role, WDSP channel, freq sync, PTT lockout).

namespace PantheonSDR.Devices.OpenHpsdr;

/// <summary>
/// ITransceiver representation of an OpenHPSDR Protocol 2 radio
/// (Brick2, ANAN G2/G2 MkII, Saturn G2).
///
/// IQ streaming is handled by the Zeus DSP pipeline internally; this adapter
/// provides RadioSession integration without duplicating the protocol layer.
/// </summary>
public sealed class OpenHpsdrP2Transceiver : ITransceiver
{
    private long _rxFrequencyHz;
    private long _txFrequencyHz;
    private bool _mox;

    public OpenHpsdrP2Transceiver(string deviceId, string friendlyName,
        long initialFrequencyHz = 14_200_000)
    {
        DeviceId = deviceId;
        FriendlyName = friendlyName;
        _rxFrequencyHz = initialFrequencyHz;
        _txFrequencyHz = initialFrequencyHz;
    }

    public string DeviceId { get; }
    public string FriendlyName { get; }

    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive    |
        DeviceCapabilities.Transmit   |
        DeviceCapabilities.FullDuplex |
        DeviceCapabilities.DualRx     |
        DeviceCapabilities.PureSignal |
        DeviceCapabilities.VariableRate;

    public FrequencyRange[] SupportedRanges =>
        [new FrequencyRange(0, 61_440_000)];

    public int[] SupportedSampleRates =>
        [48_000, 96_000, 192_000, 384_000];

    public Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct = default)
    {
        _rxFrequencyHz = options.InitialFrequencyHz > 0 ? options.InitialFrequencyHz : 14_200_000;
        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<IqBlock> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break; // IQ flows through Zeus DspPipelineService on WDSP channel 0
    }

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default)
    {
        _rxFrequencyHz = hz;
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double db, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetSampleRateAsync(int hz, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetMoxAsync(bool keyed, CancellationToken ct = default)
    {
        _mox = keyed;
        return Task.CompletedTask;
    }

    public Task SetTxFrequencyAsync(long hz, CancellationToken ct = default)
    {
        _txFrequencyHz = hz;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<TxFeedbackBlock> TxFeedbackAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break; // PS feedback handled by Zeus PsAutoAttenuateService
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
