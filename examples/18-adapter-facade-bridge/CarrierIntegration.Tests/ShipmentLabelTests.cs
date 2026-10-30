using Xunit;

namespace CarrierIntegration.Tests;

/// <summary>
/// Proves the Bridge: every combination of a label kind (Standard, Return)
/// and a renderer (PDF, ZPL) produces a correctly rendered label, with
/// neither hierarchy needing to know about the other's concrete types.
/// </summary>
public class ShipmentLabelTests
{
    private static readonly ShipmentRequest Request = new("SEA", "NRT", new Kilograms(3m));
    private static readonly CarrierQuote Quote = new("SEABRIGHT", new Money(4200), 7);

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Label_renders_correctly_for_every_kind_and_renderer_combination(bool standard, bool pdf)
    {
        ILabelRenderer renderer = pdf ? new PdfLabelRenderer() : new ZplLabelRenderer();
        ShipmentLabel label = standard
            ? new StandardShipmentLabel(renderer, Quote, Request)
            : new ReturnShipmentLabel(renderer, Quote, Request);

        string rendered = label.Render();

        string expectedTitle = standard ? "Standard Shipment" : "Return Shipment";
        string expectedDestination = standard ? Request.Destination : Request.Origin;
        Assert.Contains(expectedTitle, rendered, StringComparison.Ordinal);
        Assert.Contains(Quote.CarrierCode, rendered, StringComparison.Ordinal);
        Assert.Contains(expectedDestination, rendered, StringComparison.Ordinal);
        Assert.Equal(pdf, rendered.StartsWith("%PDF-LABEL%", StringComparison.Ordinal));
        Assert.Equal(!pdf, rendered.StartsWith("^XA", StringComparison.Ordinal));
    }
}
