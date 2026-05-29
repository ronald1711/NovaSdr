namespace PantheonSDR.Devices.Session;

/// <summary>
/// Bepaalt hoe de VFO-frequentie van een auxiliary device wordt gesynchroniseerd
/// met het primary device.
/// </summary>
public enum FreqSyncPolicy
{
    /// <summary>
    /// Geheel onafhankelijke VFO. Operator stelt handmatig in.
    /// Typisch: aux device monitort een andere band.
    /// </summary>
    Independent,

    /// <summary>
    /// Aux VFO volgt primary VFO-A exact.
    /// Handig voor side-by-side monitoring op dezelfde frequentie.
    /// NB: geen phase-coherentie — devices hebben onafhankelijke LO's.
    /// </summary>
    FollowPrimary,

    /// <summary>
    /// Aux VFO = primary VFO-A + vaste offset.
    /// Gebruik: satelliet (uplink/downlink split), transverter IF/RF.
    /// </summary>
    FollowPrimaryWithOffset,
}
