namespace ShipmentWorkflow;

/// <summary>
/// The state threaded through the <see cref="ShipmentPipeline"/>: the
/// original booking plus whatever each <see cref="IShipmentStage"/> has
/// produced so far. Immutable; each stage returns a new context via
/// <c>with</c> rather than mutating this one.
/// </summary>
public sealed record ShipmentContext(
    BookingRequest Booking,
    RateQuote? Quote = null,
    StowageAssignment? Stowage = null,
    ClearanceResult? Clearance = null,
    Invoice? Invoice = null);
