namespace TariffRules;

/// <summary>
/// An immutable snapshot of a <see cref="BookingOriginator"/>'s state. The
/// Memento pattern's memento: opaque to the <see cref="AmendmentCaretaker"/>
/// that holds it, meaningful only to the originator that created and restores
/// it.
/// </summary>
public sealed record BookingMemento(
    ServiceLevel ServiceLevel,
    decimal WeightKilograms,
    IReadOnlyList<BookingLine> Lines);
