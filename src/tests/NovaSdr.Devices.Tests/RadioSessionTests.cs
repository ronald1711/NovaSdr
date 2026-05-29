using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NovaSdr.Devices;
using NovaSdr.Devices.Session;

namespace NovaSdr.Devices.Tests;

public sealed class RadioSessionTests
{
    private static RadioSession MakeSession() =>
        new(new WdspChannelAllocator(), NullLogger<RadioSession>.Instance);

    // ── Roltoewijzing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FirstTxDevice_BecomesPrimary()
    {
        await using var session = MakeSession();
        var attached = await session.AttachAsync(new FakeTrx("brick2-1", "Brick2 P2"));
        attached.Role.Should().Be(DeviceRole.Primary);
    }

    [Fact]
    public async Task SecondDevice_BecomesAuxiliary()
    {
        await using var session = MakeSession();
        await session.AttachAsync(new FakeTrx("brick2-1", "Brick2 P2"));
        var aux = await session.AttachAsync(new FakeRx("rsp1a-1", "SDRplay RSP1A"));
        aux.Role.Should().Be(DeviceRole.Auxiliary);
    }

    [Fact]
    public async Task TwoOpenHpsdrDevices_PrimaryThenAuxiliary()
    {
        // 2x OpenHPSDR — meest gevraagde combinatie
        await using var session = MakeSession();
        await session.AttachAsync(new FakeTrx("brick2-1", "Brick2 P2"));
        await session.AttachAsync(new FakeTrx("hl2-1", "Hermes-Lite 2 P1"));

        session.Primary!.Device.DeviceId.Should().Be("brick2-1");
        session.Auxiliaries.Should().ContainSingle(a => a.Device.DeviceId == "hl2-1");
    }

    [Fact]
    public async Task ThreeDevicesMixed_CorrectRoles()
    {
        // Brick2 primary + SDRplay aux + PlutoSDR aux
        await using var session = MakeSession();
        await session.AttachAsync(new FakeTrx("brick2-1", "Brick2 P2"));
        await session.AttachAsync(new FakeRx("rsp1a-1",  "SDRplay RSP1A"));
        await session.AttachAsync(new FakeTrx("pluto-1",  "PlutoSDR Plus"));

        session.Devices.Should().HaveCount(3);
        session.Primary!.Device.DeviceId.Should().Be("brick2-1");
        session.Auxiliaries.Should().HaveCount(2);
    }

    [Fact]
    public async Task RxOnlyFirstDevice_BecomesAuxiliary_NoTxCapability()
    {
        // RX-only device mag nooit automatisch Primary worden
        await using var session = MakeSession();
        var aux = await session.AttachAsync(new FakeRx("rsp1a-1", "SDRplay RSP1A"));
        aux.Role.Should().Be(DeviceRole.Auxiliary);
        session.Primary.Should().BeNull();
    }

    // ── WDSP kanaal-isolatie ──────────────────────────────────────────────────

    [Fact]
    public async Task PrimaryAndAux_NonOverlappingWdspChannels()
    {
        await using var session = MakeSession();
        var p = await session.AttachAsync(new FakeTrx("brick2-1", "Brick2"));
        var a = await session.AttachAsync(new FakeRx("rsp1a-1",  "SDRplay"));

        p.WdspChannelId.Should().BeInRange(0, 13);
        a.WdspChannelId.Should().BeInRange(16, 29);
    }

    [Fact]
    public async Task FourDevices_AllGetUniqueChannels()
    {
        await using var session = MakeSession();
        await session.AttachAsync(new FakeTrx("d1", "Brick2"));
        await session.AttachAsync(new FakeRx("d2",  "SDRplay"));
        await session.AttachAsync(new FakeTrx("d3",  "PlutoSDR"));
        await session.AttachAsync(new FakeRx("d4",  "RTL-SDR"));

        var channels = session.Devices.Select(d => d.WdspChannelId).ToList();
        channels.Distinct().Should().HaveCount(channels.Count, "elk device krijgt een uniek kanaal");
    }

