namespace RateEngineSeams.Tests;

/// <summary>
/// An <see cref="IClock"/> test double that always reports the
/// <see cref="DateTimeOffset"/> given to its constructor, the constructor
/// seam the legacy <c>RateEngine</c> has no way to accept.
/// </summary>
public sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow => utcNow;
}
