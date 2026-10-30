using System.Reflection;

namespace RateComposition;

/// <summary>
/// A runtime Proxy built with <see cref="DispatchProxy"/>: every call made
/// through the <see cref="IRateCalculator"/> reference it returns is
/// intercepted by <see cref="Invoke"/>, recorded in
/// <see cref="InvokedMembers"/>, and then forwarded to <see cref="Target"/>.
/// <see cref="DispatchProxy.Create{TInterface, TProxy}"/> requires a public
/// parameterless constructor on the proxy type, so <see cref="Target"/> is
/// assigned afterward by <see cref="RateProxyFactory.Wrap"/> rather than
/// through a constructor parameter.
/// </summary>
public class LoggingRateProxy : DispatchProxy
{
    private readonly List<string> invokedMembers = [];

    /// <summary>The real <see cref="IRateCalculator"/> every call is forwarded to.</summary>
    public IRateCalculator Target { get; set; } = null!;

    /// <summary>The name of every member invoked through this proxy, in call order.</summary>
    public IReadOnlyList<string> InvokedMembers => invokedMembers;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        invokedMembers.Add(targetMethod.Name);

        return targetMethod.Invoke(Target, args);
    }
}
