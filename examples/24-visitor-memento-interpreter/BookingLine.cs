namespace TariffRules;

/// <summary>A single line item on a shipment booking.</summary>
public sealed record BookingLine(string Sku, int Quantity);
