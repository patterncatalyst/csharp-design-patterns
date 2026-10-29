using System.Globalization;

namespace FreightValueTypes;

/// <summary>
/// Parses one fixed-width, EDI-style consignment segment directly from a
/// <see cref="ReadOnlySpan{Char}"/>, with no intermediate string allocation
/// anywhere on the happy path.
/// </summary>
/// <remarks>
/// <para>
/// This is a <c>readonly ref struct</c> because it stores the span field
/// below, and only a <c>ref struct</c> may hold a <see cref="ReadOnlySpan{Char}"/>
/// field. That same restriction is what keeps every instance stack-only: it
/// cannot be boxed, cannot be a field of a non-<c>ref</c> type, cannot be
/// captured in a lambda or local function, and cannot be used across an
/// <c>await</c> or inside an iterator. All parsing here is synchronous and
/// non-capturing, which is exactly what a <c>ref struct</c> requires.
/// </para>
/// <para>
/// Field layout (28 characters total, offsets and lengths zero-based):
/// </para>
/// <list type="table">
/// <listheader><term>Field</term><description>Offset, length, example</description></listheader>
/// <item><term>ConsignmentId</term><description>0, 6: zero-padded digits, e.g. "004217"</description></item>
/// <item><term>Bay</term><description>6, 2: zero-padded digits, e.g. "03"</description></item>
/// <item><term>Tier</term><description>8, 2: zero-padded digits, e.g. "02"</description></item>
/// <item><term>WeightKilograms</term><description>10, 7: fixed two decimals, e.g. "0125.50"</description></item>
/// <item><term>Amount</term><description>17, 8: fixed two decimals, e.g. "00450.75"</description></item>
/// <item><term>Currency</term><description>25, 3: three-letter code, e.g. "USD"</description></item>
/// </list>
/// </remarks>
public readonly ref struct EdiRecordParser
{
    private const int SegmentLength = 28;

    private const int ConsignmentIdOffset = 0;
    private const int ConsignmentIdLength = 6;
    private const int BayOffset = 6;
    private const int BayLength = 2;
    private const int TierOffset = 8;
    private const int TierLength = 2;
    private const int WeightOffset = 10;
    private const int WeightLength = 7;
    private const int AmountOffset = 17;
    private const int AmountLength = 8;
    private const int CurrencyOffset = 25;
    private const int CurrencyLength = 3;

    private readonly ReadOnlySpan<char> _segment;

    private EdiRecordParser(ReadOnlySpan<char> segment) => _segment = segment;

    /// <summary>
    /// Parses <paramref name="segment"/> into <paramref name="record"/>.
    /// Returns <see langword="false"/> (it never throws) for a segment of
    /// the wrong length, a non-numeric field, or an unrecognized currency
    /// code.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> segment, out ConsignmentRecord record)
    {
        var parser = new EdiRecordParser(segment);
        return parser.TryParseCore(out record);
    }

    private bool TryParseCore(out ConsignmentRecord record)
    {
        record = default;

        if (_segment.Length != SegmentLength)
        {
            return false;
        }

        if (!int.TryParse(_segment.Slice(ConsignmentIdOffset, ConsignmentIdLength), NumberStyles.None, CultureInfo.InvariantCulture, out var consignmentId))
        {
            return false;
        }

        if (!int.TryParse(_segment.Slice(BayOffset, BayLength), NumberStyles.None, CultureInfo.InvariantCulture, out var bay))
        {
            return false;
        }

        if (!int.TryParse(_segment.Slice(TierOffset, TierLength), NumberStyles.None, CultureInfo.InvariantCulture, out var tier))
        {
            return false;
        }

        if (!decimal.TryParse(_segment.Slice(WeightOffset, WeightLength), NumberStyles.Number, CultureInfo.InvariantCulture, out var kilograms))
        {
            return false;
        }

        if (!decimal.TryParse(_segment.Slice(AmountOffset, AmountLength), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        if (!TryParseCurrency(_segment.Slice(CurrencyOffset, CurrencyLength), out var currency))
        {
            return false;
        }

        record = new ConsignmentRecord(consignmentId, new SlotId(bay, tier), new Weight(kilograms), new Money(amount, currency));
        return true;
    }

    /// <summary>
    /// Matches the three-character currency span against a fixed, known set
    /// and returns the matching interned literal, never <see cref="ReadOnlySpan{Char}.ToString"/>
    /// on the span, so a successful parse allocates nothing even for the
    /// one field that ends up as a <see cref="string"/>.
    /// </summary>
    private static bool TryParseCurrency(ReadOnlySpan<char> currencySpan, out string currency)
    {
        if (currencySpan.SequenceEqual("USD"))
        {
            currency = "USD";
            return true;
        }

        if (currencySpan.SequenceEqual("EUR"))
        {
            currency = "EUR";
            return true;
        }

        if (currencySpan.SequenceEqual("GBP"))
        {
            currency = "GBP";
            return true;
        }

        if (currencySpan.SequenceEqual("CAD"))
        {
            currency = "CAD";
            return true;
        }

        currency = "";
        return false;
    }
}
