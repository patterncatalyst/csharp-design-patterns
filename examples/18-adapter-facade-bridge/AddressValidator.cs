namespace CarrierIntegration;

/// <summary>
/// A minimal address validator: treats a blank address, or one explicitly
/// marked invalid, as unshippable. A real validator would call an address
/// verification service; this example keeps the rule simple and
/// deterministic.
/// </summary>
public sealed class AddressValidator : IAddressValidator
{
    public void Validate(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (IsMalformed(request.Origin))
        {
            throw new AddressValidationException(
                $"Address '{request.Origin}' is not a recognized shipping address.");
        }

        if (IsMalformed(request.Destination))
        {
            throw new AddressValidationException(
                $"Address '{request.Destination}' is not a recognized shipping address.");
        }
    }

    private static bool IsMalformed(string address) =>
        string.IsNullOrWhiteSpace(address) || address.Contains("INVALID", StringComparison.OrdinalIgnoreCase);
}
