namespace CustomsDocumentation;

/// <summary>
/// German customs rules: duty is 19% of declared value in euros (the
/// standard import VAT rate), and a shipment over 1000 kg needs a licensed
/// customs broker on file.
/// </summary>
public sealed class GermanyDeclarationBuilder : CustomsDeclarationBuilder
{
    private const decimal DutyRate = 0.19m;
    private const decimal BrokerRequiredWeightKilograms = 1000m;

    protected override IReadOnlyList<string> CollectRequiredFields(Shipment shipment) =>
        ["EORI Number", "Commodity Code", "Incoterms"];

    protected override Money CalculateDuty(Shipment shipment) =>
        new(Math.Round(shipment.DeclaredValue * DutyRate, 2, MidpointRounding.AwayFromZero), "EUR");

    protected override IReadOnlyList<string> Validate(Shipment shipment)
    {
        var messages = new List<string>();

        if (shipment.WeightKilograms > BrokerRequiredWeightKilograms)
        {
            messages.Add("Shipments over 1000 kg require a customs broker");
        }

        if (shipment.LineItems.Count == 0)
        {
            messages.Add("At least one line item is required");
        }

        return messages;
    }
}
