namespace FreightRating.OverAbstracted;

/// <summary>
/// Rates the consignment's weight. Independent of the base rate component,
/// so it also ignores the running subtotal.
/// </summary>
public sealed class WeightSurchargeComponent : IRateComponent
{
    public decimal Apply(RateRequest request, decimal runningSubtotal) => request.WeightKilograms * 2.50m;
}
