---
title: "The Options pattern"
order: 30
layout: tutorial
sectionid: docs
part: "platform-patterns"
description: "IOptions<T>, IOptionsSnapshot<T>, and IOptionsMonitor<T> for configuration binding and change notification."
---

<!-- EXAMPLE (shipping domain): Carrier gateway configuration: endpoints, timeouts, retry budgets. -->

## Learning objectives

By the end of this chapter you can:

- Bind a settable options class from a configuration section with `AddOptions<T>().Bind(...)`, and say why it needs a parameterless constructor.
- Choose between `IOptions<T>`, `IOptionsSnapshot<T>` and `IOptionsMonitor<T>` by lifetime and change visibility.
- React to a configuration reload in a singleton through `IOptionsMonitor<T>.OnChange`.
- Write an `IValidateOptions<T>` that reports every problem at once, and fail the host at startup with `ValidateOnStart()`.
- Recognize a setting that should stay a `const` or `readonly` field.

## Why this matters

Configuration changes while a service runs: a timeout tightened after an incident, a retry budget raised for a slow carrier, an endpoint moved. Reading `IConfiguration["CarrierGateway:Endpoint"]` at each call site gives no guarantee the key exists, parses as the right type, or is spelled the same everywhere. The Options pattern replaces the lookup with one typed class, bound once from a named section and injected as that type.

Binding fixes the shape. Two problems remain: a value can change after startup, and a value can be wrong. `IOptionsMonitor<T>` keeps a bound value current across reloads and notifies watchers. `IValidateOptions<T>` checks a bound value, and `ValidateOnStart()` runs the check while the host starts, so a malformed endpoint fails the deployment rather than the first request that reaches it.

{% include excalidraw.html name="30-the-options-pattern" caption="Figure 30.1: CarrierGatewayOptions binds once from configuration, then reaches callers three ways: a long-lived IOptions value, a per-request IOptionsSnapshot, and an IOptionsMonitor that tracks the current value across reloads." %}

## Binding a settable class from configuration

```csharp
public sealed class CarrierGatewayOptions
{
    public const string SectionName = "CarrierGateway";

    public string Endpoint { get; set; } = "";

    public int TimeoutSeconds { get; set; }

    public int RetryBudget { get; set; }
}
```

`SectionName` keeps the section name on the type instead of repeating a string at each registration. The class shape matters: the options factory creates the instance with its parameterless constructor and the binder then sets each property with a matching key. A positional record has no parameterless constructor, and resolving it as options throws `MissingMethodException: Cannot dynamically create an instance of type 'CarrierConfiguration.CarrierGatewayOptions'. Reason: No parameterless constructor defined.` That's why this type is a settable class where most value types are immutable records.

One method registers everything:

```csharp
public static IServiceCollection AddCarrierGateway(this IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<CarrierGatewayOptions>()
        .Bind(configuration.GetSection(CarrierGatewayOptions.SectionName))
        .ValidateOnStart();

    services.AddSingleton<IValidateOptions<CarrierGatewayOptions>, CarrierGatewayOptionsValidator>();
    services.AddSingleton<RetryBudgetWatcher>();

    return services;
}
```

`AddOptions<T>()` returns an `OptionsBuilder<T>`, and `Bind` names the section. Every consumer reads from this registration, so the section name, the validator and startup validation each appear once. Keys such as `CarrierGateway:RetryBudget` bind from an in-memory provider exactly as from a file or environment variables, with strings parsed into the property types.

## Three ways to read bound options

Binding happens once; the three interfaces differ only in lifetime and in when they see a change.

| Interface | Registered as | Value | Sees a reload |
| --- | --- | --- | --- |
| `IOptions<T>` | singleton | `Value`, computed on first use and cached | never |
| `IOptionsSnapshot<T>` | scoped | `Value`, recomputed once per scope | in the next scope |
| `IOptionsMonitor<T>` | singleton | `CurrentValue`, always the latest | immediately, plus `OnChange` |

- **`IOptions<T>`** returns the same instance on every resolve, from any lifetime. It suits a client wrapper or worker built once at startup that never needs a new value.
- **`IOptionsSnapshot<T>`** gives each scope, typically each request, the configuration as it stood when the scope began. Change the retry budget and call `configuration.Reload()`, and the next scope sees the new value while the current one keeps the old.
- **`IOptionsMonitor<T>`** matches `IOptions<T>` until a reload, then reports the new value on every read of `CurrentValue`.

## Change notification after a reload

A singleton that reads its configuration once can't learn of a change without being rebuilt. `RetryBudgetWatcher` subscribes once, at construction:

