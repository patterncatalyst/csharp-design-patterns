namespace TariffTables;

/// <summary>
/// Tier 1: the textbook GoF Singleton, exactly as the 1994 shape describes it:
/// a private constructor, a single static instance, and one global access
/// point. Kept here as the baseline the rest of the chapter moves away from;
/// <see cref="LazyTariffTable"/> gets the same guarantee without a static
/// access point to mock around, and the built-in DI container's singleton
/// lifetime gets it with no hand-written synchronization at all.
/// </summary>
public sealed class ClassicTariffTable
{
    private static readonly ClassicTariffTable InstanceValue;

    private readonly TariffTable _table;

    // An explicit static constructor, even one that only assigns a field
    // already declared with "static readonly", suppresses the compiler's
    // beforefieldinit flag on this type. Without it, the CLR would be free to
    // run the field initializer at any point before first use, including
    // before a type-initialization-order-sensitive test has set anything up;
    // with it, initialization is guaranteed to happen lazily, right before
    // the first access to any static member of this type.
    static ClassicTariffTable()
    {
        InstanceValue = new ClassicTariffTable(new CountingTariffSource());
    }

    private ClassicTariffTable(ITariffSource source)
    {
        _table = new TariffTable(source);
    }

    /// <summary>The single shared instance, built on first access to this property.</summary>
    public static ClassicTariffTable Instance => InstanceValue;

    public decimal Lookup(string origin, string destination) => _table.Lookup(origin, destination);
}
