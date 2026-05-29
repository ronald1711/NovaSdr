namespace PantheonSDR.Devices;

/// <summary>
/// Loopback IQ-feedback van de TX-keten, gebruikt voor PureSignal predistortion.
/// </summary>
public sealed class TxFeedbackBlock
{
    public TxFeedbackBlock(double[] samples, int sampleRateHz, DateTimeOffset timestamp)
    {
        Samples = samples;
        SampleRateHz = sampleRateHz;
        Timestamp = timestamp;
    }

    public double[] Samples { get; }
    public int SampleRateHz { get; }
    public DateTimeOffset Timestamp { get; }
}
