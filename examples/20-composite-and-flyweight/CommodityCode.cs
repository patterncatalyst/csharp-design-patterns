namespace ConsignmentRouting;

/// <summary>
/// A Harmonized System commodity code, the second Flyweight payload interned
/// by <see cref="CommodityCodeRegistry"/>. A plain reference type for the
/// same reason as <see cref="PortCode"/>: sharing by reference, not value
/// equality, is the point.
/// </summary>
public sealed class CommodityCode
{
    public CommodityCode(string code, string description)
    {
        Code = code;
        Description = description;
    }

    public string Code { get; }

    public string Description { get; }

    public override string ToString() => $"{Code} ({Description})";
}
