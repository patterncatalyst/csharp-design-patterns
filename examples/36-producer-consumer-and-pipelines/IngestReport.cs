namespace HandlingEventIngest;

/// <summary>
/// The result of draining a <see cref="HandlingEventPipeline"/>: how many
/// events every consumer processed in total, the events each consumer
/// actually observed (in per-consumer interleaved order when more than one
/// consumer ran), and a count of events grouped by <see cref="HandlingKind"/>.
/// </summary>
public sealed record IngestReport(
    int Processed,
    IReadOnlyList<HandlingEvent> Observed,
    IReadOnlyDictionary<HandlingKind, int> ByKind);
