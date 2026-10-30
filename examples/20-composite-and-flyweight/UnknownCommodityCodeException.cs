namespace ConsignmentRouting;

/// <summary>
/// Thrown by <see cref="CommodityCodeRegistry.Get"/> when asked for a code
/// the registry never interned. <see cref="CommodityCodeRegistry.TryGet"/> is
/// the non-throwing alternative for a caller that expects misses.
/// </summary>
public sealed class UnknownCommodityCodeException : Exception
{
    public UnknownCommodityCodeException()
    {
    }

    public UnknownCommodityCodeException(string message) : base(message)
    {
    }

    public UnknownCommodityCodeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
