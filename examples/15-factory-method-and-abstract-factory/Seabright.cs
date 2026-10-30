namespace CarrierProvisioning;

/// <summary>
/// The Seabright carrier key. Never instantiated; used only as a type
/// argument to reach Seabright's gateway family through
/// <see cref="CarrierRegistry"/> or <see cref="ShipmentDispatchService"/>.
/// </summary>
public sealed class Seabright : ICarrier
{
    public static string Code => "SEABRIGHT";
}
