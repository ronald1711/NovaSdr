namespace NovaSdr.Devices;

/// <summary>
/// Uitbreiding op <see cref="IDeviceSource"/> voor devices die ook kunnen zenden (TX).
/// Brick2, ANAN G2, Hermes-Lite 2 en PlutoSDR implementeren deze interface.
/// </summary>
public interface ITransceiver : IDeviceSource
{
    /// <summary>Activeer of deactiveer TX (MOX).</summary>
    Task SetMoxAsync(bool keyed, CancellationToken ct = default);

    /// <summary>Stel de TX-frequentie in (Hz). Kan afwijken van RX voor split-operatie.</summary>
    Task SetTxFrequencyAsync(long hz, CancellationToken ct = default);

    /// <summary>
    /// Stream TX-loopback IQ voor PureSignal predistortion.
    /// Alleen beschikbaar als <see cref="DeviceCapabilities.PureSignal"/> aanwezig is.
    /// </summary>
    IAsyncEnumerable<TxFeedbackBlock> TxFeedbackAsync(CancellationToken ct = default);
}
