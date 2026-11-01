namespace RateEngineDependencies;

/// <summary>
/// Looks up a lane's per-kilogram rate. Extracting this interface breaks
/// <see cref="RateEngine"/>'s dependency on a static lookup: a test
/// substitutes a fake rate without touching a shared static table.
/// </summary>
public interface ITariffBook
{
    decimal RateFor(string laneCode);
}
