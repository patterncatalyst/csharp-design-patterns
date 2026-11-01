namespace FreightRating.OverAbstracted;

/// <summary>
/// Rates the consignment's declared value. The first component in the
/// tower, so it ignores the running subtotal.
/// </summary>
public sealed class BaseRateComponent : IRateComponent
{
    public decimal Apply(RateRequest request, decimal runningSubtotal) => request.DeclaredValue * 0.015m;
}
