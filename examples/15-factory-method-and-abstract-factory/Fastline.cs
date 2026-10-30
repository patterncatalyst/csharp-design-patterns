namespace CarrierProvisioning;

/// <summary>
/// The Fastline carrier key. Never instantiated; used only as a type
/// argument to reach Fastline's gateway family through
/// <see cref="CarrierRegistry"/> or <see cref="ShipmentDispatchService"/>.
/// </summary>
public sealed class Fastline : ICarrier
{
    public static string Code => "FASTLINE";
}
