namespace NovaSdr.Devices;

/// <summary>Opties bij het openen van een device.</summary>
public sealed class DeviceOpenOptions
{
    /// <summary>Gewenste sample rate in Hz. Device kiest dichtstbijzijnde ondersteunde waarde.</summary>
    public int PreferredSampleRateHz { get; init; } = 48_000;

    /// <summary>Start-frequentie in Hz.</summary>
    public long InitialFrequencyHz { get; init; }

    /// <summary>Start-gain in dB.</summary>
    public double InitialGainDb { get; init; }
}
