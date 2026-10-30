namespace CarrierProvisioning;

/// <summary>
/// Thrown by <see cref="CarrierRegistry.FactoryFor{TCarrier}"/> when no
/// factory has been registered for <typeparamref name="TCarrier"/>.
/// </summary>
public sealed class UnknownCarrierException : Exception
{
    public UnknownCarrierException()
    {
    }

    public UnknownCarrierException(string message)
        : base(message)
    {
    }

    public UnknownCarrierException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
