namespace PantheonSDR.Devices;

/// <summary>
/// Abstractie voor elk SDR-device dat IQ-samples kan leveren (RX).
/// Elke hardware-adapter implementeert deze interface.
/// </summary>
public interface IDeviceSource : IAsyncDisposable
{
    /// <summary>Unieke identificatie voor dit device-exemplaar (bijv. serienummer of MAC).</summary>
    string DeviceId { get; }

    /// <summary>Leesbare naam voor weergave in de UI.</summary>
    string FriendlyName { get; }

    /// <summary>Mogelijkheden van dit device.</summary>
    DeviceCapabilities Capabilities { get; }

    /// <summary>Ondersteunde frequentiebereiken.</summary>
    FrequencyRange[] SupportedRanges { get; }

    /// <summary>Ondersteunde sample rates in Hz.</summary>
    int[] SupportedSampleRates { get; }

    /// <summary>
    /// Open het device en maak het klaar voor streaming.
    /// </summary>
    Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct = default);

    /// <summary>
    /// Start IQ-streaming. Blokkeert totdat <paramref name="ct"/> geannuleerd wordt
    /// of het device stopt. Gebruik in een BackgroundService of Task.Run.
    /// </summary>
    IAsyncEnumerable<IqBlock> StreamAsync(CancellationToken ct = default);

    /// <summary>Stel de center-frequentie in (Hz).</summary>
    Task SetFrequencyAsync(long hz, CancellationToken ct = default);

    /// <summary>Stel de gain in (dB). Device klampt naar ondersteund bereik.</summary>
    Task SetGainAsync(double db, CancellationToken ct = default);

    /// <summary>Stel de sample rate in (Hz). Device kiest dichtstbijzijnde ondersteunde waarde.</summary>
    Task SetSampleRateAsync(int hz, CancellationToken ct = default);
}
