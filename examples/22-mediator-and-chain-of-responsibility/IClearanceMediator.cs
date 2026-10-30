namespace CustomsClearance;

/// <summary>
/// The GoF Mediator role: the only thing a <see cref="ClearanceColleague"/>
/// talks to. Colleagues never hold a reference to one another.
/// </summary>
public interface IClearanceMediator
{
    void Send(string message, ClearanceColleague sender);
}
