namespace CustomsDocumentation;

/// <summary>
/// The delegate-form counterpart to the three
/// <see cref="CustomsDeclarationBuilder"/> subclasses. Each named value
/// reproduces that subclass's required fields, duty formula, and validation
/// rule exactly, so running a shipment through either form for the same
/// country produces an equal <see cref="CustomsDeclaration"/>.
/// </summary>
public static class CountrySteps
{
    private const decimal UnitedStatesDutyRate = 0.025m;
    private const decimal UnitedStatesFormalEntryThreshold = 2500m;

    private const decimal GermanyDutyRate = 0.19m;
    private const decimal GermanyBrokerRequiredWeightKilograms = 1000m;

    private const decimal JapanDutyRate = 0.05m;
    private const decimal JapanHandlingFee = 2000m;
    private const decimal JapanInspectionThreshold = 200000m;

    public static DeclarationSteps UnitedStates { get; } = new(
        CollectRequiredFields: _ => ["Importer of Record", "HTS Code", "Country of Origin"],
        CalculateDuty: shipment =>
            new Money(Math.Round(shipment.DeclaredValue * UnitedStatesDutyRate, 2, MidpointRounding.AwayFromZero), "USD"),
        Validate: shipment =>
        {
            var messages = new List<string>();

            if (shipment.DeclaredValue > UnitedStatesFormalEntryThreshold)
            {
                messages.Add("Formal entry required for shipments over 2500.00 USD");
            }

            if (shipment.LineItems.Count == 0)
            {
                messages.Add("At least one line item is required");
            }

            return messages;
        });

    public static DeclarationSteps Germany { get; } = new(
        CollectRequiredFields: _ => ["EORI Number", "Commodity Code", "Incoterms"],
        CalculateDuty: shipment =>
            new Money(Math.Round(shipment.DeclaredValue * GermanyDutyRate, 2, MidpointRounding.AwayFromZero), "EUR"),
        Validate: shipment =>
        {
            var messages = new List<string>();

            if (shipment.WeightKilograms > GermanyBrokerRequiredWeightKilograms)
            {
                messages.Add("Shipments over 1000 kg require a customs broker");
            }

            if (shipment.LineItems.Count == 0)
            {
                messages.Add("At least one line item is required");
            }

            return messages;
        });

    public static DeclarationSteps Japan { get; } = new(
        CollectRequiredFields: _ => ["Importer Tax ID", "HS Code", "Certificate of Origin"],
        CalculateDuty: shipment =>
            new Money(Math.Round(shipment.DeclaredValue * JapanDutyRate, 2, MidpointRounding.AwayFromZero) + JapanHandlingFee, "JPY"),
        Validate: shipment =>
        {
            var messages = new List<string>();

            if (shipment.DeclaredValue > JapanInspectionThreshold)
            {
                messages.Add("Import declaration requires customs inspection over 200000 JPY");
            }

            if (shipment.LineItems.Count == 0)
            {
                messages.Add("At least one line item is required");
            }

            return messages;
        });
}
