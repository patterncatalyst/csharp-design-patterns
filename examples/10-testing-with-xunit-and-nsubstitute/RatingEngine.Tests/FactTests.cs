using NSubstitute;
using Xunit;

namespace RatingEngine.Tests;

/// <summary>
/// The canonical four-phase <c>[Fact]</c> test: Arrange, Act, Assert, with
/// substitute setup folded into Arrange. Every other test in this project
/// builds on this shape.
/// </summary>
public class FactTests
{
    [Fact]
    public async Task Quotes_the_base_rate_with_tax_applied()
    {
        // Arrange
        var carrierRates = Substitute.For<ICarrierRates>();
        carrierRates.GetBaseRateAsync("Rotterdam", 820m, Arg.Any<CancellationToken>())
            .Returns(new Money(100.00m));
        var taxPolicy = Substitute.For<ITaxPolicy>();
        taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));
        var auditSink = Substitute.For<IAuditSink>();
        var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);
        var consignment = new Consignment("C-0001", "Rotterdam", 820m);

        // Act
        var quote = await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Money(110.00m), quote);
    }
}
