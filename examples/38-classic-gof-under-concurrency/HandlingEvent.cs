namespace ConcurrentHandling;

/// <summary>
/// A single handling step recorded against a consignment. Records are
/// immutable by default, so once constructed a <see cref="HandlingEvent"/> can
/// be read from any number of threads without a lock; a thread that wants a
/// changed copy uses a non-destructive <c>with</c> expression rather than
/// mutating the shared instance.
/// </summary>
public sealed record HandlingEvent(TrackingId Tracking, HandlingKind Kind, int Sequence);
