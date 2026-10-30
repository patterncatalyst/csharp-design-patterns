namespace CustomsClearance;

/// <summary>
/// Second link in the chain: computes the duty owed on the declared value.
/// This stage never halts; it only records a figure the later stages and the
/// final result carry forward.
/// </summary>
public sealed class DutyCalculationStage : ClearanceStage
{
    private const decimal DutyRate = 0.08m;

    protected override ClearanceResult Process(ClearanceContext context)
    {
        context.Duty = context.Declaration.DeclaredValue * DutyRate;
        return new ClearanceResult(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
    }
}
