using Microsoft.Extensions.Logging;

namespace PantheonSDR.Devices.Session;

/// <summary>
/// Vertegenwoordigt een actieve radio-sessie: een combinatie van N devices
/// waarvan precies één de Primary-rol heeft (TX + RX) en de rest Auxiliary (RX).
///
/// Symmetrisch model: elk <see cref="IDeviceSource"/> kan elke rol krijgen.
/// Voorbeelden van geldige configuraties:
///   • Brick2 P2 (primary) + SDRplay RSP1A (aux)
///   • Brick2 P2 (primary) + PlutoSDR Plus (aux TX/RX)
///   • Brick2 P2 (primary) + HL2 P1 (aux)   ← twee OpenHPSDR devices
///   • PlutoSDR Plus (primary) + SDRplay RSP1A (aux)
///   • Brick2 P2 (primary) + SDRplay RSP1A (aux1) + PlutoSDR (aux2)
/// </summary>
public sealed class RadioSession : IAsyncDisposable
{
    private readonly List<AttachedDevice> _devices = [];
    private readonly WdspChannelAllocator _allocator;
    private readonly ILogger<RadioSession> _logger;
    private readonly Lock _lock = new();

    public RadioSession(WdspChannelAllocator allocator, ILogger<RadioSession> logger)
    {
        _allocator = allocator;
        _logger    = logger;
    }

    // ── Devices ───────────────────────────────────────────────────────────────

    /// <summary>Snapshot van alle gekoppelde devices.</summary>
    public IReadOnlyList<AttachedDevice> Devices
    {
        get { lock (_lock) { return [.._devices]; } }
    }

    /// <summary>Het primaire device (TX + RX). Null als sessie leeg is.</summary>
    public AttachedDevice? Primary
    {
        get { lock (_lock) { return _devices.FirstOrDefault(d => d.Role == DeviceRole.Primary); } }
    }

    /// <summary>Alle auxiliary devices (RX).</summary>
    public IReadOnlyList<AttachedDevice> Auxiliaries
    {
        get { lock (_lock) { return [.._devices.Where(d => d.Role == DeviceRole.Auxiliary)]; } }
    }

    /// <summary>MOX-status van het primary device.</summary>
    public bool IsMoxActive { get; private set; }

    // ── Attach / Detach ───────────────────────────────────────────────────────

    /// <summary>
    /// Voeg een device toe aan de sessie.
    /// Het eerste device wordt automatisch Primary als geen ander Primary aanwezig is.
    /// </summary>
    public async Task<AttachedDevice> AttachAsync(
        IDeviceSource device,
        DeviceRole? forceRole = null,
        DeviceOpenOptions? openOptions = null,
        CancellationToken ct = default)
    {
        var role = forceRole ?? DetermineRole(device);

        // Zorg dat er slechts één Primary is
        if (role == DeviceRole.Primary)
            await EnsureNoPrimaryAsync(ct);

        var channelId = role == DeviceRole.Primary
            ? _allocator.AllocatePrimaryRxChannel()
            : _allocator.AllocateAuxRxChannel();

        var opts = openOptions ?? new DeviceOpenOptions
        {
            PreferredSampleRateHz = role == DeviceRole.Primary ? 48_000 : 2_000_000,
            InitialFrequencyHz    = 14_200_000,
        };

        var opened = await device.OpenAsync(opts, ct);
        if (!opened)
            throw new InvalidOperationException($"Device '{device.FriendlyName}' weigerde OpenAsync.");

        var attached = new AttachedDevice(device, role, _allocator, channelId);

        // Standaard audio-routing: primary = Left, eerste aux = Right, rest = MonoMix
        attached.AudioRoute = role == DeviceRole.Primary
            ? AudioRoute.Left
            : Auxiliaries.Count == 0 ? AudioRoute.Right : AudioRoute.MonoMix;

        lock (_lock) { _devices.Add(attached); }

        _logger.LogInformation(
            "Device gekoppeld: '{Name}' als {Role} op WDSP ch {Ch}",
            device.FriendlyName, role, channelId);

        return attached;
    }

