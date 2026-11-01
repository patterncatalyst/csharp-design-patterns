namespace ShipmentWorkflow;

/// <summary>
/// Accumulates <see cref="LineItem"/> entries and builds the final
/// <see cref="Invoice"/> with their total. Used directly by
/// <see cref="InvoiceStage"/> to itemize freight and, for an express
/// booking, the service surcharge.
/// </summary>
public sealed class InvoiceBuilder
{
    private readonly List<LineItem> _lineItems = [];

    public InvoiceBuilder AddLineItem(string description, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(description);
        _lineItems.Add(new LineItem(description, amount));
        return this;
    }

    public Invoice Build(string consignmentId)
    {
        ArgumentNullException.ThrowIfNull(consignmentId);

        var lineItems = _lineItems.ToArray();
        return new Invoice(consignmentId, lineItems, lineItems.Sum(item => item.Amount));
    }
}
