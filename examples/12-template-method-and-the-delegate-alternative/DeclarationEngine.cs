namespace CustomsDocumentation;

/// <summary>
/// The delegate-based skeleton. <see cref="Build"/> runs the same three
/// steps, in the same fixed order, as <see cref="CustomsDeclarationBuilder.Build"/>,
/// but reads them from a <see cref="DeclarationSteps"/> value instead of
/// dispatching through a subclass's overrides.
/// </summary>
public static class DeclarationEngine
{
    public static CustomsDeclaration Build(Shipment shipment, DeclarationSteps steps)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        ArgumentNullException.ThrowIfNull(steps);

        var requiredFields = steps.CollectRequiredFields(shipment);
        var duty = steps.CalculateDuty(shipment);
        var validationMessages = steps.Validate(shipment);

        return new CustomsDeclaration(shipment.DestinationCountry, requiredFields, duty, validationMessages);
    }
}
