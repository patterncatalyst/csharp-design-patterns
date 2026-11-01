namespace RateEngineSeams;

/// <summary>
/// The production <see cref="IClock"/>: the real wall clock, wrapped behind
/// the seam so a test can swap in a fixed one instead.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
