using Zeus.Protocol2;
using Zeus.Contracts;

namespace PantheonSDR.Devices.OpenHpsdr;

/// <summary>
/// ITransceiver adapter voor OpenHPSDR Protocol 2 radios
/// (Brick2, ANAN G2/G2 MkII, Saturn G2).
/// Wraps Zeus.Protocol2.Protocol2Client.
/// </summary>
public sealed class OpenHpsdrP2Transceiver : ITransceiver
{
    private readonly Protocol2Client _client;
    private readonly HpsdrBoardKind _boardKind;

    public OpenHpsdrP2Transceiver(Protocol2Client client, HpsdrBoardKind boardKind, string deviceId, string friendlyName)
    {
        _client = client;
        _boardKind = boardKind;
        DeviceId = deviceId;
        FriendlyName = friendlyName;
    }

    public string DeviceId { get; }
    public string FriendlyName { get; }

    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive |
        DeviceCapabilities.Transmit |
        DeviceCapabilities.FullDuplex |
        DeviceCapabilities.DualRx |
        DeviceCapabilities.PureSignal |
        DeviceCapabilities.VariableRate;

    public FrequencyRange[] SupportedRanges =>
        [new FrequencyRange(0, 61_440_000)]; // 0–61.44 MHz

    public int[] SupportedSampleRates =>
        [48_000, 96_000, 192_000, 384_000];

    public Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct = default)
    {
        // Protocol2Client is al verbonden bij discovery; hier stellen we
        // de initiële frequentie en sample rate in.
        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<IqBlock> StreamAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Delegeer naar Protocol2Client IQ stream
        await foreach (var frame in _client.StreamIqAsync(ct))
        {
            yield return new IqBlock(
                samples: frame.InterleavedIq,
                sampleRateHz: frame.SampleRateHz,
                centerFrequencyHz: frame.CenterFrequencyHz,
                timestamp: DateTimeOffset.UtcNow);
        }
    }

    public Task SetFrequencyAsync(long hz, CancellationToken ct = default) =>
        _client.SetRxFrequencyAsync(0, hz, ct);

    public Task SetGainAsync(double db, CancellationToken ct = default) =>
        _client.SetAttenuatorAsync((int)-db, ct);

    public Task SetSampleRateAsync(int hz, CancellationToken ct = default) =>
        _client.SetSampleRateAsync(hz, ct);

    public Task SetMoxAsync(bool keyed, CancellationToken ct = default) =>
        _client.SetMoxAsync(keyed, ct);

    public Task SetTxFrequencyAsync(long hz, CancellationToken ct = default) =>
        _client.SetTxFrequencyAsync(hz, ct);

    public async IAsyncEnumerable<TxFeedbackBlock> TxFeedbackAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var frame in _client.StreamPsFeedbackAsync(ct))
        {
            yield return new TxFeedbackBlock(
                samples: frame.InterleavedIq,
                sampleRateHz: frame.SampleRateHz,
                timestamp: DateTimeOffset.UtcNow);
        }
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
