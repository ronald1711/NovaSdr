using FluentAssertions;
using PantheonSDR.Devices;

namespace PantheonSDR.Devices.Tests;

public sealed class WdspChannelAllocatorTests
{
    [Fact]
    public void AllocatePrimaryRx_ReturnsChannelInRange0to13()
    {
        var allocator = new WdspChannelAllocator();
        var ch = allocator.AllocatePrimaryRxChannel();
        ch.Should().BeInRange(0, 13);
    }

    [Fact]
    public void AllocateAuxRx_ReturnsChannelInRange16to29()
    {
        var allocator = new WdspChannelAllocator();
        var ch = allocator.AllocateAuxRxChannel();
        ch.Should().BeInRange(16, 29);
    }

    [Fact]
    public void PrimaryAndAuxChannels_NeverOverlap()
    {
        var allocator = new WdspChannelAllocator();
        var primary = Enumerable.Range(0, 5).Select(_ => allocator.AllocatePrimaryRxChannel()).ToList();
        var aux = Enumerable.Range(0, 5).Select(_ => allocator.AllocateAuxRxChannel()).ToList();

        primary.Intersect(aux).Should().BeEmpty("primary en aux kanalen mogen nooit overlappen");
    }

    [Fact]
    public void ReleaseChannel_MakesItAvailableAgain()
    {
        var allocator = new WdspChannelAllocator();
        var ch = allocator.AllocatePrimaryRxChannel();
        allocator.IsAllocated(ch).Should().BeTrue();

        allocator.ReleaseChannel(ch);
        allocator.IsAllocated(ch).Should().BeFalse();

        var ch2 = allocator.AllocatePrimaryRxChannel();
        ch2.Should().Be(ch, "vrijgegeven kanaal moet opnieuw toewijsbaar zijn");
    }

    [Fact]
    public void TxChannel_IsNotAllocatedByRxMethods()
    {
        var allocator = new WdspChannelAllocator();
        // Alle primary RX kanalen opgebruiken
        var channels = Enumerable.Range(0, 13)
            .Select(_ => allocator.AllocatePrimaryRxChannel())
            .ToList();

        channels.Should().NotContain(WdspChannelAllocator.PrimaryTxChannel,
            "TX kanaal (14) mag nooit door RX allocator worden uitgegeven");
    }

    [Fact]
    public void AllocatePrimary_ThrowsWhenExhausted()
    {
        var allocator = new WdspChannelAllocator();
        for (int i = 0; i < 13; i++) allocator.AllocatePrimaryRxChannel();

        var act = () => allocator.AllocatePrimaryRxChannel();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeviceRegistry_DeduplicatesByDeviceId()
    {
        var registry = new DeviceRegistry();
        var device = new FakeDevice("serial-001", "Test SDR");

        registry.RegisterDiscovered(device);
        registry.RegisterDiscovered(device); // tweede keer zelfde device

        registry.DiscoveredDevices.Should().HaveCount(1);
    }

    [Fact]
    public void DeviceRegistry_FiresEventOnNewDevice()
    {
        var registry = new DeviceRegistry();
        IDeviceSource? received = null;
        registry.DeviceDiscovered += (_, d) => received = d;

        var device = new FakeDevice("serial-002", "Another SDR");
        registry.RegisterDiscovered(device);

        received.Should().Be(device);
    }
}

/// <summary>Minimale IDeviceSource stub voor tests.</summary>
file sealed class FakeDevice(string deviceId, string friendlyName) : IDeviceSource
{
    public string DeviceId => deviceId;
    public string FriendlyName => friendlyName;
    public DeviceCapabilities Capabilities => DeviceCapabilities.Receive;
    public FrequencyRange[] SupportedRanges => [new(0, 2_000_000_000)];
    public int[] SupportedSampleRates => [48_000];

    public Task<bool> OpenAsync(DeviceOpenOptions options, CancellationToken ct) => Task.FromResult(true);
    public IAsyncEnumerable<IqBlock> StreamAsync(CancellationToken ct) => AsyncEnumerable.Empty<IqBlock>();
    public Task SetFrequencyAsync(long hz, CancellationToken ct) => Task.CompletedTask;
    public Task SetGainAsync(double db, CancellationToken ct) => Task.CompletedTask;
    public Task SetSampleRateAsync(int hz, CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
