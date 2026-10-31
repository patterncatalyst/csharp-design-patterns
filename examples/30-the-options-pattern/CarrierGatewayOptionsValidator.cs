using Microsoft.Extensions.Options;

namespace CarrierConfiguration;

/// <summary>
/// Validates a bound <see cref="CarrierGatewayOptions"/> instance, collecting
/// every violation instead of stopping at the first one, so a misconfigured
/// deployment learns about all its problems from a single failed startup
/// rather than one at a time.
/// </summary>
public sealed class CarrierGatewayOptionsValidator : IValidateOptions<CarrierGatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, CarrierGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint) ||
            !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            reasons.Add($"{nameof(CarrierGatewayOptions.Endpoint)} must be a non-empty absolute URI.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            reasons.Add($"{nameof(CarrierGatewayOptions.TimeoutSeconds)} must be greater than zero.");
        }

        if (options.RetryBudget < 0)
        {
            reasons.Add($"{nameof(CarrierGatewayOptions.RetryBudget)} must not be negative.");
        }

        return reasons.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(reasons);
    }
}
