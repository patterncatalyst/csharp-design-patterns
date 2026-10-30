using System.Collections.Frozen;

namespace ConsignmentRouting;

/// <summary>
/// Interns one <see cref="CommodityCode"/> instance per Harmonized System
/// code, the second instance of the same Flyweight shape as
/// <see cref="PortCodeRegistry"/>: a small, fixed seed set built once into a
/// <see cref="FrozenDictionary{TKey,TValue}"/>, shared instances on every
/// lookup.
/// </summary>
public sealed class CommodityCodeRegistry
{
    private static readonly CommodityCode[] SeedCommodities =
    [
        new CommodityCode("0901", "Coffee, whether or not roasted or decaffeinated"),
        new CommodityCode("6109", "T-shirts, singlets and other vests, knitted or crocheted"),
        new CommodityCode("8471", "Automatic data processing machines and units thereof"),
        new CommodityCode("2709", "Petroleum oils and oils obtained from bituminous minerals, crude"),
    ];

    private readonly FrozenDictionary<string, CommodityCode> _byCode =
        SeedCommodities.ToFrozenDictionary(commodity => commodity.Code);

    /// <summary>
    /// Returns the shared <see cref="CommodityCode"/> instance for
    /// <paramref name="code"/>, or throws <see cref="UnknownCommodityCodeException"/>
    /// if the registry never interned it.
    /// </summary>
    public CommodityCode Get(string code) => _byCode.TryGetValue(code, out var commodity)
        ? commodity
        : throw new UnknownCommodityCodeException($"No commodity code is registered for '{code}'.");

    /// <summary>
    /// The non-throwing counterpart to <see cref="Get"/>, for a caller that
    /// expects an unknown code to be a routine case rather than a fault.
    /// </summary>
    public bool TryGet(string code, out CommodityCode? commodity) => _byCode.TryGetValue(code, out commodity);
}
