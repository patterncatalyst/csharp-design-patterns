using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarrierGateways.Tests;

/// <summary>
/// Proves the Dependency Inversion payoff: swapping which concrete gateway
/// backs <see cref="ICarrierGateway"/> is a one-line change to the DI
/// registration. <see cref="ShipmentRateAdvisor"/> and
/// <see cref="ShipmentBookingService"/> are built, unmodified, from each
/// provider; only their resolved behavior differs.
/// </summary>
public class CarrierGatewaySwapTests
{
    [Fact]
    public async Task Swapping_the_registration_changes_the_resolved_quote_with_no_consumer_change()
    {
        var restServices = new ServiceCollection();
        restServices.AddCarrierGateway<RestCarrierGateway>();
        await using var restProvider = restServices.BuildServiceProvider();

        var fakeServices = new ServiceCollection();
        fakeServices.AddCarrierGateway<FakeCarrierGateway>();
        await using var fakeProvider = fakeServices.BuildServiceProvider();

        var restAdvisor = restProvider.GetRequiredService<ShipmentRateAdvisor>();
        var fakeAdvisor = fakeProvider.GetRequiredService<ShipmentRateAdvisor>();

        var restDescription = await restAdvisor.DescribeQuoteAsync("Rotterdam", 820m, TestContext.Current.CancellationToken);
        var fakeDescription = await fakeAdvisor.DescribeQuoteAsync("Rotterdam", 820m, TestContext.Current.CancellationToken);

        Assert.NotEqual(restDescription, fakeDescription);
        Assert.Contains(FakeCarrierGateway.FlatRate.ToString(), fakeDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swapping_the_registration_changes_the_resolved_tracking_number_format()
    {
        var restServices = new ServiceCollection();
        restServices.AddCarrierGateway<RestCarrierGateway>();
        await using var restProvider = restServices.BuildServiceProvider();

        var fakeServices = new ServiceCollection();
        fakeServices.AddCarrierGateway<FakeCarrierGateway>();
        await using var fakeProvider = fakeServices.BuildServiceProvider();

        var restBooking = restProvider.GetRequiredService<ShipmentBookingService>();
        var fakeBooking = fakeProvider.GetRequiredService<ShipmentBookingService>();

        var restReceipt = await restBooking.BookShipmentAsync("Santos", 150m, TestContext.Current.CancellationToken);
        var fakeReceipt = await fakeBooking.BookShipmentAsync("Santos", 150m, TestContext.Current.CancellationToken);

        Assert.StartsWith("RST-", restReceipt.TrackingNumber, StringComparison.Ordinal);
        Assert.Equal("FAKE-000001", fakeReceipt.TrackingNumber);
    }
}
