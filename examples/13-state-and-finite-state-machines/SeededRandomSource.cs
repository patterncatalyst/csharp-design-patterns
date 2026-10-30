namespace ConsignmentTracking;

/// <summary>
/// An <see cref="IRandomSource"/> backed by a seeded <see cref="Random"/>, so a
/// <see cref="ProbabilisticTransitMachine"/> built on the same seed replays the
/// identical sequence of draws, and therefore the identical status sequence,
/// every time.
/// </summary>
public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private readonly Random _random = new(seed);

    public double NextDouble()
    {
        // CA5394 flags System.Random as an insecure randomness source. This
        // class drives a transit-delay SIMULATION, not a security decision
        // (no token, key, or password derives from it), so the analyzer's
        // concern does not apply here. Suppressed locally, not project-wide.
#pragma warning disable CA5394
        return _random.NextDouble();
#pragma warning restore CA5394
    }
}
