namespace PantheonSDR.Devices;

/// <summary>
/// Houdt alle ontdekte en verbonden devices bij.
/// Eén instantie per applicatiesessie (Singleton in DI).
/// </summary>
public sealed class DeviceRegistry
{
    private readonly List<IDeviceSource> _discovered = [];
    private readonly Lock _lock = new();

    public event EventHandler<IDeviceSource>? DeviceDiscovered;
    public event EventHandler<string>? DeviceRemoved;

    /// <summary>Snapshot van alle momenteel ontdekte devices.</summary>
    public IReadOnlyList<IDeviceSource> DiscoveredDevices
    {
        get { lock (_lock) { return [.._discovered]; } }
    }

    public void RegisterDiscovered(IDeviceSource device)
    {
        lock (_lock)
        {
            if (_discovered.Any(d => d.DeviceId == device.DeviceId)) return;
            _discovered.Add(device);
        }
        DeviceDiscovered?.Invoke(this, device);
    }

    public void RemoveDevice(string deviceId)
    {
        lock (_lock) { _discovered.RemoveAll(d => d.DeviceId == deviceId); }
        DeviceRemoved?.Invoke(this, deviceId);
    }

    public IDeviceSource? FindById(string deviceId)
    {
        lock (_lock) { return _discovered.FirstOrDefault(d => d.DeviceId == deviceId); }
    }
}
