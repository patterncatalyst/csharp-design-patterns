namespace CarrierDispatch.Tests;

/// <summary>
/// A hand-rolled, synchronous <see cref="IProgress{T}"/> test double. The
/// built-in <see cref="Progress{T}"/> marshals its callback onto a captured
/// <see cref="System.Threading.SynchronizationContext"/> asynchronously,
/// which races against the assertions in these tests; <see cref="Report"/>
/// here appends to <see cref="Reports"/> on the calling thread, immediately.
/// </summary>
public sealed class RecordingProgress : IProgress<DispatchProgress>
{
    private readonly List<DispatchProgress> _reports = [];

    public IReadOnlyList<DispatchProgress> Reports => _reports;

    public void Report(DispatchProgress value) => _reports.Add(value);
}
