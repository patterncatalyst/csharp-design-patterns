namespace RateEngineCatalogues;

/// <summary>
/// The <see cref="ServiceLevel.Express"/> surcharge: a 25% markup over the
/// base subtotal, the same multiplier the legacy <c>"EXPRESS"</c> case
/// applies inline.
/// </summary>
public sealed class ExpressSurcharge : IServiceSurcharge
{
    public decimal Apply(decimal subtotal) => subtotal * 1.25m;
}
