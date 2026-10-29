namespace CarrierGateways;

/// <summary>
/// Quoting a shipment: the only member a rate-shopping consumer needs. Split
/// out of a single fat carrier interface so that consumer depends on nothing
/// it does not call (Interface Segregation Principle).
/// </summary>
public interface ICarrierRates
{
    Task<RateQuote> GetRateAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);
}

/// <summary>
/// Booking a shipment and following it afterward: the two members a booking
/// consumer needs. A consumer that only quotes rates never has to implement
/// or stub these.
/// </summary>
public interface ICarrierTracking
{
    Task<BookingReceipt> BookAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);

    Task<string> GetTrackingStatusAsync(string trackingNumber, CancellationToken cancellationToken);
}

/// <summary>
/// The full carrier integration surface, composed from the two segregated
/// interfaces above. This is the abstraction the composition root registers
/// a concrete gateway against, and the one <see cref="ICarrierGateway"/>
/// high-level code depends on under the Dependency Inversion Principle:
/// neither the consumer nor this interface knows about
/// <see cref="RestCarrierGateway"/> or <see cref="FakeCarrierGateway"/>.
/// A gateway implementation satisfies both narrow interfaces at once; a
/// consumer is still free to depend on only the one it uses.
/// </summary>
public interface ICarrierGateway : ICarrierRates, ICarrierTracking
{
}