```csharp
public sealed class RetryBudgetWatcher : IDisposable
{
    private readonly IDisposable? _subscription;
    private readonly List<CarrierGatewayOptions> _observed = [];
    private CarrierGatewayOptions _current;

    public RetryBudgetWatcher(IOptionsMonitor<CarrierGatewayOptions> monitor)
    {
        _current = monitor.CurrentValue;
        _subscription = monitor.OnChange(updated =>
        {
            _current = updated;
            _observed.Add(updated);
        });
    }

    public IReadOnlyList<CarrierGatewayOptions> Observed => _observed;

    public int CurrentRetryBudget => _current.RetryBudget;

    public void Dispose() => _subscription?.Dispose();
}
```

`OnChange` returns an `IDisposable` for the subscription; disposing it in `Dispose` stops the callback from outliving the watcher. Change the retry budget from 3 to 9 and call `configuration.Reload()`, and the same watcher instance reports 9, with the new value recorded in `Observed`. Nothing is rebuilt and no new scope is created. A gateway client can retry a flaky carrier more or less aggressively without a deployment.

{% include excalidraw.html name="30-options-change-notification" caption="Figure 30.2: When configuration reloads, CarrierGatewayOptions rebinds and RetryBudgetWatcher is notified immediately. A validator wired in at startup fails the host before any of this runs against bad config." %}

## Validating before anything can go wrong

An empty endpoint, a zero timeout and a negative retry budget all bind without complaint; the binder only matches keys and parses strings. Validation is its own interface:

```csharp
public sealed class CarrierGatewayOptionsValidator : IValidateOptions<CarrierGatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, CarrierGatewayOptions options)
    {
        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint) ||
            !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            reasons.Add($"{nameof(CarrierGatewayOptions.Endpoint)} must be a non-empty absolute URI.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            reasons.Add($"{nameof(CarrierGatewayOptions.TimeoutSeconds)} must be greater than zero.");
        }

        if (options.RetryBudget < 0)
        {
            reasons.Add($"{nameof(CarrierGatewayOptions.RetryBudget)} must not be negative.");
        }

        return reasons.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(reasons);
    }
}
```

The validator checks every rule before returning, so one failed result lists every bad setting.

A registered validator still runs only when something resolves the options. `ValidateOnStart()` registers an `IStartupValidator` that the host calls while starting, before it accepts requests. With all three settings malformed, `IStartupValidator.Validate()` throws `OptionsValidationException`, and its `Failures` name `Endpoint`, `TimeoutSeconds` and `RetryBudget` together. A deployment pipeline sees every problem in one message instead of one per failing request.

{% include excalidraw.html name="30-options-validation-on-start" caption="Figure 30.3: CarrierGatewayOptionsValidator collects every failure reason instead of stopping at the first, and ValidateOnStart fails the host at startup, before any caller can use the bad configuration." %}

## When not to use it

- **A fixed value with one reader.** A base currency code that never changes and is read in one place is a `const string` or `static readonly` field. Options pay off when a value changes without a deployment, has several consumers, or needs validation.
- **Change tracking nothing uses.** A consumer that wants its configuration fixed for its lifetime should take `IOptions<T>`. Subscribing to `OnChange` for a value no source ever reloads leaves a subscription to dispose for a callback that never fires.
- **Options outside the startup path.** Here `ValidateOnStart()` runs the validator while the host starts. Bind and validate a type with plain `.Validate(...)` and no `ValidateOnStart()`, behind a rarely used feature, and validation waits until the first resolve, after the host is already taking traffic. Chain `ValidateOnStart()` on every options type whose failure should stop a deployment.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a configuration setting.** "Add a retry budget setting" leaves open whether to read a key inline or bind a type, when to validate, and whether anything reacts to change. State it:

> Add `RetryBudget` as an `int` property on `CarrierGatewayOptions`, bound from the `CarrierGateway` section through `AddOptions<CarrierGatewayOptions>().Bind(...).ValidateOnStart()`. Reject a negative value in `CarrierGatewayOptionsValidator`. Inject `IOptionsMonitor<CarrierGatewayOptions>` only in the singleton that must react to a reload; use `IOptions<T>` elsewhere.

If the reply parses `configuration["CarrierGateway:RetryBudget"]` inline, ask where a bad value fails.
</blockquote>

## Build, run, observe

```sh
cd examples/30-the-options-pattern
dotnet test
```

All 12 tests pass, with configuration from the in-memory provider and no file, environment variable or host; `scripts/verify.sh` runs them with every other example. One experiment:

- **Make the options a positional record.** Replace `CarrierGatewayOptions` with `sealed record CarrierGatewayOptions(string Endpoint, int TimeoutSeconds, int RetryBudget)`. The library builds, the tests that construct it with object initializers stop compiling, and every binding case fails at resolve with `MissingMethodException`.

## For deeper coverage

- Microsoft Learn, [Options pattern in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/options): `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>` and their lifetimes.
- Microsoft Learn, [Options validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation): `IValidateOptions<T>`, `ValidateOnStart()` and the `IStartupValidator` path.
- Microsoft Learn, [Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration): `IConfiguration`, the in-memory provider, and how a source signals a reload.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection): the singleton, scoped and transient lifetimes the three interfaces are built around.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
