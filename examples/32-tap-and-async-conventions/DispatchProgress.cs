namespace CarrierDispatch;

/// <summary>
/// A snapshot of how many requests in a batch have been dispatched so far.
/// </summary>
public sealed record DispatchProgress(int Completed, int Total);
