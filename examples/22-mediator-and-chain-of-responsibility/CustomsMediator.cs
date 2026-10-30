namespace CustomsClearance;

/// <summary>
/// The GoF ConcreteMediator role: routes a message from one desk to the
/// other, never back to the sender. The two colleagues cannot be passed to
/// this mediator's constructor, since each colleague's own constructor needs
/// a mediator reference first; <see cref="Register"/> wires them in
/// afterward, breaking the construction cycle.
/// </summary>
public sealed class CustomsMediator : IClearanceMediator
{
    private DeclarationDesk? _declarationDesk;
    private DutyDesk? _dutyDesk;

    public void Register(DeclarationDesk declarationDesk, DutyDesk dutyDesk)
    {
        _declarationDesk = declarationDesk;
        _dutyDesk = dutyDesk;
    }

    public void Send(string message, ClearanceColleague sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        if (ReferenceEquals(sender, _declarationDesk))
        {
            _dutyDesk?.Receive(message);
        }
        else if (ReferenceEquals(sender, _dutyDesk))
        {
            _declarationDesk?.Receive(message);
        }
    }
}
