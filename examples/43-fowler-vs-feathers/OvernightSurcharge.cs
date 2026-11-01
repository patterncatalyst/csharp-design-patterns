namespace RateEngineCatalogues;

/// <summary>
/// The <see cref="ServiceLevel.Overnight"/> surcharge: a 50% markup over the
/// base subtotal, the same multiplier the legacy <c>"OVERNIGHT"</c> case
/// applies inline.
/// </summary>
public sealed class OvernightSurcharge : IServiceSurcharge
{
    public decimal Apply(decimal subtotal) => subtotal * 1.5m;
}
