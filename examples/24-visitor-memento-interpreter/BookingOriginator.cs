namespace TariffRules;

/// <summary>
/// The Memento pattern's originator: a shipment booking that can be amended in
/// place, snapshotted before a risky amendment, and rolled back if the
/// amendment needs to be undone.
/// </summary>
public sealed class BookingOriginator
{
    private List<BookingLine> _lines = [];

    public ServiceLevel ServiceLevel { get; private set; }

    public decimal WeightKilograms { get; private set; }

    public IReadOnlyList<BookingLine> Lines => _lines;

    public void SetServiceLevel(ServiceLevel serviceLevel) => ServiceLevel = serviceLevel;

    public void SetWeight(decimal weightKilograms) => WeightKilograms = weightKilograms;

    public void AddLine(BookingLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _lines.Add(line);
    }

    /// <summary>Captures the current state as an isolated snapshot.</summary>
    public BookingMemento Save() => new(ServiceLevel, WeightKilograms, [.. _lines]);

    /// <summary>
    /// Restores state from a snapshot, rebuilding the line list from the
    /// memento's copy rather than aliasing it, so later mutation of this
    /// booking can never reach back into the snapshot.
    /// </summary>
    public void Restore(BookingMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        ServiceLevel = memento.ServiceLevel;
        WeightKilograms = memento.WeightKilograms;
        _lines = [.. memento.Lines];
    }
}
