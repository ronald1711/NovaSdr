using Microsoft.Extensions.Logging;

namespace NovaSdr.Devices.SdrPlay;

/// <summary>
/// Ontdekt SDRplay-devices via SDRplay API 3.x.
/// Geeft een lege lijst terug als de API niet geïnstalleerd is (geen crash).
/// </summary>
public sealed class SdrplayEnumerator(ILogger<SdrplayEnumerator> logger)
{
    private const uint MaxDevices = 16;

    /// <summary>
    /// Zoek alle aangesloten SDRplay-devices.
    /// Retourneert leeg als de SDRplay API niet gevonden wordt.
    /// </summary>
    public IReadOnlyList<SdrplaySource> Enumerate(ILoggerFactory loggerFactory)
    {
        try
        {
            var err = SdrplayNative.Open();
            if (err != SdrplayError.Success)
            {
                logger.LogWarning("SDRplay API Open: {Error}", err);
                return [];
            }

            var devices = new SdrplayDeviceT[MaxDevices];
            err = SdrplayNative.GetDevices(devices, out var numDevs, MaxDevices);
            if (err != SdrplayError.Success || numDevs == 0)
            {
                SdrplayNative.Close();
                logger.LogInformation("Geen SDRplay devices gevonden");
                return [];
            }

            var sources = new List<SdrplaySource>();
            for (int i = 0; i < (int)numDevs; i++)
            {
                var selectErr = SdrplayNative.SelectDevice(ref devices[i]);
                if (selectErr != SdrplayError.Success)
                {
                    logger.LogWarning("SelectDevice {Serial} mislukt: {Err}", devices[i].SerNo, selectErr);
                    continue;
                }

                var sourceLogger = loggerFactory.CreateLogger<SdrplaySource>();
                sources.Add(new SdrplaySource(devices[i], sourceLogger));
                logger.LogInformation("SDRplay gevonden: HW v{HwVer}, S/N {Serial}", devices[i].HwVer, devices[i].SerNo);
            }

            return sources;
        }
        catch (DllNotFoundException)
        {
            logger.LogInformation(
                "SDRplay API niet gevonden. Installeer via https://www.sdrplay.com/api/ om SDRplay-support te activeren.");
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Onverwachte fout bij SDRplay enumeratie");
            return [];
        }
    }
}
