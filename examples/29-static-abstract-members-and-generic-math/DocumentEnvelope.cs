namespace RateMath;

/// <summary>A documents-only envelope, rated by page count alone.</summary>
/// <param name="PageCount">The number of pages enclosed.</param>
public sealed record DocumentEnvelope(int PageCount) : IRateable<DocumentEnvelope>
{
    private const decimal PerPageFee = 0.10m;

    public static Money BaseRate => new(2.00m);

    public static Money RateOf(DocumentEnvelope shipment)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        return BaseRate + new Money(shipment.PageCount * PerPageFee);
    }
}
