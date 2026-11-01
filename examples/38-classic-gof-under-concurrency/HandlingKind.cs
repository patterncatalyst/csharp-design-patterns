namespace ConcurrentHandling;

/// <summary>
/// The physical handling steps a consignment moves through on its way from
/// receipt to claim.
/// </summary>
public enum HandlingKind
{
    Receive,
    Load,
    Unload,
    Customs,
    Claim,
}
