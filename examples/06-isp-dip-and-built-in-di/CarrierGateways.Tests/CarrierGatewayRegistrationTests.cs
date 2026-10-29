using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarrierGateways.Tests;

public class CarrierGatewayRegistrationTests
{
    [Fact]
    public async Task Resolved_gateway_returns_a_priced_rate_quote()
    {
        var services = new ServiceCollection();
        services.AddCarrierGateway();
        await using var provider = services.BuildServiceProvider();

        var gateway = provider.GetRequiredService<ICarrierGateway>();
        var quote = await gateway.GetRateAsync("Rotterdam", 820m, TestContext.Current.CancellationToken);

        Assert.True(quote.Price.Amount > 0m);
        Assert.Equal("USD", quote.Price.Currency);
        Assert.True(quote.TransitTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task Resolved_gateway_records_a_booking_it_can_then_track()
    {
        var services = new ServiceCollection();
        services.AddCarrierGateway();
        await using var provider = services.BuildServiceProvider();

        var gateway = provider.GetRequiredService<ICarrierGateway>();
        var receipt = await gateway.BookAsync("Singapore", 410m, TestContext.Current.CancellationToken);
        var status = await gateway.GetTrackingStatusAsync(receipt.TrackingNumber, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(receipt.TrackingNumber));
        Assert.Equal("Booked", status);
    }

    [Fact]
    public async Task Narrow_interfaces_resolve_to_the_same_registered_gateway_instance()
    {
        var services = new ServiceCollection();
        services.AddCarrierGateway();
        await using var provider = services.BuildServiceProvider();

        var gateway = provider.GetRequiredService<ICarrierGateway>();
        var rates = provider.GetRequiredService<ICarrierRates>();
        var tracking = provider.GetRequiredService<ICarrierTracking>();

        Assert.Same(gateway, rates);
        Assert.Same(gateway, tracking);
    }

    [Fact]
    public async Task Container_builds_consumers_through_constructor_injection()
    {
        var services = new ServiceCollection();
        services.AddCarrierGateway();
        await using var provider = services.BuildServiceProvider();

        var advisor = provider.GetRequiredService<ShipmentRateAdvisor>();
        var description = await advisor.DescribeQuoteAsync("Santos", 300m, TestContext.Current.CancellationToken);

        Assert.Contains("Santos", description, StringComparison.Ordinal);
    }
}
