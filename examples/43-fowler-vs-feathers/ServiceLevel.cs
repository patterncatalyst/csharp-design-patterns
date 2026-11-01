namespace RateEngineCatalogues;

/// <summary>
/// The shipping service tiers a consignment can be rated under, shared by the
/// legacy, the minimally-seamed, and the fully refactored rate engines so all
/// three are compared over identical input.
/// </summary>
public enum ServiceLevel
{
    Standard,
    Express,
    Overnight,
}
