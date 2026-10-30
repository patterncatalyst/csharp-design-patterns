namespace CarrierIntegration;

/// <summary>
/// Thrown by <see cref="AddressValidator"/> when a shipment's origin or
/// destination is not a recognized address. Sealed, with the three
/// constructors expected of any exception type.
/// </summary>
public sealed class AddressValidationException : Exception
{
    public AddressValidationException()
    {
    }

    public AddressValidationException(string message)
        : base(message)
    {
    }

    public AddressValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
