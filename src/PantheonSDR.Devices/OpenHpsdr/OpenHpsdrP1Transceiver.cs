using Zeus.Protocol1;
using Zeus.Contracts;

namespace PantheonSDR.Devices.OpenHpsdr;

/// <summary>
/// ITransceiver adapter voor OpenHPSDR Protocol 1 radios
/// (Hermes-Lite 2, ANAN-100, Brick2 in P1 mode).
/// Wraps Zeus.Protocol1.Protocol1Client.
/// </summary>
public sealed class OpenHpsdrP1Transceiver : ITransceiver
{
    private readonly Protocol1Client _client;

    public OpenHpsdrP1Transceiver(Protocol1Client client, string deviceId, string friendlyName)
    {
        _client = client;
        DeviceId = deviceId;
        FriendlyName = friendlyName;
    }

    public string DeviceId { get; }
    public string FriendlyName { get; }

    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive |
        DeviceCapabilities.Transmit |
        DeviceCapabilities.FullDuplex |
        DeviceCapabilities.DualRx;

    public FrequencyRange[] SupportedRanges =>
        [new FrequencyRange(0, 61_440_000)];

    public int[] SupportedSampleRates =>
        [48_000, 96_000, 192_000, 384_000];

    public Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct = default) =>
        Task.FromResult(true);

    public async IAsyncEnumerable<IqBlock> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var frame in _client.StreamIqAsync(ct))
        {
            yield return new IqBlock(
                samples: frame.InterleavedIq,
                sampleRateHz: 48_000,
                centerFrequencyHz: frame.CenterFrequencyHz,
                timestamp: DateTimeOffset.UtcNow);
        }
    }

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default) =>
        _client.SetRxFrequencyAsync(hz, ct);

    public Task SetGainAsync(double db, CancellationToken ct = default) =>
        Task.CompletedTask; // P1 gain via AGC/attenuator registers

    public Task SetSampleRateAsync(int hz, CancellationToken ct = default) =>
        _client.SetSampleRateAsync(hz, ct);

    public Task SetMoxAsync(bool keyed, CancellationToken ct = default) =>
        _client.SetMoxAsync(keyed, ct);

    public Task SetTxFrequencyAsync(long hz, CancellationToken ct = default) =>
        _client.SetTxFrequencyAsync(hz, ct);

    public async IAsyncEnumerable<TxFeedbackBlock> TxFeedbackAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // P1 heeft geen hardware PS loopback; leeg.
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
