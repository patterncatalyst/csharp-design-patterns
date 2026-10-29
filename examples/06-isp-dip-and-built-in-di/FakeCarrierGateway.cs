namespace CarrierGateways;

/// <summary>
/// A deterministic stand-in for a real carrier, used in a sandbox or demo
/// environment where no production carrier contract exists yet. Every quote
/// and booking returns the same canned values, which is exactly what lets a
/// swap test prove that changing the DI registration from
/// <see cref="RestCarrierGateway"/> to this type changes the resolved
/// behavior without touching a single consumer.
/// </summary>
public sealed class FakeCarrierGateway : ICarrierGateway
{
    public static readonly Money FlatRate = new(9.99m);
    private const string FixedTrackingNumber = "FAKE-000001";

    public Task<RateQuote> GetRateAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RateQuote(FlatRate, TimeSpan.FromDays(1)));
    }

    public Task<BookingReceipt> BookAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new BookingReceipt(FixedTrackingNumber, DateTimeOffset.UnixEpoch));
    }

    public Task<string> GetTrackingStatusAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("Booked");
    }
}
