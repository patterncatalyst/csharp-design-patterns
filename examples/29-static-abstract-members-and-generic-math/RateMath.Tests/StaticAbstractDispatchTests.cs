using Xunit;

namespace RateMath.Tests;

public class StaticAbstractDispatchTests
{
    [Fact]
    public void Quote_dispatches_to_the_concrete_types_static_RateOf()
    {
        var shipment = new ParcelShipment(WeightKilograms: 10m);

        var quoted = RateBook.Quote(shipment);

        Assert.Equal(new Money(12.00m), quoted);
        Assert.Equal(ParcelShipment.RateOf(shipment), quoted);
    }

    [Fact]
    public void Quote_uses_each_types_own_base_rate()
    {
        // Zero volume on every shipment-specific input strips each formula
        // down to its own base rate, proving RateBook.Quote<T> reads a
        // different static member per T rather than one shared constant.
        var parcelQuote = RateBook.Quote(new ParcelShipment(WeightKilograms: 0m));
        var freightQuote = RateBook.Quote(new FreightPallet(WeightKilograms: 0m, PalletCount: 0));
        var documentQuote = RateBook.Quote(new DocumentEnvelope(PageCount: 0));

        Assert.Equal(ParcelShipment.BaseRate, parcelQuote);
        Assert.Equal(FreightPallet.BaseRate, freightQuote);
        Assert.Equal(DocumentEnvelope.BaseRate, documentQuote);
        Assert.NotEqual(parcelQuote, freightQuote);
        Assert.NotEqual(freightQuote, documentQuote);
    }

    [Fact]
    public void BaseRateOf_reads_a_static_abstract_member_through_the_type_parameter()
    {
        Assert.Equal(new Money(4.50m), RateBook.BaseRateOf<ParcelShipment>());
        Assert.Equal(new Money(35.00m), RateBook.BaseRateOf<FreightPallet>());
        Assert.Equal(new Money(2.00m), RateBook.BaseRateOf<DocumentEnvelope>());
    }

    [Fact]
    public void Total_quote_over_a_set_sums_each_shipments_rate()
    {
        var shipments = new[]
        {
            new ParcelShipment(WeightKilograms: 2m),
            new ParcelShipment(WeightKilograms: 4m),
            new ParcelShipment(WeightKilograms: 6m),
        };

        var total = Totals.Sum(shipments.Select(RateBook.Quote));

        Assert.Equal(new Money(22.50m), total);
    }
}
