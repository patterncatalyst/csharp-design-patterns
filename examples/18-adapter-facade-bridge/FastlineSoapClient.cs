namespace CarrierIntegration;

/// <summary>
/// The native response shape returned by <see cref="FastlineSoapClient"/>:
/// a flat cent amount and a transit day count, the way a SOAP client
/// deserializes a typed response envelope.
/// </summary>
public sealed record FastlineQuoteResponse(long AmountCents, int DaysInTransit);

/// <summary>
/// A fake, in-memory stand-in for a generated SOAP client, configured with
/// the service endpoint it would otherwise dial out to. Real code would call
/// out to a WSDL-described endpoint; this example keeps the call in-process
/// and deterministic so the Adapter can be tested without a network.
/// Fastline's native call takes plain strings and an integer weight in
/// grams, and answers with its own response record, neither of which
/// matches <see cref="ICarrierGateway"/>.
/// </summary>
public sealed class FastlineSoapClient(string serviceEndpoint = "https://fastline.example/soap/quotes")
{
    public FastlineQuoteResponse SubmitQuote(string originCode, string destCode, int grams)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(destCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceEndpoint);

        long amountCents = 1200 + (grams * 3L);
        int transitDays = originCode == destCode ? 1 : 4;
        return new FastlineQuoteResponse(amountCents, transitDays);
    }
}
