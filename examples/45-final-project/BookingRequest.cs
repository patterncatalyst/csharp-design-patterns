namespace ShipmentWorkflow;

/// <summary>
/// A request to book one consignment for shipment: the ports it moves
/// between, its declared value and weight for rating, and the
/// <see cref="ServiceLevel"/> it ships under. Carried through every stage of
/// the <see cref="ShipmentPipeline"/> inside a <see cref="ShipmentContext"/>.
/// </summary>
public sealed record BookingRequest(
    string ConsignmentId,
    string OriginPort,
    string DestinationPort,
    decimal DeclaredValue,
    decimal WeightKilograms,
    ServiceLevel Service);
