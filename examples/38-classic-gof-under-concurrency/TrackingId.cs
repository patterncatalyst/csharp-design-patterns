namespace ConcurrentHandling;

/// <summary>
/// Identifies the consignment a handling event belongs to. A readonly record
/// struct copies by value, so every thread that reads a <see cref="TrackingId"/>
/// holds its own independent copy.
/// </summary>
public readonly record struct TrackingId(string Value);
