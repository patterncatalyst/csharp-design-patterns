namespace HandlingEventIngest;

/// <summary>
/// The kind of handling a carrier or terminal reports against a consignment
/// as it moves through the network.
/// </summary>
public enum HandlingKind
{
    Receive,
    Load,
    Unload,
    Customs,
    Claim,
}
