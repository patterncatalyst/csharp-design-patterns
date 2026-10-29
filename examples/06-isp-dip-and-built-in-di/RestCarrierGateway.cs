using System.Collections.Concurrent;

namespace CarrierGateways;

/// <summary>
/// Stands in for a real carrier's REST API: a destination surcharge plus a
/// per-kilogram rate, and an in-memory booking ledger in place of the
/// carrier's own tracking database. A production gateway would replace the
/// pricing lookup and booking ledger with an <c>HttpClient</c> call; nothing
/// else in this library, nor any consumer, would need to change because both
/// depend on <see cref="ICarrierGateway"/>, not on this type.
/// </summary>
public sealed class RestCarrierGateway : ICarrierGateway
{
    private const decimal BaseRatePerKilogram = 2.25m;
    private const decimal DefaultDestinationSurcharge = 50.00m;

    private static readonly Dictionary<string, decimal> DestinationSurcharges =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Rotterdam"] = 18.50m,
            ["Singapore"] = 42.00m,
            ["Santos"] = 35.75m,
        };

    private readonly ConcurrentDictionary<string, string> _bookingStatuses = new();

    public Task<RateQuote> GetRateAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isKnownDestination = DestinationSurcharges.TryGetValue(destination, out var surcharge);
        var price = new Money((isKnownDestination ? surcharge : DefaultDestinationSurcharge) + (BaseRatePerKilogram * weightKilograms));
        var transitTime = TimeSpan.FromDays(isKnownDestination ? 5 : 10);

        return Task.FromResult(new RateQuote(price, transitTime));
    }

    public Task<BookingReceipt> BookAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var trackingNumber = $"RST-{Guid.NewGuid():N}".ToUpperInvariant()[..12];
        _bookingStatuses[trackingNumber] = "Booked";

        return Task.FromResult(new BookingReceipt(trackingNumber, DateTimeOffset.UtcNow));
    }

    public Task<string> GetTrackingStatusAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = _bookingStatuses.TryGetValue(trackingNumber, out var recordedStatus) ? recordedStatus : "Unknown";
        return Task.FromResult(status);
    }
}
