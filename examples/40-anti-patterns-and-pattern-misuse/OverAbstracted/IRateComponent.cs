namespace FreightRating.OverAbstracted;

/// <summary>
/// One line item in a freight rate quote. Each component sees the request
/// and the subtotal accumulated by the components that ran before it, and
/// returns its own contribution.
/// </summary>
public interface IRateComponent
{
    decimal Apply(RateRequest request, decimal runningSubtotal);
}