    // ── PTT Lockout ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MoxOn_BlocksAuxTransceivers()
    {
        await using var session = MakeSession();
        var brick2 = new FakeTrx("brick2-1", "Brick2");
        var pluto  = new FakeTrx("pluto-1",  "PlutoSDR");

        await session.AttachAsync(brick2);
        await session.AttachAsync(pluto);
        await session.SetMoxAsync(true);

        brick2.MoxState.Should().BeTrue("primary TX moet actief zijn");
        pluto.MoxState.Should().BeFalse("aux TX moet geblokkeerd zijn");
    }

    [Fact]
    public async Task MoxOff_AllowsAuxTx()
    {
        await using var session = MakeSession();
        var brick2 = new FakeTrx("brick2-1", "Brick2");
        var pluto  = new FakeTrx("pluto-1",  "PlutoSDR");

        await session.AttachAsync(brick2);
        await session.AttachAsync(pluto);

        await session.SetMoxAsync(true);
        await session.SetMoxAsync(false); // PTT los

        brick2.MoxState.Should().BeFalse();
    }

    [Fact]
    public async Task TwoOpenHpsdrDevices_MoxLockout_WorksCorrectly()
    {
        // Specifieke use case: Brick2 primary + HL2 als RX-aux — HL2 TX geblokkeerd
        await using var session = MakeSession();
        var brick2 = new FakeTrx("brick2-1", "Brick2 P2");
        var hl2    = new FakeTrx("hl2-1",    "Hermes-Lite 2");

        await session.AttachAsync(brick2);
        await session.AttachAsync(hl2);
        await session.SetMoxAsync(true);

        hl2.MoxState.Should().BeFalse("HL2 als aux moet geblokkeerd zijn tijdens Brick2 TX");
    }

    [Fact]
    public async Task AllowSimultaneousTx_BypassesLockout()
    {
        await using var session = MakeSession();
        var brick2 = new FakeTrx("brick2-1", "Brick2");
        var pluto  = new FakeTrx("pluto-1",  "PlutoSDR");

        await session.AttachAsync(brick2);
        var aux = await session.AttachAsync(pluto);
        aux.AllowSimultaneousTx = true;

        await pluto.SetMoxAsync(true); // Pluto al in TX
        await session.SetMoxAsync(true);

        pluto.MoxState.Should().BeTrue("AllowSimultaneousTx omzeilt lockout");
    }

    // ── Frequentiesynchronisatie ──────────────────────────────────────────────

    [Fact]
    public async Task FollowPrimary_PropagatesFreq()
    {
        await using var session = MakeSession();
        var rsp = new FakeRx("rsp1a-1", "SDRplay RSP1A");

        await session.AttachAsync(new FakeTrx("brick2-1", "Brick2"));
        var aux = await session.AttachAsync(rsp);
        aux.FreqSync = FreqSyncPolicy.FollowPrimary;

        await session.SetPrimaryFrequencyAsync(14_200_000);

        rsp.CurrentFreqHz.Should().Be(14_200_000);
    }

    [Fact]
    public async Task FollowPrimaryWithOffset_AppliesOffset()
    {
        await using var session = MakeSession();
        var pluto = new FakeRx("pluto-1", "PlutoSDR");

        await session.AttachAsync(new FakeTrx("brick2-1", "Brick2"));
        var aux = await session.AttachAsync(pluto);
        aux.FreqSync         = FreqSyncPolicy.FollowPrimaryWithOffset;
        aux.FreqSyncOffsetHz = -600_000; // transverter-offset

        await session.SetPrimaryFrequencyAsync(144_200_000);

        pluto.CurrentFreqHz.Should().Be(143_600_000);
    }

