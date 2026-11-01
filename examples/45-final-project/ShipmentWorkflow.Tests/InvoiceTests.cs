using Xunit;

namespace ShipmentWorkflow.Tests;

/// <summary>
/// The invoice Builder, directly and as driven by <see cref="InvoiceStage"/>:
/// a standard booking produces one freight line, an express booking produces
/// a freight line plus a separate surcharge line, and the total always
/// matches the quoted amount.
/// </summary>
public class InvoiceTests
{
    private static readonly BookingRequest ExpressBooking = new(
        "CN-7002", "USLAX", "SGSIN", DeclaredValue: 10_000.00m, WeightKilograms: 500.00m, Service: ServiceLevel.Express);

    [Fact]
    public void Invoice_builder_sums_line_items_into_the_total()
    {
        var invoice = new InvoiceBuilder()
            .AddLineItem("Freight", 100.00m)
            .AddLineItem("Handling", 25.00m)
            .Build("CN-9001");

        Assert.Equal(2, invoice.LineItems.Count);
        Assert.Equal(125.00m, invoice.Total);
    }

    [Fact]
    public void Express_invoice_itemizes_freight_and_the_service_surcharge()
    {
        var pipeline = ShipmentPipelineFactory.CreateDefault();

        var result = pipeline.Run(ExpressBooking);

        Assert.NotNull(result.Invoice);
        Assert.NotNull(result.Quote);
        var invoice = result.Invoice!;

        Assert.Equal(2, invoice.LineItems.Count);
        Assert.Contains(invoice.LineItems, item => item.Description == "Freight");
        Assert.Contains(invoice.LineItems, item => item.Description == "Express service surcharge");
        Assert.Equal(invoice.LineItems.Sum(item => item.Amount), invoice.Total);
        Assert.Equal(result.Quote!.Amount, invoice.Total);
    }
}
