namespace CustomsDocumentation;

/// <summary>
/// Classic Template Method. <see cref="Build"/> is the template: it is a
/// plain, non-virtual public method, so a derived class cannot override it
/// and cannot change the order the three steps run in. A derived class
/// supplies only the per-country rules by overriding the three protected
/// steps.
/// </summary>
public abstract class CustomsDeclarationBuilder
{
    // Protected, not public: an abstract class has no business being
    // constructed directly (CA1012), only through a concrete subclass.
    protected CustomsDeclarationBuilder()
    {
    }

    /// <summary>
    /// The template method. The step order is fixed here, once, for every
    /// country: collect the required fields, calculate duty, then validate.
    /// Nothing about this method is virtual, so no subclass can reorder or
    /// skip a step; a subclass can only change what each step does.
    /// </summary>
    public CustomsDeclaration Build(Shipment shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        var requiredFields = CollectRequiredFields(shipment);
        var duty = CalculateDuty(shipment);
        var validationMessages = Validate(shipment);

        return new CustomsDeclaration(shipment.DestinationCountry, requiredFields, duty, validationMessages);
    }

    protected abstract IReadOnlyList<string> CollectRequiredFields(Shipment shipment);

    protected abstract Money CalculateDuty(Shipment shipment);

    protected abstract IReadOnlyList<string> Validate(Shipment shipment);
}
