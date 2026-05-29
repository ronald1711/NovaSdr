namespace PantheonSDR.Devices.Session;

/// <summary>
/// Rol van een device binnen een RadioSession.
/// Elk device kan elke rol hebben — de rol bepaalt gedrag, niet het device-type.
/// </summary>
public enum DeviceRole
{
    /// <summary>
    /// Hoofd-transceiver: levert het primaire RX-kanaal en mag TX.
    /// Precies één device per sessie heeft deze rol.
    /// </summary>
    Primary,

    /// <summary>
    /// Secundaire ontvanger: extra RX-kanaal naast de primary.
    /// Meerdere devices kunnen deze rol hebben.
    /// TX is geblokkeerd tijdens Primary MOX (PTT lockout),
    /// tenzij <see cref="AttachedDevice.AllowSimultaneousTx"/> = true.
    /// </summary>
    Auxiliary,
}
