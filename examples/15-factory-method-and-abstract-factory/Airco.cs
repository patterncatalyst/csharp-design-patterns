namespace CarrierProvisioning;

/// <summary>
/// The Airco carrier key. Never instantiated; used only as a type argument
/// to reach Airco's gateway family through <see cref="CarrierRegistry"/> or
/// <see cref="ShipmentDispatchService"/>. Airco is added after Fastline and
/// Seabright are already wired up, and needs no change to
/// <see cref="CarrierRegistry"/> itself.
/// </summary>
public sealed class Airco : ICarrier
{
    public static string Code => "AIRCO";
}
