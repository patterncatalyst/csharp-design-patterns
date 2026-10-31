using System.Diagnostics.CodeAnalysis;
using CarrierParcels.Vendor;

namespace CarrierParcels;

/// <summary>
/// C# 14 extension members over the unowned, sealed <see cref="CarrierShipment"/>
/// vendor type: an instance extension property, static extension members, and
/// an extension operator, plus one classic <c>this</c>-parameter extension
/// method coexisting on the same static class.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1708:Identifiers should differ by more than case",
    Justification = "Analyzer false positive: CA1708 compares the compiler-synthesized " +
        "extension-block marker members for the instance and static extension(CarrierShipment) " +
        "blocks below as though they collided by case, even though the two blocks declare " +
        "disjoint member sets (one instance, one static). Known gap in the current " +
        "analyzer's support for C# 14 extension members, re-verified against SDK 10.0.113.")]
public static class CarrierShipmentExtensions
{
    extension(CarrierShipment shipment)
    {
        /// <summary>The shipment's declared weight, converted from grams to kilograms.</summary>
        public decimal WeightKilograms => shipment.WeightGrams / 1000m;

        /// <summary>True once a shipment crosses the carrier's heavy-parcel threshold.</summary>
        public bool IsHeavy => shipment.WeightGrams > 30_000;

        /// <summary>
        /// Returns a new shipment with <paramref name="grams"/> added to the weight,
        /// leaving the original <see cref="CarrierShipment"/> untouched: the vendor
        /// type has no setters, so there is nothing to mutate.
        /// </summary>
        public CarrierShipment WithAddedWeight(int grams) =>
            new(shipment.WeightGrams + grams, shipment.DestinationCode, shipment.DeclaredValueMinorUnits);

        /// <summary>
        /// Combines two shipments into one manifest line: weights and declared
        /// values add, and the combined destination is the left operand's, since a
        /// single parcel manifest line travels to one place.
        /// </summary>
        public static CarrierShipment operator +(CarrierShipment left, CarrierShipment right) =>
            new(
                left.WeightGrams + right.WeightGrams,
                left.DestinationCode,
                left.DeclaredValueMinorUnits + right.DeclaredValueMinorUnits);

        /// <summary>Friendly named alternate for <c>operator +</c>, as CA2225 expects.</summary>
        public static CarrierShipment Add(CarrierShipment left, CarrierShipment right) => left + right;
    }

    extension(CarrierShipment)
    {
        /// <summary>A zero-weight, unrouted shipment, useful as a fold seed.</summary>
        public static CarrierShipment Empty => new(0, string.Empty, 0);

        /// <summary>Builds a shipment from a kilogram weight, rounding to the nearest gram.</summary>
        public static CarrierShipment FromKilograms(decimal kilograms, string destinationCode) =>
            new(decimal.ToInt32(decimal.Round(kilograms * 1000m)), destinationCode, 0);
    }

    /// <summary>
    /// Classic <c>this</c>-parameter extension method, coexisting on the same
    /// static class as the C# 14 extension blocks above.
    /// </summary>
    public static string NormalisedDestination(this CarrierShipment shipment) =>
        shipment.DestinationCode.Trim().ToUpperInvariant();
}
