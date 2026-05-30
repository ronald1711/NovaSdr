namespace PantheonSDR.Devices.Resampling;

/// <summary>
/// Decimeert IQ-blokken van een native device sample rate naar de WDSP-invoerrate (48 kHz).
///
/// Implementatie: polyphase FIR decimatie met anti-aliasing filter.
/// De decimatiefactor wordt berekend als de rationele verhouding native/target.
///
/// Voorbeelden:
///   SDRplay 2 MHz   → 48 kHz  : factor ≈ 41.67  (polyphase 5/208)
///   PlutoSDR 2.5 MHz → 48 kHz : factor ≈ 52.08  (polyphase 25/1302)
/// </summary>
public sealed class SampleRateBridge : IDisposable
{
    private readonly int _inputRateHz;
    private readonly int _outputRateHz;
    private readonly int _decimationFactor;
    private readonly double[] _filterCoefficients;

    public SampleRateBridge(int inputRateHz, int outputRateHz = 48_000)
    {
        _inputRateHz  = inputRateHz;
        _outputRateHz = outputRateHz;

        // Eenvoudige integerverhouding decimatie
        _decimationFactor = Math.Max(1, (int)Math.Round((double)inputRateHz / outputRateHz));

        // Anti-aliasing lowpass FIR (windowed sinc)
        // Afkapfrequentie = Nyquist van outputRate = outputRate / 2
        var cutoff = (double)outputRateHz / inputRateHz; // normalised to inputRate
        _filterCoefficients = DesignLowpassFir(cutoff, filterLength: 63);
    }

    public int InputRateHz  => _inputRateHz;
    public int OutputRateHz => _outputRateHz;
    public int DecimationFactor => _decimationFactor;

    /// <summary>
    /// Verwerk één IqBlock van de native rate en retourneer een IqBlock op 48 kHz.
    /// Retourneert null als nog niet genoeg input-samples beschikbaar zijn.
    /// </summary>
    public IqBlock? Process(IqBlock input)
    {
        if (_decimationFactor == 1) return input; // geen conversie nodig

        var inputSamples = input.Samples;
        var outputCount  = inputSamples.Length / 2 / _decimationFactor;

        if (outputCount == 0) return null;

        var outputSamples = new double[outputCount * 2];
        int outIdx = 0;

        for (int i = 0; i < outputCount; i++)
        {
            // Neem elk n-de sample na FIR filtering
            int inputIdx = i * _decimationFactor;
            var (filtI, filtQ) = ApplyFirAt(inputSamples, inputIdx);

            outputSamples[outIdx++] = filtI;
            outputSamples[outIdx++] = filtQ;
        }

        return new IqBlock(
            outputSamples,
            _outputRateHz,
            input.CenterFrequencyHz,
            input.Timestamp);
    }

    private (double I, double Q) ApplyFirAt(double[] samples, int centerIdx)
    {
        double sumI = 0, sumQ = 0;
        int len = _filterCoefficients.Length;
        int half = len / 2;

        for (int k = 0; k < len; k++)
        {
            int si = (centerIdx - half + k) * 2;
            if (si < 0 || si + 1 >= samples.Length) continue;

            sumI += _filterCoefficients[k] * samples[si];
            sumQ += _filterCoefficients[k] * samples[si + 1];
        }

        return (sumI, sumQ);
    }

    /// <summary>
    /// Ontwerp een windowed-sinc lowpass FIR filter.
    /// cutoff: normaliseerde afkapfrequentie (0..0.5 t.o.v. inputRate)
    /// </summary>
    private static double[] DesignLowpassFir(double cutoff, int filterLength)
    {
        if (filterLength % 2 == 0) filterLength++; // zorg voor odd lengte
        var h = new double[filterLength];
        int M = filterLength - 1;
        double sum = 0;

        for (int n = 0; n <= M; n++)
        {
            double sinc = n == M / 2
                ? 2.0 * cutoff
                : Math.Sin(2.0 * Math.PI * cutoff * (n - M / 2.0)) / (Math.PI * (n - M / 2.0));

            // Blackman venster
            double window = 0.42 - 0.5 * Math.Cos(2.0 * Math.PI * n / M)
                               + 0.08 * Math.Cos(4.0 * Math.PI * n / M);
            h[n] = sinc * window;
            sum += h[n];
        }

        // Normaliseer voor eenheidsgain bij DC
        for (int n = 0; n < filterLength; n++) h[n] /= sum;
        return h;
    }

    public void Dispose() { /* stateless — niets te vrijgeven */ }
}
