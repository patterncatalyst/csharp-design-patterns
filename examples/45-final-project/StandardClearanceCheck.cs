namespace ShipmentWorkflow;

/// <summary>
/// Rejects a booking with a non-positive declared value or an embargoed
/// destination port; otherwise clears it.
/// </summary>
public sealed class StandardClearanceCheck : IClearanceCheck
{
    private static readonly HashSet<string> EmbargoedDestinations = ["ZZZ"];

    public ClearanceResult Check(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DeclaredValue <= 0m)
        {
            return ClearanceResult.Rejected("declared value must be positive");
        }

        if (EmbargoedDestinations.Contains(request.DestinationPort))
        {
            return ClearanceResult.Rejected("destination port is embargoed");
        }

        return ClearanceResult.Clear();
    }
}
