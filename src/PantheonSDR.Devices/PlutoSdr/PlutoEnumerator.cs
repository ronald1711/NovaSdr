using Microsoft.Extensions.Logging;

namespace PantheonSDR.Devices.PlutoSdr;

/// <summary>
/// Ontdekt PlutoSDR / PlutoSDR Plus devices op het netwerk of via USB.
/// </summary>
public sealed class PlutoEnumerator(ILogger<PlutoEnumerator> logger)
{
    // Standaard PlutoSDR Ethernet IP (instelbaar in firmware)
    private static readonly string[] DefaultHosts = ["192.168.2.1", "pluto.local"];

    /// <summary>
    /// Probeer bekende adressen en retourneer gevonden PlutoSDR-devices.
    /// Retourneert leeg als libiio niet geïnstalleerd is.
    /// </summary>
    public IReadOnlyList<PlutoSdrTransceiver> EnumerateNetwork(ILoggerFactory loggerFactory,
        IEnumerable<string>? additionalHosts = null)
    {
        var results = new List<PlutoSdrTransceiver>();
        var hosts = DefaultHosts.Concat(additionalHosts ?? []);

        try
        {
            foreach (var host in hosts)
            {
                var ctx = IioNative.CreateNetworkContext(host);
                if (ctx == nint.Zero) continue;

                var name = IioNative.ContextGetName(ctx);
                IioNative.ContextDestroy(ctx);

                var deviceLogger = loggerFactory.CreateLogger<PlutoSdrTransceiver>();
                var friendly = $"PlutoSDR Plus F5OEO @ {host}";
                var trx = new PlutoSdrTransceiver($"ip:{host}", friendly, deviceLogger);
                results.Add(trx);

                logger.LogInformation("PlutoSDR gevonden op {Host} ({Name})", host, name ?? "onbekend");
            }
        }
        catch (DllNotFoundException)
        {
            logger.LogInformation(
                "libiio niet gevonden. Installeer via https://github.com/analogdevicesinc/libiio voor PlutoSDR-support.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fout bij PlutoSDR enumeratie");
        }

        return results;
    }

    /// <summary>
    /// Probeer USB-verbinding via uri "usb:".
    /// </summary>
    public PlutoSdrTransceiver? EnumerateUsb(ILoggerFactory loggerFactory)
    {
        try
        {
            var ctx = IioNative.CreateContextFromUri("usb:");
            if (ctx == nint.Zero) return null;

            var name = IioNative.ContextGetName(ctx);
            IioNative.ContextDestroy(ctx);

            var deviceLogger = loggerFactory.CreateLogger<PlutoSdrTransceiver>();
            logger.LogInformation("PlutoSDR via USB gevonden ({Name})", name ?? "onbekend");
            return new PlutoSdrTransceiver("usb:", "PlutoSDR Plus F5OEO (USB)", deviceLogger);
        }
        catch (DllNotFoundException)
        {
            return null;
        }
    }
}
