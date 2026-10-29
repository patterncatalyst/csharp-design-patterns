namespace FreightValueTypes;

/// <summary>
/// A warehouse storage slot identified by bay and tier. Like
/// <see cref="Weight"/>, <c>default(SlotId)</c> (Bay 0, Tier 0) is a real,
/// addressable slot, so it needs no sentinel the way <see cref="Money"/>
/// does.
/// </summary>
public readonly record struct SlotId(int Bay, int Tier)
{
    /// <summary>The bay number, validated non-negative at construction.</summary>
    public int Bay { get; init; } = Bay >= 0
        ? Bay
        : throw new ArgumentOutOfRangeException(nameof(Bay), Bay, "Bay cannot be negative.");

    /// <summary>The tier number, validated non-negative at construction.</summary>
    public int Tier { get; init; } = Tier >= 0
        ? Tier
        : throw new ArgumentOutOfRangeException(nameof(Tier), Tier, "Tier cannot be negative.");

    public override string ToString() => $"Bay {Bay}, Tier {Tier}";
}
