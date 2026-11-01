namespace RateEngineDependencies;

/// <summary>
/// Validates a lane code against the lanes <see cref="RateEngine"/> knows how
/// to quote. Internal, not a public seam: <c>InternalsVisibleTo</c> lets the
/// test project reach it directly (Test 7) without widening the engine's own
/// public surface.
/// </summary>
internal static class LaneValidation
{
    internal static bool IsKnownLane(string laneCode) => laneCode is "ROT-NYC" or "SIN-LAX" or "HKG-FRA";
}
