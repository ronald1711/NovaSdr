namespace NovaSdr.Devices;

/// <summary>Frequentiebereik dat een device ondersteunt.</summary>
public readonly record struct FrequencyRange(long MinHz, long MaxHz)
{
    public bool Contains(long frequencyHz) => frequencyHz >= MinHz && frequencyHz <= MaxHz;

    public override string ToString() =>
        $"{MinHz / 1_000_000.0:F3} MHz – {MaxHz / 1_000_000.0:F3} MHz";
}
