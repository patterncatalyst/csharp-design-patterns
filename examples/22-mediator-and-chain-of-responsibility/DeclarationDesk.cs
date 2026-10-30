namespace CustomsClearance;

/// <summary>
/// A concrete colleague: the desk that first logs a consignment's
/// declaration. It knows nothing about <see cref="DutyDesk"/>; it only sends
/// through the mediator.
/// </summary>
public sealed class DeclarationDesk : ClearanceColleague
{
    public DeclarationDesk(IClearanceMediator mediator)
        : base(mediator)
    {
    }
}