    /// <summary>Verwijder een device en geef resources vrij.</summary>
    public async Task DetachAsync(string deviceId, CancellationToken ct = default)
    {
        AttachedDevice? found;
        lock (_lock)
        {
            found = _devices.FirstOrDefault(d => d.Device.DeviceId == deviceId);
            if (found is not null) _devices.Remove(found);
        }

        if (found is not null)
        {
            await found.DisposeAsync();
            _logger.LogInformation("Device losgekoppeld: '{Name}'", found.Device.FriendlyName);
        }
    }

    // ── MOX / PTT ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Activeer TX op het primary device.
    /// Blokkeert TX op alle auxiliary <see cref="ITransceiver"/> devices
    /// tenzij <see cref="AttachedDevice.AllowSimultaneousTx"/> is ingesteld.
    /// </summary>
    public async Task SetMoxAsync(bool keyed, CancellationToken ct = default)
    {
        IsMoxActive = keyed;

        if (Primary?.Device is ITransceiver primaryTx)
            await primaryTx.SetMoxAsync(keyed, ct);

        if (keyed)
        {
            // PTT lockout: blokkeer TX op alle aux ITransceiver devices
            foreach (var aux in Auxiliaries)
            {
                if (!aux.AllowSimultaneousTx && aux.Device is ITransceiver auxTx)
                    await auxTx.SetMoxAsync(false, ct);
            }
            _logger.LogDebug("PTT lockout toegepast op {Count} aux device(s)", Auxiliaries.Count);
        }
    }

    // ── Frequentie synchronisatie ─────────────────────────────────────────────

    /// <summary>
    /// Verander de frequentie van het primary device.
    /// Propageert naar aux devices op basis van hun <see cref="FreqSyncPolicy"/>.
    /// </summary>
    public async Task SetPrimaryFrequencyAsync(long hz, CancellationToken ct = default)
    {
        if (Primary is null) return;

        Primary.FrequencyHz = hz;
        await Primary.Device.SetFrequencyAsync(hz, ct);

        // Sync naar aux devices
        foreach (var aux in Auxiliaries.Where(a => a.IsEnabled))
        {
            var targetHz = aux.FreqSync switch
            {
                FreqSyncPolicy.FollowPrimary           => hz,
                FreqSyncPolicy.FollowPrimaryWithOffset => hz + aux.FreqSyncOffsetHz,
                _                                       => (long?)null,
            };

            if (targetHz.HasValue)
            {
                aux.FrequencyHz = targetHz.Value;
                await aux.Device.SetFrequencyAsync(targetHz.Value, ct);
            }
        }
    }

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>Geeft een overzicht van de huidige sessie voor logging/debug.</summary>
    public string Summary()
    {
        var lines = Devices.Select(d =>
            $"  [{d.Role,9}] {d.Device.FriendlyName,-40} ch={d.WdspChannelId,2} " +
            $"freq={d.FrequencyHz / 1e6:F3} MHz  audio={d.AudioRoute}  sync={d.FreqSync}");
        return "RadioSession:\n" + string.Join("\n", lines);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DeviceRole DetermineRole(IDeviceSource device)
    {
        lock (_lock)
        {
            // Eerste device = Primary als het TX kan; anders Auxiliary
            if (!_devices.Any(d => d.Role == DeviceRole.Primary))
                return device.Capabilities.HasFlag(DeviceCapabilities.Transmit)
                    ? DeviceRole.Primary
                    : DeviceRole.Auxiliary;

            return DeviceRole.Auxiliary;
        }
    }

    private async Task EnsureNoPrimaryAsync(CancellationToken ct)
    {
        AttachedDevice? existing;
        lock (_lock) { existing = _devices.FirstOrDefault(d => d.Role == DeviceRole.Primary); }

        if (existing is not null)
        {
            _logger.LogWarning(
                "Primary device '{Name}' wordt vervangen door nieuw primary device.",
                existing.Device.FriendlyName);
            await DetachAsync(existing.Device.DeviceId, ct);
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        List<AttachedDevice> toDispose;
        lock (_lock) { toDispose = [.._devices]; _devices.Clear(); }

        foreach (var d in toDispose)
            await d.DisposeAsync();
    }
}
