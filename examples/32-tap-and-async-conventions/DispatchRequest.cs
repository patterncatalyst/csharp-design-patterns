namespace CarrierDispatch;

/// <summary>
/// A request to hand one consignment to a carrier for dispatch.
/// </summary>
public sealed record DispatchRequest(string ConsignmentId, string Destination, decimal WeightKilograms);
