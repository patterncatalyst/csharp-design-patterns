namespace FreightRating.OverAbstracted;

/// <summary>
/// Applies the fuel levy, the one component that genuinely depends on its
/// position in the tower: it taxes whatever subtotal the components before
/// it already produced.
/// </summary>
public sealed class FuelLevyComponent : IRateComponent
{
    public decimal Apply(RateRequest request, decimal runningSubtotal) =>
        request.FuelLevyApplies ? runningSubtotal * 0.08m : 0m;
}
