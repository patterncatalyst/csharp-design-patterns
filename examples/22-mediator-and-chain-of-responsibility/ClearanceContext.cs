namespace CustomsClearance;

/// <summary>
/// Mutable state threaded through a <see cref="ClearanceStage"/> chain for one
/// declaration: which stages actually ran, in order, and the duty a stage
/// computes along the way. The declaration itself stays immutable; this is
/// the chain's own working state.
/// </summary>
public sealed class ClearanceContext
{
    private readonly List<string> _stagesRun = [];

    public ClearanceContext(CustomsDeclaration declaration)
    {
        Declaration = declaration;
    }

    public CustomsDeclaration Declaration { get; }

    public IReadOnlyList<string> StagesRun => _stagesRun;

    public decimal Duty { get; set; }

    internal void RecordStage(string stageName) => _stagesRun.Add(stageName);
}
