namespace ConsignmentTracking;

/// <summary>
/// A second State-pattern machine over the same <see cref="ConsignmentStatus"/>
/// states, but one state's exit is probabilistic instead of event-driven: from
/// <see cref="ConsignmentStatus.InTransit"/>, a draw from <see cref="IRandomSource"/>
/// decides whether the leg clears or is delayed, instead of a carrier event
/// deciding it. The surrounding states still progress deterministically, the
/// same shape <see cref="Transitions"/> uses for the event-driven machine.
/// </summary>
public sealed class ProbabilisticTransitMachine
{
    private readonly IRandomSource _source;
    private readonly double _delayProbability;

    public ProbabilisticTransitMachine(IRandomSource source, double delayProbability)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (delayProbability is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayProbability), delayProbability, "A probability is between 0.0 and 1.0.");
        }

        _source = source;
        _delayProbability = delayProbability;
    }

    /// <summary>
    /// Advances one step from <paramref name="current"/>. Every state but
    /// <see cref="ConsignmentStatus.InTransit"/> progresses deterministically;
    /// <see cref="ConsignmentStatus.Delivered"/> is terminal and stays put.
    /// From <see cref="ConsignmentStatus.InTransit"/>, a draw below the
    /// configured delay probability goes to <see cref="ConsignmentStatus.Delayed"/>,
    /// otherwise the leg clears. A delayed consignment retries transit on its
    /// next step rather than being stuck.
    /// </summary>
    public ConsignmentStatus Step(ConsignmentStatus current) => current switch
    {
        ConsignmentStatus.Booked => ConsignmentStatus.Loaded,
        ConsignmentStatus.Loaded => ConsignmentStatus.InTransit,
        ConsignmentStatus.InTransit => _source.NextDouble() < _delayProbability
            ? ConsignmentStatus.Delayed
            : ConsignmentStatus.Cleared,
        ConsignmentStatus.Delayed => ConsignmentStatus.InTransit,
        ConsignmentStatus.Cleared => ConsignmentStatus.Delivered,
        ConsignmentStatus.Delivered => ConsignmentStatus.Delivered,
        _ => throw new ArgumentOutOfRangeException(nameof(current), current, "Unknown consignment status."),
    };

    /// <summary>
    /// Runs <paramref name="steps"/> steps from <see cref="ConsignmentStatus.Booked"/>,
    /// returning the full path including the starting status. Two machines
    /// built on <see cref="SeededRandomSource"/>s with the same seed produce
    /// the identical path.
    /// </summary>
    public IReadOnlyList<ConsignmentStatus> Simulate(int steps)
    {
        if (steps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), steps, "Step count cannot be negative.");
        }

        var path = new List<ConsignmentStatus>(steps + 1) { ConsignmentStatus.Booked };
        var current = ConsignmentStatus.Booked;
        for (var i = 0; i < steps; i++)
        {
            current = Step(current);
            path.Add(current);
        }

        return path;
    }
}
