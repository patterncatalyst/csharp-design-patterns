namespace CarrierIntegration;

/// <summary>
/// The data a shipment label carries, independent of both the label kind
/// that built it and the renderer that will turn it into a printable
/// format. This is the type that crosses the Bridge between the abstraction
/// (<see cref="ShipmentLabel"/>) and the implementor (<see cref="ILabelRenderer"/>).
/// </summary>
public sealed record LabelContent(string Title, string CarrierCode, string Destination, string Instructions);
