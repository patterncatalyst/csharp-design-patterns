namespace CustomsClearance;

/// <summary>
/// A concrete colleague: the desk that assesses duty once a declaration has
/// been logged. It knows nothing about <see cref="DeclarationDesk"/>; it only
/// sends through the mediator.
/// </summary>
public sealed class DutyDesk : ClearanceColleague
{
    public DutyDesk(IClearanceMediator mediator)
        : base(mediator)
    {
    }
}
