namespace RatingEngine;

/// <summary>
/// Records a finalized quote for compliance. Kept separate from pricing and
/// tax so tests can verify what was recorded, and how many times, without
/// writing to a real log, queue, or database.
/// </summary>
public interface IAuditSink
{
    Task RecordAsync(string consignmentId, Money quoted, CancellationToken cancellationToken);
}
