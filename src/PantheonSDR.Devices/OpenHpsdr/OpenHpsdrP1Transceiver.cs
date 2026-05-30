// OpenHPSDR Protocol 1 device adapter.
// The actual P1 connection is managed by Zeus.Server.Hosting.RadioService.
// This class represents the device in the RadioSession model and
// delegates control commands back through the RadioService.

namespace PantheonSDR.Devices.OpenHpsdr;

/// <summary>
/// ITransceiver representation of an OpenHPSDR Protocol 1 radio
/// (Hermes-Lite 2, ANAN-10/100, Brick2 in P1 mode).
///
/// IQ streaming is handled by the Zeus DSP pipeline internally.
/// This adapter provides RadioSession integration (role, WDSP channel,
/// freq sync, PTT lockout) without duplicating the protocol layer.
/// </summary>
public sealed class OpenHpsdrP1Transceiver : ITransceiver
{
    private long _rxFrequencyHz;
    private long _txFrequencyHz;
    private bool _mox;

    public OpenHpsdrP1Transceiver(string deviceId, string friendlyName,
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
        DeviceCapabilities.Receive   |
        DeviceCapabilities.Transmit  |
        DeviceCapabilities.FullDuplex|
        DeviceCapabilities.DualRx;

    public FrequencyRange[] SupportedRanges =>
        [new FrequencyRange(0, 61_440_000)]; // 0–61.44 MHz

    public int[] SupportedSampleRates =>
        [48_000, 96_000, 192_000, 384_000];

    public Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct = default)
    {
        _rxFrequencyHz = options.InitialFrequencyHz > 0 ? options.InitialFrequencyHz : 14_200_000;
        return Task.FromResult(true);
    }

    // IQ streaming is done internally by Zeus DspPipelineService via IRxPacketSink.
    // This enumerable is intentionally empty — the IQ data flows through WDSP
    // channel 0 (primary) without going through IDeviceSource.StreamAsync().
    public async IAsyncEnumerable<IqBlock> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default)
    {
        _rxFrequencyHz = hz;
        // Actual frequency command is sent via RadioService → Protocol1Client
        // in the existing Zeus pipeline. SessionEndpoints calls RadioService
        // for primary OpenHPSDR device frequency changes.
        return Task.CompletedTask;
    }

    public Task SetGainAsync(double db, CancellationToken ct = default) =>
        Task.CompletedTask; // Controlled via AGC/attenuator registers in RadioService

    public Task SetSampleRateAsync(int hz, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SetMoxAsync(bool keyed, CancellationToken ct = default)
    {
        _mox = keyed;
        // PTT is sent via RadioService → Protocol1Client internally
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
        yield break; // P1 has no hardware PS loopback
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
