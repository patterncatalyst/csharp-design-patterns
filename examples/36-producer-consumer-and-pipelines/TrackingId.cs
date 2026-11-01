namespace HandlingEventIngest;

/// <summary>
/// The tracking identifier a handling event is reported against, for example
/// a bill-of-lading or consignment code. Wrapped in a record struct so the
/// pipeline never confuses a tracking identifier with any other plain
/// <see cref="string"/> flowing through it.
/// </summary>
public readonly record struct TrackingId(string Value);