    [Fact]
    public async Task Independent_DoesNotPropagate()
    {
        await using var session = MakeSession();
        var rsp = new FakeRx("rsp1a-1", "SDRplay") { CurrentFreqHz = 7_074_000 };

        await session.AttachAsync(new FakeTrx("brick2-1", "Brick2"));
        var aux = await session.AttachAsync(rsp);
        aux.FreqSync = FreqSyncPolicy.Independent;

        await session.SetPrimaryFrequencyAsync(14_200_000);

        rsp.CurrentFreqHz.Should().Be(7_074_000, "Independent mag niet propageren");
    }

    // ── Detach & cleanup ──────────────────────────────────────────────────────

    [Fact]
    public async Task Detach_ReleasesWdspChannel()
    {
        var alloc = new WdspChannelAllocator();
        await using var session = new RadioSession(alloc, NullLogger<RadioSession>.Instance);

        await session.AttachAsync(new FakeTrx("brick2-1", "Brick2"));
        var aux   = await session.AttachAsync(new FakeRx("rsp1a-1", "SDRplay"));
        var chId  = aux.WdspChannelId;

        await session.DetachAsync("rsp1a-1");

        session.Auxiliaries.Should().BeEmpty();
        alloc.IsAllocated(chId).Should().BeFalse("kanaal vrijgegeven na detach");
    }
}

// ── Test-stubs ────────────────────────────────────────────────────────────────

file sealed class FakeRx(string id, string name) : IDeviceSource
{
    public string DeviceId => id;
    public string FriendlyName => name;
    public DeviceCapabilities Capabilities => DeviceCapabilities.Receive;
    public FrequencyRange[] SupportedRanges => [new(0, 2_000_000_000)];
    public int[] SupportedSampleRates => [48_000];
    public long CurrentFreqHz { get; set; }

    public Task<bool> OpenAsync(DeviceOpenOptions o, CancellationToken ct) => Task.FromResult(true);
    public IAsyncEnumerable<IqBlock> StreamAsync(CancellationToken ct) =>
        AsyncEnumerable.Empty<IqBlock>();
    public Task SetFrequencyAsync(long hz, CancellationToken ct) {
        CurrentFreqHz = hz; return Task.CompletedTask; }
    public Task SetGainAsync(double db, CancellationToken ct) => Task.CompletedTask;
    public Task SetSampleRateAsync(int hz, CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

file sealed class FakeTrx(string id, string name) : ITransceiver
{
    public string DeviceId => id;
    public string FriendlyName => name;
    public DeviceCapabilities Capabilities =>
        DeviceCapabilities.Receive | DeviceCapabilities.Transmit | DeviceCapabilities.FullDuplex;
    public FrequencyRange[] SupportedRanges => [new(0, 2_000_000_000)];
    public int[] SupportedSampleRates => [48_000];
    public bool MoxState { get; private set; }
    public long CurrentFreqHz { get; set; }

    public Task<bool> OpenAsync(DeviceOpenOptions o, CancellationToken ct) => Task.FromResult(true);
    public IAsyncEnumerable<IqBlock> StreamAsync(CancellationToken ct) =>
        AsyncEnumerable.Empty<IqBlock>();
    public Task SetFrequencyAsync(long hz, CancellationToken ct) {
        CurrentFreqHz = hz; return Task.CompletedTask; }
    public Task SetGainAsync(double db, CancellationToken ct) => Task.CompletedTask;
    public Task SetSampleRateAsync(int hz, CancellationToken ct) => Task.CompletedTask;
    public Task SetMoxAsync(bool v, CancellationToken ct) { MoxState = v; return Task.CompletedTask; }
    public Task SetTxFrequencyAsync(long hz, CancellationToken ct) => Task.CompletedTask;
    public IAsyncEnumerable<TxFeedbackBlock> TxFeedbackAsync(CancellationToken ct) =>
        AsyncEnumerable.Empty<TxFeedbackBlock>();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
