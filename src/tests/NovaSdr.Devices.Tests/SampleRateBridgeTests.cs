using FluentAssertions;
using NovaSdr.Devices.Resampling;

namespace NovaSdr.Devices.Tests;

public sealed class SampleRateBridgeTests
{
    [Theory]
    [InlineData(2_000_000,  48_000, 41)]
    [InlineData(2_500_000,  48_000, 52)]
    [InlineData(  250_000,  48_000,  5)]
    [InlineData(   96_000,  48_000,  2)]
    [InlineData(   48_000,  48_000,  1)]
    public void DecimationFactor_IsCalculatedCorrectly(int inputRate, int outputRate, int expectedFactor)
    {
        var bridge = new SampleRateBridge(inputRate, outputRate);
        bridge.DecimationFactor.Should().Be(expectedFactor);
    }

    [Fact]
    public void Process_At48k_ReturnsInputUnchanged()
    {
        var bridge = new SampleRateBridge(48_000, 48_000);
        var input = MakeBlock(48_000, 1024);

        var output = bridge.Process(input);

        output.Should().NotBeNull();
        output!.SampleRateHz.Should().Be(48_000);
        output.SampleCount.Should().Be(1024);
    }

    [Fact]
    public void Process_DecimatesTo48k()
    {
        // 2.5 MHz decimeren naar 48 kHz
        var bridge = new SampleRateBridge(2_500_000, 48_000);
        var input = MakeBlock(2_500_000, 2048);

        var output = bridge.Process(input);

        output.Should().NotBeNull();
        output!.SampleRateHz.Should().Be(48_000);
        output.SampleCount.Should().BeGreaterThan(0);
        output.SampleCount.Should().BeLessThan(input.SampleCount);
    }

    [Fact]
    public void Process_PreservesCenterFrequency()
    {
        var bridge = new SampleRateBridge(2_000_000, 48_000);
        var input = MakeBlock(2_000_000, 2048, centerHz: 145_000_000);

        var output = bridge.Process(input);

        output!.CenterFrequencyHz.Should().Be(145_000_000);
    }

    [Fact]
    public void Process_OutputSamplesAreNormalized()
    {
        var bridge = new SampleRateBridge(2_000_000, 48_000);
        var input = MakeBlock(2_000_000, 4096);

        var output = bridge.Process(input);

        // Geen samples buiten [-2, 2] na FIR filtering
        foreach (var s in output!.Samples)
            s.Should().BeInRange(-2.0, 2.0);
    }

    private static IqBlock MakeBlock(int rate, int sampleCount, long centerHz = 14_200_000)
    {
        var rng = new Random(42);
        var samples = new double[sampleCount * 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = rng.NextDouble() * 2 - 1; // [-1, 1]

        return new IqBlock(samples, rate, centerHz, DateTimeOffset.UtcNow);
    }
}
