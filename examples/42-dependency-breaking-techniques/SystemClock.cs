namespace RateEngineDependencies;

/// <summary>
/// The production <see cref="IClock"/>, backed by the real system clock.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
