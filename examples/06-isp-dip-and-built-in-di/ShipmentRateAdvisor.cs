namespace CarrierGateways;

/// <summary>
/// Quotes a shipment for a customer. Depends only on
/// <see cref="ICarrierRates"/>, the narrow interface it actually calls, not
/// the full <see cref="ICarrierGateway"/>: it has no booking or tracking
/// concern, so Interface Segregation keeps it from depending on members it
/// would otherwise have to fake out in a test.
/// </summary>
public sealed class ShipmentRateAdvisor(ICarrierRates rates)
{
    public async Task<string> DescribeQuoteAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken)
    {
        var quote = await rates.GetRateAsync(destination, weightKilograms, cancellationToken).ConfigureAwait(false);
        return $"{destination}: {quote.Price}, {quote.TransitTime.Days}-day transit";
    }
}
