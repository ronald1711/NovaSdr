namespace PantheonSDR.Devices;

/// <summary>
/// Eén blok interleaved IQ-samples van een device.
/// I en Q worden afwisselend opgeslagen: [I0, Q0, I1, Q1, ...].
/// </summary>
public sealed class IqBlock
{
    public IqBlock(double[] samples, int sampleRateHz, long centerFrequencyHz, DateTimeOffset timestamp)
    {
        Samples = samples;
        SampleRateHz = sampleRateHz;
        CenterFrequencyHz = centerFrequencyHz;
        Timestamp = timestamp;
    }

    /// <summary>Interleaved IQ doubles [I0,Q0,I1,Q1,...].</summary>
    public double[] Samples { get; }

    /// <summary>Sample rate van dit blok in Hz.</summary>
    public int SampleRateHz { get; }

    /// <summary>Center frequentie bij opname in Hz.</summary>
    public long CenterFrequencyHz { get; }

    /// <summary>Tijdstip van het eerste sample.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Aantal IQ-paren (helft van Samples.Length).</summary>
    public int SampleCount => Samples.Length / 2;
}
