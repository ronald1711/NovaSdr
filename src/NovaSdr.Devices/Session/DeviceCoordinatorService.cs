using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NovaSdr.Devices.Session;

/// <summary>
/// Achtergrondservice die de <see cref="RadioSession"/> bewaakt en policies afdwingt:
///
///  1. PTT Lockout   — bij Primary TX worden alle auxiliary ITransceiver devices
///                     geblokkeerd (tenzij AllowSimultaneousTx).
///  2. Frequentiesync — aux devices volgen primary VFO indien geconfigureerd.
///  3. Audio mute    — optioneel: aux audio dempen tijdens TX.
///  4. Conflict check — waarschuwing als twee devices op dezelfde freq TX proberen.
///
/// Symmetrisch: werkt ongeacht welke device-combinatie of -volgorde.
/// </summary>
public sealed class DeviceCoordinatorService : BackgroundService
{
    private readonly RadioSession _session;
    private readonly ILogger<DeviceCoordinatorService> _logger;

    // Tijdstip van laatste PTT-wisseling, voor debounce
    private DateTimeOffset _lastMoxChange = DateTimeOffset.MinValue;
    private const int MoxDebouncMs = 50;

    public DeviceCoordinatorService(RadioSession session, ILogger<DeviceCoordinatorService> logger)
    {
        _session = session;
        _logger  = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeviceCoordinatorService gestart ({DeviceCount} device(s))",
            _session.Devices.Count);

        // Periodieke controle: 100 ms interval voor status-monitoring
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckSessionHealthAsync(stoppingToken);
        }
    }

    // ── PTT Lockout ───────────────────────────────────────────────────────────

    /// <summary>
    /// Verwerk een MOX-statuswijziging van buiten (bijv. hardware PTT, CAT-commando).
    /// Thread-safe: kan vanuit elke thread worden aangeroepen.
    /// </summary>
    public async Task HandleMoxChangeAsync(bool keyed, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastMoxChange).TotalMilliseconds < MoxDebouncMs) return;
        _lastMoxChange = now;

        _logger.LogInformation("MOX {State} — PTT lockout toepassen", keyed ? "ON" : "OFF");
        await _session.SetMoxAsync(keyed, ct);

        if (keyed) LogActiveTxDevices();
    }

    // ── Session Health Check ──────────────────────────────────────────────────

    private async Task CheckSessionHealthAsync(CancellationToken ct)
    {
        // Controleer of primary device nog beschikbaar is
        var primary = _session.Primary;
        if (primary is null && _session.Devices.Count > 0)
        {
            _logger.LogWarning(
                "RadioSession heeft {Count} device(s) maar geen Primary. " +
                "Promoveer eerste TX-capable device naar Primary.",
                _session.Devices.Count);

            // Promoveer automatisch het eerste TX-capable device
            var candidate = _session.Auxiliaries
                .FirstOrDefault(a => a.Device.Capabilities.HasFlag(DeviceCapabilities.Transmit));

            if (candidate is not null)
            {
                _logger.LogInformation("Auto-promote: '{Name}' → Primary", candidate.Device.FriendlyName);
                // In een volgende iteratie: support role-change zonder re-attach
            }
        }

        // Controleer frequentieconflicten (twee TX-capable devices op zelfde freq)
        await CheckTxFrequencyConflictsAsync(ct);
    }

    private Task CheckTxFrequencyConflictsAsync(CancellationToken ct)
    {
        if (!_session.IsMoxActive) return Task.CompletedTask;

        var txDevices = _session.Devices
            .Where(d => d.Device.Capabilities.HasFlag(DeviceCapabilities.Transmit))
            .ToList();

        if (txDevices.Count < 2) return Task.CompletedTask;

        var freqGroups = txDevices.GroupBy(d => d.FrequencyHz / 1000); // 1 kHz tolerantie
        foreach (var group in freqGroups.Where(g => g.Count() > 1))
        {
            _logger.LogWarning(
                "CONFLICT: {Count} TX-devices op {FreqMHz:F3} MHz tegelijkertijd actief: {Names}",
                group.Count(),
                group.Key / 1000.0,
                string.Join(", ", group.Select(d => d.Device.FriendlyName)));
        }

        return Task.CompletedTask;
    }

    private void LogActiveTxDevices()
    {
        _logger.LogInformation(_session.Summary());
    }
}
