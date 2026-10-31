// Stands in for a third-party carrier SDK assembly we cannot modify: a sealed
// type with a constructor and read-only properties only, no methods, shipped
// by a vendor who owns its source. CarrierShipmentExtensions attaches the
// behavior this book's examples need without touching this file again.
namespace CarrierParcels.Vendor;

public sealed class CarrierShipment
{
    public CarrierShipment(int weightGrams, string destinationCode, int declaredValueMinorUnits)
    {
        WeightGrams = weightGrams;
        DestinationCode = destinationCode;
        DeclaredValueMinorUnits = declaredValueMinorUnits;
    }

    public int WeightGrams { get; }

    public string DestinationCode { get; }

    public int DeclaredValueMinorUnits { get; }
}
