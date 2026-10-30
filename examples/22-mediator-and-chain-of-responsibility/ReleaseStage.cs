namespace CustomsClearance;

/// <summary>
/// Final link in the chain: reached only when every earlier stage cleared the
/// declaration, so it always clears, carrying forward the duty the chain
/// computed along the way.
/// </summary>
public sealed class ReleaseStage : ClearanceStage
{
    protected override ClearanceResult Process(ClearanceContext context) =>
        new(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
}
