// PantheonSDR multi-device session REST endpoints.
// Mounted at /api/session by ZeusHost.Build().

using PantheonSDR.Devices;
using PantheonSDR.Devices.Session;
using Zeus.Contracts.Session;

namespace Zeus.Server;

internal static class SessionEndpoints
{
    internal static void MapSessionEndpoints(this WebApplication app)
    {
        // ── Discovery ─────────────────────────────────────────────────────────

        // GET /api/session/devices/discovered
        // Returns all devices found by the last discovery run.
        app.MapGet("/api/session/devices/discovered", (DeviceRegistry registry) =>
        {
            var devices = registry.DiscoveredDevices.Select(ToDiscoveredDto).ToList();
            return Results.Ok(devices);
        });

        // POST /api/session/discover
        // Trigger a new discovery sweep across all hardware families.
        app.MapPost("/api/session/discover", async (
            DiscoveryAggregatorService discovery,
            CancellationToken ct) =>
        {
            var found = await discovery.DiscoverAllAsync(ct: ct);
            return Results.Ok(found.Select(ToDiscoveredDto).ToList());
        });

        // ── Session state ─────────────────────────────────────────────────────

        // GET /api/session
        // Returns the current session: primary + auxiliaries + MOX state.
        app.MapGet("/api/session", (RadioSession session) =>
            Results.Ok(ToSessionDto(session)));

        // ── Attach / Detach ───────────────────────────────────────────────────

        // POST /api/session/attach
        // Attach a discovered device to the session.
        // Body: AttachRequest { DeviceId, Role?, InitialFrequencyHz, FreqSync, AudioRoute }
        app.MapPost("/api/session/attach", async (
            AttachRequest req,
            RadioSession session,
            DeviceRegistry registry,
            StreamingHub hub,
            CancellationToken ct) =>
        {
            var device = registry.FindById(req.DeviceId);
            if (device is null)
                return Results.NotFound($"Device '{req.DeviceId}' not found. Run /api/session/discover first.");

            DeviceRole? role = req.Role?.ToUpperInvariant() switch
            {
                "PRIMARY"   => DeviceRole.Primary,
                "AUXILIARY" => DeviceRole.Auxiliary,
                _           => null,
            };

            var opts = new DeviceOpenOptions
            {
                InitialFrequencyHz    = req.InitialFrequencyHz,
                PreferredSampleRateHz = role == DeviceRole.Primary ? 48_000 : 2_000_000,
            };

            var attached = await session.AttachAsync(device, role, opts, ct);

            // Apply freq sync and audio route from request
            if (Enum.TryParse<FreqSyncPolicy>(req.FreqSync, true, out var syncPolicy))
                attached.FreqSync = syncPolicy;
            if (Enum.TryParse<AudioRoute>(req.AudioRoute, true, out var audioRoute))
                attached.AudioRoute = audioRoute;

            // Broadcast updated session state to all connected clients
            hub.BroadcastSessionState(ToSessionDto(session));

            return Results.Ok(ToAttachedDto(attached));
        });

        // DELETE /api/session/devices/{deviceId}
        // Detach a device from the session and release its WDSP channel.
        app.MapDelete("/api/session/devices/{deviceId}", async (
            string deviceId,
            RadioSession session,
            StreamingHub hub,
            CancellationToken ct) =>
        {
            var found = session.Devices.Any(d => d.Device.DeviceId == deviceId);
            if (!found)
                return Results.NotFound($"Device '{deviceId}' is not attached.");

            await session.DetachAsync(deviceId, ct);
            hub.BroadcastSessionState(ToSessionDto(session));
            return Results.Ok();
        });

        // ── Per-device controls ───────────────────────────────────────────────

        // POST /api/session/devices/{deviceId}/frequency
        // Set frequency on any attached device (primary or auxiliary).
        app.MapPost("/api/session/devices/{deviceId}/frequency", async (
            string deviceId,
            Rx2FrequencyRequest req,
            RadioSession session,
            CancellationToken ct) =>
        {
            var attached = session.Devices.FirstOrDefault(d => d.Device.DeviceId == deviceId);
            if (attached is null) return Results.NotFound();

            if (attached.Role == DeviceRole.Primary)
            {
                // Primary: use RadioSession.SetPrimaryFrequencyAsync to propagate sync
                await session.SetPrimaryFrequencyAsync(req.FrequencyHz, ct);
            }
            else
            {
                attached.FrequencyHz = req.FrequencyHz;
                await attached.Device.SetFrequencyAsync(req.FrequencyHz, ct);
            }
            return Results.Ok();
        });

        // POST /api/session/devices/{deviceId}/freqsync
        // Change the frequency-sync policy for an auxiliary device.
        app.MapPost("/api/session/devices/{deviceId}/freqsync", (
            string deviceId,
            FreqSyncRequest req,
            RadioSession session) =>
        {
            var attached = session.Devices.FirstOrDefault(d => d.Device.DeviceId == deviceId);
            if (attached is null) return Results.NotFound();

            if (!Enum.TryParse<FreqSyncPolicy>(req.Policy, true, out var policy))
                return Results.BadRequest($"Unknown FreqSyncPolicy: '{req.Policy}'");

            attached.FreqSync         = policy;
            attached.FreqSyncOffsetHz = req.OffsetHz;
            return Results.Ok();
        });

        // POST /api/session/devices/{deviceId}/audio
        // Change the audio routing for a device.
        app.MapPost("/api/session/devices/{deviceId}/audio", (
            string deviceId,
            AudioRouteRequest req,
            RadioSession session) =>
        {
            var attached = session.Devices.FirstOrDefault(d => d.Device.DeviceId == deviceId);
            if (attached is null) return Results.NotFound();

            if (!Enum.TryParse<AudioRoute>(req.Route, true, out var route))
                return Results.BadRequest($"Unknown AudioRoute: '{req.Route}'");

            attached.AudioRoute = route;
            return Results.Ok();
        });

        // POST /api/session/devices/{deviceId}/gain
        // Set RX gain on any device.
        app.MapPost("/api/session/devices/{deviceId}/gain", async (
            string deviceId,
            double gainDb,
            RadioSession session,
            CancellationToken ct) =>
        {
            var attached = session.Devices.FirstOrDefault(d => d.Device.DeviceId == deviceId);
            if (attached is null) return Results.NotFound();
            await attached.Device.SetGainAsync(gainDb, ct);
            return Results.Ok();
        });

        // POST /api/session/mox
        // Set MOX on the primary device with full PTT lockout.
        app.MapPost("/api/session/mox", async (
            bool keyed,
            RadioSession session,
            DeviceCoordinatorService coordinator,
            CancellationToken ct) =>
        {
            await coordinator.HandleMoxChangeAsync(keyed, ct);
            return Results.Ok(new { IsMoxActive = session.IsMoxActive });
        });
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static SessionStateDto ToSessionDto(RadioSession session) =>
        new(
            Primary:      session.Primary is { } p ? ToAttachedDto(p) : null,
            Auxiliaries:  session.Auxiliaries.Select(ToAttachedDto).ToList(),
            IsMoxActive:  session.IsMoxActive);

    private static AttachedDeviceDto ToAttachedDto(PantheonSDR.Devices.Session.AttachedDevice a) =>
        new(
            DeviceId:          a.Device.DeviceId,
            FriendlyName:      a.Device.FriendlyName,
            Role:              a.Role.ToString(),
            CapabilityFlags:   (long)a.Device.Capabilities,
            CanTransmit:       a.Device.Capabilities.HasFlag(DeviceCapabilities.Transmit),
            WdspChannelId:     a.WdspChannelId,
            FrequencyHz:       a.FrequencyHz,
            FreqSync:          a.FreqSync.ToString(),
            FreqSyncOffsetHz:  a.FreqSyncOffsetHz,
            AudioRoute:        a.AudioRoute.ToString(),
            IsEnabled:         a.IsEnabled);

    private static DiscoveredDeviceDto ToDiscoveredDto(IDeviceSource d)
    {
        var range = d.SupportedRanges.FirstOrDefault();
        return new(
            DeviceId:       d.DeviceId,
            FriendlyName:   d.FriendlyName,
            Protocol:       InferProtocol(d),
            CapabilityFlags:(long)d.Capabilities,
            CanTransmit:    d.Capabilities.HasFlag(DeviceCapabilities.Transmit),
            MinFrequencyHz: range.MinHz,
            MaxFrequencyHz: range.MaxHz);
    }

    private static string InferProtocol(IDeviceSource d) => d.DeviceId switch
    {
        var id when id.StartsWith("sdrplay:")  => "SdrPlay",
        var id when id.StartsWith("pluto:")    => "PlutoSdr",
        var id when id.StartsWith("rtlsdr:")   => "RtlSdr",
        var id when id.StartsWith("hpsdr-p1:") => "OpenHpsdrP1",
        var id when id.StartsWith("hpsdr-p2:") => "OpenHpsdrP2",
        _                                       => "Unknown",
    };
}
