using NSubstitute;
using Xunit;

namespace ConsignmentTracking.Tests;

/// <summary>
/// Proves the probabilistic variant two ways: against a real
/// <see cref="SeededRandomSource"/>, the same seed replays the identical
/// status sequence and a different seed can diverge; against a substitute
/// <see cref="IRandomSource"/>, a scripted draw deterministically picks
/// <see cref="ConsignmentStatus.Delayed"/> or <see cref="ConsignmentStatus.Cleared"/>.
/// </summary>
public class ProbabilisticTransitMachineTests
{
    [Fact]
    public void The_same_seed_produces_an_identical_status_sequence()
    {
        var first = new ProbabilisticTransitMachine(new SeededRandomSource(42), 0.5);
        var second = new ProbabilisticTransitMachine(new SeededRandomSource(42), 0.5);

        var firstPath = first.Simulate(20);
        var secondPath = second.Simulate(20);

        Assert.Equal(firstPath, secondPath);
    }

    [Fact]
    public void A_different_seed_can_produce_a_different_status_sequence()
    {
        var first = new ProbabilisticTransitMachine(new SeededRandomSource(42), 0.5);
        var second = new ProbabilisticTransitMachine(new SeededRandomSource(7), 0.5);

        var firstPath = first.Simulate(20);
        var secondPath = second.Simulate(20);

        Assert.NotEqual(firstPath, secondPath);
    }

    [Fact]
    public void A_draw_below_the_delay_probability_transitions_to_Delayed()
    {
        var source = Substitute.For<IRandomSource>();
        source.NextDouble().Returns(0.1);
        var machine = new ProbabilisticTransitMachine(source, 0.5);

        var next = machine.Step(ConsignmentStatus.InTransit);

        Assert.Equal(ConsignmentStatus.Delayed, next);
    }

    [Fact]
    public void A_draw_at_or_above_the_delay_probability_transitions_to_Cleared()
    {
        var source = Substitute.For<IRandomSource>();
        source.NextDouble().Returns(0.5);
        var machine = new ProbabilisticTransitMachine(source, 0.5);

        var next = machine.Step(ConsignmentStatus.InTransit);

        Assert.Equal(ConsignmentStatus.Cleared, next);
    }
}
