namespace ConsignmentRouting;

/// <summary>
/// A UN/LOCODE port of call, the Flyweight payload interned by
/// <see cref="PortCodeRegistry"/>. This is deliberately a plain reference
/// type, not a record: every <see cref="ShipmentParcel"/> bound for the same
/// port holds a reference to the one shared instance, and the tests prove
/// that sharing with reference equality, something a value-equal record would
/// paper over.
/// </summary>
public sealed class PortCode
{
    public PortCode(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; }

    public string Name { get; }

    public override string ToString() => $"{Code} ({Name})";
}
