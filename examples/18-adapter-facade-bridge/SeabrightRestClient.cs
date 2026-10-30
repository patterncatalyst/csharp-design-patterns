namespace CarrierIntegration;

/// <summary>
/// The native request shape <see cref="SeabrightRestClient"/> expects: port
/// names and a kilogram weight, the way a REST client would model a JSON
/// request body.
/// </summary>
public sealed record SeabrightRateRequest(string FromPort, string ToPort, decimal WeightKilograms);

/// <summary>
/// The native response shape <see cref="SeabrightRestClient"/> returns: a
/// decimal dollar rate and an estimated day count.
/// </summary>
public sealed record SeabrightQuoteDto(decimal RateDollars, int EstimatedDays);

/// <summary>
/// A fake, in-memory stand-in for a generated REST client, configured with
/// the base address it would otherwise call out to. Seabright's native call
/// takes a request DTO and answers with a response DTO, both shaped around
/// ports and dollars rather than the origin/destination and cent amounts
/// <see cref="ICarrierGateway"/> uses.
/// </summary>
public sealed class SeabrightRestClient(string baseAddress = "https://seabright.example/api/v2/rates")
{
    public SeabrightQuoteDto RequestRate(SeabrightRateRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);

        decimal rate = 18.50m + (body.WeightKilograms * 0.95m);
        int days = body.FromPort == body.ToPort ? 2 : 6;
        return new SeabrightQuoteDto(rate, days);
    }
}
