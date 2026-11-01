namespace ShipmentWorkflow;

/// <summary>
/// One billable line on an <see cref="Invoice"/>: a description and its
/// amount. Accumulated by <see cref="InvoiceBuilder"/>.
/// </summary>
public sealed record LineItem(string Description, decimal Amount);
