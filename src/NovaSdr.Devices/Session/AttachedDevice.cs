using NovaSdr.Devices.Resampling;

namespace NovaSdr.Devices.Session;

/// <summary>
/// Eén device dat aan een <see cref="RadioSession"/> is gekoppeld,
/// inclusief zijn rol, configuratie en toegewezen WDSP-kanaal.
///
/// Het model is rolvrijheid: elk <see cref="IDeviceSource"/> kan
/// elke rol krijgen ongeacht het merk of protocol.
/// </summary>
public sealed class AttachedDevice : IAsyncDisposable
{
    private readonly WdspChannelAllocator _allocator;
    private SampleRateBridge? _bridge;

    internal AttachedDevice(
        IDeviceSource device,
        DeviceRole role,
        WdspChannelAllocator allocator,
        int wdspChannelId)
    {
        Device         = device;
        Role           = role;
        WdspChannelId  = wdspChannelId;
        _allocator     = allocator;

        // Bouw sample-rate bridge als het device niet op 48 kHz levert
        var nativeRate = device.SupportedSampleRates.FirstOrDefault(48_000);
        if (nativeRate != 48_000)
            _bridge = new SampleRateBridge(nativeRate);
    }

    // ── Identiteit ────────────────────────────────────────────────────────────

    /// <summary>Achterliggend hardware-device.</summary>
    public IDeviceSource Device { get; }

    /// <summary>Rol in de huidige sessie (Primary of Auxiliary).</summary>
    public DeviceRole Role { get; }

    /// <summary>Toegewezen WDSP channel ID (0-13 primary, 16-29 aux).</summary>
    public int WdspChannelId { get; }

    // ── Configuratie (runtime wijzigbaar) ─────────────────────────────────────

    /// <summary>Huidige VFO-frequentie in Hz.</summary>
    public long FrequencyHz { get; set; }

    /// <summary>Frequentiesync-beleid t.o.v. primary device.</summary>
    public FreqSyncPolicy FreqSync { get; set; } = FreqSyncPolicy.Independent;

    /// <summary>Vaste frequentie-offset bij FollowPrimaryWithOffset (Hz).</summary>
    public long FreqSyncOffsetHz { get; set; }

    /// <summary>Audio-routing naar de output.</summary>
    public AudioRoute AudioRoute { get; set; } = AudioRoute.Left;

    /// <summary>
    /// Staat simultane TX toe naast de primary (normaliter false).
    /// Alleen zinvol als dit device ook <see cref="ITransceiver"/> is
    /// én de operator bewust twee stations wil runnen.
    /// </summary>
    public bool AllowSimultaneousTx { get; set; }

    /// <summary>Is dit device actief en verwerkt het IQ-data?</summary>
    public bool IsEnabled { get; set; } = true;

    // ── DSP helper ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decimeert een IQ-blok van native device-rate naar 48 kHz voor WDSP.
    /// Retourneert het origineel als geen decimatie nodig is.
    /// </summary>
    public IqBlock? Decimate(IqBlock block) =>
        _bridge is null ? block : _bridge.Process(block);

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _allocator.ReleaseChannel(WdspChannelId);
        _bridge?.Dispose();
        await Device.DisposeAsync();
    }
}
