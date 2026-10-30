using System.Reflection;

namespace RateComposition;

/// <summary>
/// Builds a <see cref="LoggingRateProxy"/> in front of a real
/// <see cref="IRateCalculator"/>, the one place that knows how to wire a
/// <see cref="DispatchProxy"/> target together after construction.
/// </summary>
public static class RateProxyFactory
{
    /// <summary>
    /// Wraps <paramref name="target"/> in a logging proxy. The returned
    /// <see cref="IRateCalculator"/> forwards every call to
    /// <paramref name="target"/>; <paramref name="invokedMembers"/> exposes
    /// the member names the proxy intercepted, in call order.
    /// </summary>
    public static IRateCalculator Wrap(IRateCalculator target, out IReadOnlyList<string> invokedMembers)
    {
        var proxy = DispatchProxy.Create<IRateCalculator, LoggingRateProxy>();
        var loggingProxy = (LoggingRateProxy)proxy;
        loggingProxy.Target = target;

        invokedMembers = loggingProxy.InvokedMembers;

        return proxy;
    }
}
