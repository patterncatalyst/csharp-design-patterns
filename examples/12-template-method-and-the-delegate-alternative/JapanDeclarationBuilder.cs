namespace CustomsDocumentation;

/// <summary>
/// Japanese customs rules: duty is 5% of declared value plus a flat
/// 2000 JPY handling fee, and a declared value above 200000 JPY triggers a
/// mandatory customs inspection.
/// </summary>
public sealed class JapanDeclarationBuilder : CustomsDeclarationBuilder
{
    private const decimal DutyRate = 0.05m;
    private const decimal HandlingFee = 2000m;
    private const decimal InspectionThreshold = 200000m;

    protected override IReadOnlyList<string> CollectRequiredFields(Shipment shipment) =>
        ["Importer Tax ID", "HS Code", "Certificate of Origin"];

    protected override Money CalculateDuty(Shipment shipment) =>
        new(Math.Round(shipment.DeclaredValue * DutyRate, 2, MidpointRounding.AwayFromZero) + HandlingFee, "JPY");

    protected override IReadOnlyList<string> Validate(Shipment shipment)
    {
        var messages = new List<string>();

        if (shipment.DeclaredValue > InspectionThreshold)
        {
            messages.Add("Import declaration requires customs inspection over 200000 JPY");
        }

        if (shipment.LineItems.Count == 0)
        {
            messages.Add("At least one line item is required");
        }

        return messages;
    }
}
