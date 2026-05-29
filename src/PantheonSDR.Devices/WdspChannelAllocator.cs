namespace PantheonSDR.Devices;

/// <summary>
/// Verdeelt WDSP channel IDs over primary en auxiliary devices.
/// WDSP ondersteunt MAX_CHANNELS=32; we partitioneren:
///   0-13  → primary RX kanalen + TX (ch 14)
///  16-29  → auxiliary RX (max 14 aux devices)
/// </summary>
public sealed class WdspChannelAllocator
{
    private const int PrimaryRxStart = 0;
    private const int PrimaryRxCount = 13;
    public const int PrimaryTxChannel = 14;
    private const int AuxRxStart = 16;
    private const int AuxRxCount = 14;

    private readonly HashSet<int> _usedChannels = [];
    private readonly Lock _lock = new();

    public int AllocatePrimaryRxChannel()
    {
        lock (_lock)
        {
            for (int i = PrimaryRxStart; i < PrimaryRxStart + PrimaryRxCount; i++)
            {
                if (_usedChannels.Add(i)) return i;
            }
            throw new InvalidOperationException("Geen primary RX channels meer beschikbaar (max 13).");
        }
    }

    public int AllocateAuxRxChannel()
    {
        lock (_lock)
        {
            for (int i = AuxRxStart; i < AuxRxStart + AuxRxCount; i++)
            {
                if (_usedChannels.Add(i)) return i;
            }
            throw new InvalidOperationException("Geen auxiliary RX channels meer beschikbaar (max 14).");
        }
    }

    public void ReleaseChannel(int channelId)
    {
        lock (_lock) { _usedChannels.Remove(channelId); }
    }

    public bool IsAllocated(int channelId)
    {
        lock (_lock) { return _usedChannels.Contains(channelId); }
    }
}
