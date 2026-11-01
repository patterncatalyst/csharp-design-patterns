namespace RateEngineCatalogues;

/// <summary>
/// The Fowler stage's extracted service-surcharge strategy: the inline string
/// <c>switch</c> both the legacy and the Feathers-seamed engines carry is
/// replaced here with a small polymorphic hierarchy, one implementation per
/// <see cref="ServiceLevel"/>.
/// </summary>
public interface IServiceSurcharge
{
    decimal Apply(decimal subtotal);
}
