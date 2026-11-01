namespace ShipmentWorkflow;

/// <summary>
/// The billing document for one consignment: its line items and their total,
/// built by <see cref="InvoiceBuilder"/> from the <see cref="RateQuote"/>
/// produced earlier in the <see cref="ShipmentPipeline"/>.
/// </summary>
public sealed record Invoice(string ConsignmentId, IReadOnlyList<LineItem> LineItems, decimal Total);
