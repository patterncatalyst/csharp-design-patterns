namespace CustomsDocumentation;

/// <summary>
/// United States customs rules: duty is a flat 2.5% of declared value in
/// US dollars, formal entry paperwork is required above 2500.00 USD, and
/// every line must carry at least one item.
/// </summary>
public sealed class UnitedStatesDeclarationBuilder : CustomsDeclarationBuilder
{
    private const decimal DutyRate = 0.025m;
    private const decimal FormalEntryThreshold = 2500m;

    protected override IReadOnlyList<string> CollectRequiredFields(Shipment shipment) =>
        ["Importer of Record", "HTS Code", "Country of Origin"];

    protected override Money CalculateDuty(Shipment shipment) =>
        new(Math.Round(shipment.DeclaredValue * DutyRate, 2, MidpointRounding.AwayFromZero), "USD");

    protected override IReadOnlyList<string> Validate(Shipment shipment)
    {
        var messages = new List<string>();

        if (shipment.DeclaredValue > FormalEntryThreshold)
        {
            messages.Add("Formal entry required for shipments over 2500.00 USD");
        }

        if (shipment.LineItems.Count == 0)
        {
            messages.Add("At least one line item is required");
        }

        return messages;
    }
}
