namespace RateEngineCatalogues;

/// <summary>
/// The <see cref="ServiceLevel.Standard"/> surcharge: no markup over the base
/// subtotal.
/// </summary>
public sealed class StandardSurcharge : IServiceSurcharge
{
    public decimal Apply(decimal subtotal) => subtotal;
}
