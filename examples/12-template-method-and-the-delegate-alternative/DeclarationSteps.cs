namespace CustomsDocumentation;

/// <summary>
/// The modern delegate-record alternative to a protected abstract step. A
/// shipment is no longer bound to a subclass hierarchy; it is paired with a
/// value holding the three per-country functions the skeleton in
/// <see cref="DeclarationEngine"/> calls in a fixed order. Swapping rules
/// means swapping a value, not writing a new type.
/// </summary>
public sealed record DeclarationSteps(
    Func<Shipment, IReadOnlyList<string>> CollectRequiredFields,
    Func<Shipment, Money> CalculateDuty,
    Func<Shipment, IReadOnlyList<string>> Validate);
