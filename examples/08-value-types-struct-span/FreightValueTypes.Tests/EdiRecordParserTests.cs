using Xunit;

namespace FreightValueTypes.Tests;

/// <summary>
/// <see cref="EdiRecordParser"/> over the 28-character fixed-width segment:
/// ConsignmentId(6) Bay(2) Tier(2) WeightKilograms(7) Amount(8) Currency(3).
/// Each test segment below is built from its named fields so the field
/// boundaries stay visible at the call site.
/// </summary>
public sealed class EdiRecordParserTests
{
    private const string ConsignmentId = "004217";
    private const string Bay = "03";
    private const string Tier = "02";
    private const string WeightKilograms = "0125.50";
    private const string Amount = "00450.75";
    private const string Currency = "USD";

    private const string ValidSegment = ConsignmentId + Bay + Tier + WeightKilograms + Amount + Currency;

    [Fact]
    public void Valid_segment_is_28_characters_long()
    {
        // Guards the fixture itself: if a field constant above drifts out of
        // sync with the documented layout, this fails before the real
        // assertions below get a chance to give a confusing error.
        Assert.Equal(28, ValidSegment.Length);
    }

    [Fact]
    public void TryParse_valid_segment_parses_every_field_into_its_typed_slot()
    {
        var parsed = EdiRecordParser.TryParse(ValidSegment, out var record);

        Assert.True(parsed);
        Assert.Equal(4217, record.ConsignmentId);
        Assert.Equal(new SlotId(3, 2), record.Slot);
        Assert.Equal(new Weight(125.5m), record.Weight);
        Assert.Equal(new Money(450.75m, "USD"), record.Charge);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("CAD")]
    public void TryParse_recognizes_every_supported_currency_code(string currency)
    {
        var segment = ConsignmentId + Bay + Tier + WeightKilograms + Amount + currency;

        var parsed = EdiRecordParser.TryParse(segment, out var record);

        Assert.True(parsed);
        Assert.Equal(currency, record.Charge.Currency);
    }

    [Fact]
    public void TryParse_segment_too_short_returns_false_and_does_not_throw()
    {
        var tooShort = ValidSegment[..^1];

        var parsed = EdiRecordParser.TryParse(tooShort, out var record);

        Assert.False(parsed);
        Assert.Equal(default, record);
    }

    [Fact]
    public void TryParse_segment_too_long_returns_false_and_does_not_throw()
    {
        var tooLong = ValidSegment + "X";

        var parsed = EdiRecordParser.TryParse(tooLong, out var record);

        Assert.False(parsed);
        Assert.Equal(default, record);
    }

    [Fact]
    public void TryParse_non_numeric_consignment_id_returns_false_and_does_not_throw()
    {
        var segment = "AB4217" + Bay + Tier + WeightKilograms + Amount + Currency;

        var parsed = EdiRecordParser.TryParse(segment, out var record);

        Assert.False(parsed);
        Assert.Equal(default, record);
    }

    [Fact]
    public void TryParse_non_numeric_weight_returns_false_and_does_not_throw()
    {
        var segment = ConsignmentId + Bay + Tier + "XX25.50" + Amount + Currency;

        var parsed = EdiRecordParser.TryParse(segment, out var record);

        Assert.False(parsed);
        Assert.Equal(default, record);
    }

    [Fact]
    public void TryParse_unrecognized_currency_returns_false_and_does_not_throw()
    {
        var segment = ConsignmentId + Bay + Tier + WeightKilograms + Amount + "XYZ";

        var parsed = EdiRecordParser.TryParse(segment, out var record);

        Assert.False(parsed);
        Assert.Equal(default, record);
    }

    [Fact]
    public void TryParse_empty_segment_returns_false_and_does_not_throw()
    {
        var parsed = EdiRecordParser.TryParse(ReadOnlySpan<char>.Empty, out var record);

        Assert.False(parsed);
        Assert.Equal(default, record);
    }
}
