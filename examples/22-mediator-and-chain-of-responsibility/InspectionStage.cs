namespace CustomsClearance;

/// <summary>
/// Third link in the chain: a declaration flagged for physical inspection
/// halts here if that inspection has not passed. A declaration that was never
/// flagged skips straight through.
/// </summary>
public sealed class InspectionStage : ClearanceStage
{
    protected override ClearanceResult Process(ClearanceContext context)
    {
        var declaration = context.Declaration;
        if (declaration.RequiresInspection && !declaration.InspectionPassed)
        {
            return new ClearanceResult(
                Cleared: false,
                HaltedStage: nameof(InspectionStage),
                Reason: "Physical inspection did not pass.",
                Duty: context.Duty);
        }

        return new ClearanceResult(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
    }
}
