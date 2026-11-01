namespace StowageCoordination;

/// <summary>
/// The durability port: a reservation is not confirmed until it is appended
/// to the ledger. The append is asynchronous, which is what drives every
/// coordination stage in this example past a synchronous lock.
/// </summary>
public interface IStowageLedger
{
    Task AppendAsync(SlotAssignment assignment, CancellationToken ct);
}
