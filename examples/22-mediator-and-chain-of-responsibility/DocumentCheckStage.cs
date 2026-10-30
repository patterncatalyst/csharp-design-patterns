namespace CustomsClearance;

/// <summary>
/// First link in the chain: a declaration with incomplete paperwork halts
/// here before any duty is computed or any inspection is checked.
/// </summary>
public sealed class DocumentCheckStage : ClearanceStage
{
    protected override ClearanceResult Process(ClearanceContext context)
    {
        if (!context.Declaration.DocumentsComplete)
        {
            return new ClearanceResult(
                Cleared: false,
                HaltedStage: nameof(DocumentCheckStage),
                Reason: "Required clearance documents are missing.",
                Duty: context.Duty);
        }

        return new ClearanceResult(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
    }
}
