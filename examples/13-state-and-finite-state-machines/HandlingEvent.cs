namespace ConsignmentTracking;

/// <summary>
/// A single carrier report against a consignment: what happened, when, and
/// where. <see cref="ConsignmentStateMachine.Apply"/> consumes one of these
/// per state transition.
/// </summary>
public sealed record HandlingEvent(HandlingEventType Type, DateTimeOffset OccurredAt, string Location);
