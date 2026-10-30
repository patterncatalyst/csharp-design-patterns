namespace CustomsClearance;

/// <summary>
/// The GoF Colleague role: a participant that sends and receives messages
/// only through its <see cref="IClearanceMediator"/>, never by holding a
/// direct reference to another colleague.
/// </summary>
public abstract class ClearanceColleague
{
    private readonly List<string> _inbox = [];

    protected ClearanceColleague(IClearanceMediator mediator)
    {
        Mediator = mediator;
    }

    protected IClearanceMediator Mediator { get; }

    public IReadOnlyList<string> Inbox => _inbox;

    public void Receive(string message) => _inbox.Add(message);

    public void Send(string message) => Mediator.Send(message, this);
}
