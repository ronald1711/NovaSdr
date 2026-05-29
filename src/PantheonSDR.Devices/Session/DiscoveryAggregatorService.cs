using PantheonSDR.Devices.OpenHpsdr;
using PantheonSDR.Devices.PlutoSdr;
using PantheonSDR.Devices.SdrPlay;
using Microsoft.Extensions.Logging;

namespace PantheonSDR.Devices.Session;

/// <summary>
/// Combineert discovery van alle ondersteunde hardware-families
/// in één uniforme lijst van <see cref="IDeviceSource"/> objecten.
///
/// Ondersteunde families (extensief uitbreidbaar):
///   • OpenHPSDR P1 — Hermes-Lite 2, Brick2 in P1 mode, ANAN classic
///   • OpenHPSDR P2 — Brick2, ANAN G2/G2 MkII, Saturn G2
///   • PlutoSDR Plus — F5OEO firmware, Ethernet/USB
///   • SDRplay RSP   — RSP1A, RSP2, RSPdx, RSPduo (API 3.x, user-installed)
///
/// Toekomstige uitbreidingen: RTL-SDR, SoapySDR generiek.
/// </summary>
public sealed class DiscoveryAggregatorService(
    DeviceRegistry registry,
    ILoggerFactory loggerFactory,
    ILogger<DiscoveryAggregatorService> logger)
{
    /// <summary>
    /// Voer een volledige discovery-cyclus uit op alle families.
    /// Geeft een snapshot terug van alle gevonden devices.
    /// Thread-safe: kan meerdere keren worden aangeroepen.
    /// </summary>
    public async Task<IReadOnlyList<IDeviceSource>> DiscoverAllAsync(
        DiscoveryOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? new DiscoveryOptions();
        var found = new List<IDeviceSource>();

        logger.LogInformation("Discovery gestart...");

        // Parallelle discovery over alle families
        var tasks = new List<Task<IReadOnlyList<IDeviceSource>>>();

        if (opts.IncludeOpenHpsdr)
            tasks.Add(DiscoverOpenHpsdrAsync(ct));

        if (opts.IncludePlutoSdr)
            tasks.Add(Task.Run(() => DiscoverPlutoSdrAsync(opts.PlutoHosts), ct));

        if (opts.IncludeSdrPlay)
            tasks.Add(Task.Run(() => DiscoverSdrPlayAsync(), ct));

        await Task.WhenAll(tasks);

        foreach (var task in tasks)
        {
            foreach (var dev in task.Result)
            {
                registry.RegisterDiscovered(dev);
                found.Add(dev);
            }
        }

        logger.LogInformation(
            "Discovery voltooid: {Count} device(s) gevonden — {Names}",
            found.Count,
            found.Count > 0
                ? string.Join(", ", found.Select(d => d.FriendlyName))
                : "geen");

        return found;
    }

    // ── Per-familie discovery ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<IDeviceSource>> DiscoverOpenHpsdrAsync(CancellationToken ct)
    {
        var results = new List<IDeviceSource>();
        try
        {
            // Protocol 1 discovery (UDP broadcast poort 1024)
            var p1Enumerator = new OpenHpsdrDiscovery(
                loggerFactory.CreateLogger<OpenHpsdrDiscovery>());
            var p1Devices = await p1Enumerator.DiscoverP1Async(ct);
            results.AddRange(p1Devices);

            // Protocol 2 discovery (UDP broadcast poort 1024)
            var p2Devices = await p1Enumerator.DiscoverP2Async(ct);
            results.AddRange(p2Devices);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenHPSDR discovery mislukt");
        }
        return results;
    }

    private IReadOnlyList<IDeviceSource> DiscoverPlutoSdrAsync(IEnumerable<string>? extraHosts)
    {
        try
        {
            var enumerator = new PlutoEnumerator(
                loggerFactory.CreateLogger<PlutoEnumerator>());
            return [..enumerator.EnumerateNetwork(loggerFactory, extraHosts),
                    ..( enumerator.EnumerateUsb(loggerFactory) is { } usb ? [usb] : [] )];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PlutoSDR discovery mislukt");
            return [];
        }
    }

    private IReadOnlyList<IDeviceSource> DiscoverSdrPlayAsync()
    {
        try
        {
            var enumerator = new SdrplayEnumerator(
                loggerFactory.CreateLogger<SdrplayEnumerator>());
            return [..enumerator.Enumerate(loggerFactory)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SDRplay discovery mislukt");
            return [];
        }
    }
}

/// <summary>Configuratie-opties voor discovery.</summary>
public sealed class DiscoveryOptions
{
    public bool IncludeOpenHpsdr { get; init; } = true;
    public bool IncludePlutoSdr  { get; init; } = true;
    public bool IncludeSdrPlay   { get; init; } = true;

    /// <summary>Extra PlutoSDR IP-adressen of hostnamen naast de standaard 192.168.2.1.</summary>
    public IEnumerable<string>? PlutoHosts { get; init; }
}
