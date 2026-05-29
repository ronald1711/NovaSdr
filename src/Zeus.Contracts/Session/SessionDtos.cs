// PantheonSDR multi-device session DTOs.

namespace Zeus.Contracts.Session;

public sealed record SessionStateDto(
    AttachedDeviceDto? Primary,
    IReadOnlyList<AttachedDeviceDto> Auxiliaries,
    bool IsMoxActive);

public sealed record AttachedDeviceDto(
    string DeviceId,
    string FriendlyName,
    string Role,
    long CapabilityFlags,
    bool CanTransmit,
    int WdspChannelId,
    long FrequencyHz,
    string FreqSync,
    long FreqSyncOffsetHz,
    string AudioRoute,
    bool IsEnabled);

public sealed record DiscoveredDeviceDto(
    string DeviceId,
    string FriendlyName,
    string Protocol,
    long CapabilityFlags,
    bool CanTransmit,
    long MinFrequencyHz,
    long MaxFrequencyHz);

public sealed record AttachRequest(
    string DeviceId,
    string? Role = null,
    long InitialFrequencyHz = 14_200_000,
    string FreqSync = "Independent",
    string AudioRoute = "Right");

public sealed record Rx2FrequencyRequest(long FrequencyHz);
public sealed record FreqSyncRequest(string Policy, long OffsetHz = 0);
public sealed record AudioRouteRequest(string Route);
