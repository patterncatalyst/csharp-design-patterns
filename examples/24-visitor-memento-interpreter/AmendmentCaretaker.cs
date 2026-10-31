namespace TariffRules;

/// <summary>
/// The Memento pattern's caretaker: keeps a history of snapshots for a
/// <see cref="BookingOriginator"/> and can roll back to the most recent one,
/// without ever looking inside a <see cref="BookingMemento"/>.
/// </summary>
public sealed class AmendmentCaretaker
{
    private readonly Stack<BookingMemento> _history = new();

    public void Checkpoint(BookingOriginator originator)
    {
        ArgumentNullException.ThrowIfNull(originator);
        _history.Push(originator.Save());
    }

    public void Rollback(BookingOriginator originator)
    {
        ArgumentNullException.ThrowIfNull(originator);

        if (_history.Count == 0)
        {
            throw new InvalidOperationException("No checkpoint has been recorded to roll back to.");
        }

        originator.Restore(_history.Pop());
    }
}
