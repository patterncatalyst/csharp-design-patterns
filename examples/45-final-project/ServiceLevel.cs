namespace ShipmentWorkflow;

/// <summary>
/// The service tier a consignment ships under: <see cref="Standard"/> at the
/// base rate, or <see cref="Express"/> at a surcharge over it. Selects which
/// <see cref="IRatingStrategy"/> <see cref="RatingStrategies.For"/> returns.
/// </summary>
public enum ServiceLevel
{
    Standard,
    Express,
}
