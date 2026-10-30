using NSubstitute;
using Xunit;

namespace CarrierIntegration.Tests;

/// <summary>
/// Proves the Facade orchestrates its three subsystems in order, validate
/// then quote then file, and that a validation failure short-circuits the
/// rest of the call instead of being swallowed.
/// </summary>
public class ShipmentBookingFacadeTests
{
    [Fact]
    public void Book_validates_quotes_and_files_the_shipment_in_order()
    {
        var validator = Substitute.For<IAddressValidator>();
        var gateway = Substitute.For<ICarrierGateway>();
        var filer = Substitute.For<ICustomsFiler>();
        var request = new ShipmentRequest("ATL", "ORD", new Kilograms(2m));
        var quote = new CarrierQuote("FASTLINE", new Money(5000), 3);
        gateway.GetQuote(request).Returns(quote);
        filer.File(request, quote).Returns("FASTLINE-ATL-ORD-03");
        var facade = new ShipmentBookingFacade(validator, gateway, filer);

        ShipmentConfirmation confirmation = facade.Book(request);

        validator.Received(1).Validate(request);
        gateway.Received(1).GetQuote(request);
        filer.Received(1).File(request, quote);
        Assert.Equal("FASTLINE", confirmation.CarrierCode);
        Assert.Equal("FASTLINE-ATL-ORD-03", confirmation.BookingReference);
        Assert.Equal(quote.Amount, confirmation.Charged);
    }

    [Fact]
    public void Book_surfaces_an_address_validation_failure_without_quoting_or_filing()
    {
        var validator = Substitute.For<IAddressValidator>();
        var gateway = Substitute.For<ICarrierGateway>();
        var filer = Substitute.For<ICustomsFiler>();
        var request = new ShipmentRequest("INVALID", "ORD", new Kilograms(2m));
        validator
            .When(v => v.Validate(request))
            .Do(_ => throw new AddressValidationException("Address 'INVALID' is not a recognized shipping address."));
        var facade = new ShipmentBookingFacade(validator, gateway, filer);

        Assert.Throws<AddressValidationException>(() => facade.Book(request));
        gateway.DidNotReceiveWithAnyArgs().GetQuote(default!);
        filer.DidNotReceiveWithAnyArgs().File(default!, default!);
    }

    [Fact]
    public void AddressValidator_rejects_a_malformed_address()
    {
        var validator = new AddressValidator();
        var request = new ShipmentRequest("INVALID", "ORD", new Kilograms(1m));

        Assert.Throws<AddressValidationException>(() => validator.Validate(request));
    }

    [Fact]
    public void AddressValidator_accepts_a_well_formed_address()
    {
        var validator = new AddressValidator();
        var request = new ShipmentRequest("ATL", "ORD", new Kilograms(1m));

        Exception? exception = Record.Exception(() => validator.Validate(request));

        Assert.Null(exception);
    }
}
