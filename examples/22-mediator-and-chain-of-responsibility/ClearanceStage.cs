namespace CustomsClearance;

/// <summary>
/// The GoF Chain of Responsibility role: a link that either halts a
/// declaration with a reason, or lets it continue to the next link. Each
/// stage knows only its own successor, never the whole pipeline, and a halt
/// is signaled by returning a non-cleared <see cref="ClearanceResult"/>, not
/// by throwing.
/// </summary>
public abstract class ClearanceStage
{
    private ClearanceStage? _next;

    /// <summary>
    /// Wires this stage's successor and returns that successor, so a pipeline
    /// can be built as a single fluent chain of calls.
    /// </summary>
    public ClearanceStage SetNext(ClearanceStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _next = stage;
        return stage;
    }

    public ClearanceResult Handle(ClearanceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.RecordStage(GetType().Name);
        var result = Process(context);
        if (!result.Cleared)
        {
            return result;
        }

        return _next?.Handle(context) ?? result;
    }

    protected abstract ClearanceResult Process(ClearanceContext context);
}
