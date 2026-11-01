namespace ShipmentWorkflow;

/// <summary>
/// The rated amount for one consignment, produced by an
/// <see cref="IRatingStrategy"/> and consumed by <see cref="InvoiceStage"/>
/// to build the final <see cref="Invoice"/>.
/// </summary>
public sealed record RateQuote(string ConsignmentId, decimal Amount);
