namespace PantheonSDR.Devices.Session;

/// <summary>Audio routing van een device naar de audio output.</summary>
public enum AudioRoute
{
    /// <summary>Links kanaal van stereo output.</summary>
    Left,

    /// <summary>Rechts kanaal van stereo output.</summary>
    Right,

    /// <summary>Mono mix met alle andere actieve devices.</summary>
    MonoMix,

    /// <summary>Geen audio output (spectrum display only).</summary>
    Mute,
}
