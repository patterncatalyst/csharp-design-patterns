namespace BookingRequests;

/// <summary>
/// Thrown by <see cref="BookingRequestBuilder.Build"/> when a required field
/// is missing or invalid. The runtime mirror of the compile-time check the
/// language performs for an object initializer that omits a required member.
/// </summary>
public sealed class BookingValidationException : Exception
{
    public BookingValidationException()
    {
    }

    public BookingValidationException(string message)
        : base(message)
    {
    }

    public BookingValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
