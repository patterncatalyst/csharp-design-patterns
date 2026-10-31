using Microsoft.Extensions.Options;

namespace CarrierConfiguration;

/// <summary>
/// Watches the carrier gateway's retry budget across configuration reloads.
/// Subscribes to <see cref="IOptionsMonitor{TOptions}.OnChange"/> once, at
/// construction, and keeps every observed value so a test or a caller can
/// confirm a specific reload actually reached a long-lived singleton, not
/// just that the monitor's own <c>CurrentValue</c> moved.
/// </summary>
public sealed class RetryBudgetWatcher : IDisposable
{
    private readonly IDisposable? _subscription;
    private readonly List<CarrierGatewayOptions> _observed = [];
    private CarrierGatewayOptions _current;

    public RetryBudgetWatcher(IOptionsMonitor<CarrierGatewayOptions> monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);

        _current = monitor.CurrentValue;
        _subscription = monitor.OnChange(updated =>
        {
            _current = updated;
            _observed.Add(updated);
        });
    }

    /// <summary>
    /// Every value this watcher has observed through <c>OnChange</c>, in the
    /// order the reloads happened. Does not include the value captured at
    /// construction time, only later changes.
    /// </summary>
    public IReadOnlyList<CarrierGatewayOptions> Observed => _observed;

    /// <summary>
    /// The most recently observed retry budget, updated at construction and
    /// again on every subsequent reload.
    /// </summary>
    public int CurrentRetryBudget => _current.RetryBudget;

    public void Dispose() => _subscription?.Dispose();
}
