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

- Bind `CarrierGatewayOptions`, a settable class with a public parameterless
  constructor, from a `CarrierGateway` configuration section with
  `AddOptions<T>().Bind(...)`, and explain why the binder needs settable
  properties rather than a record's constructor-only initialization.
- Resolve the same bound options three different ways: `IOptions<CarrierGatewayOptions>`
  as a long-lived singleton value, `IOptionsSnapshot<CarrierGatewayOptions>`
  recomputed once per scope, and `IOptionsMonitor<CarrierGatewayOptions>` for
  its always-current `CurrentValue`, and say which lifetime each interface
  pairs with.
- Subscribe to `IOptionsMonitor<T>.OnChange` in `RetryBudgetWatcher` and
  observe a retry-budget update the moment an in-memory
  `IConfigurationRoot.Reload()` call runs, without restarting anything.
- Write `CarrierGatewayOptionsValidator` as an
  `IValidateOptions<CarrierGatewayOptions>` that collects every problem in
  one pass, and explain what `ValidateOnStart()` does with it through
  `IStartupValidator`.
- Recognize a configuration value that never changes and has exactly one
  reader, the case where a `const` or `readonly` field does the whole job and
  registering an options type adds ceremony with nothing behind it.

## Why this matters

Configuration changes while a service is running: a timeout gets tightened
after an incident, a retry budget gets raised for a slow carrier, an endpoint
moves. A service that reads `IConfiguration["CarrierGateway:Endpoint"]`
directly at the call site is reading a string out of a dictionary every time,
with no compile-time guarantee the key exists, the value parses as the right
type, or two call sites agree on what the key is even called. The Options
pattern turns that lookup into something an editor can check and a compiler
can enforce: one typed class, `CarrierGatewayOptions` in this chapter's
example, with a property per setting, bound once from a named configuration
section and handed to every consumer as that same type.

Binding solves the shape problem. It does not, by itself, solve the other
two problems that come with configuration that lives outside the running
process: a value can change after the service starts, and a value can be
wrong. .NET's options system answers both directly. `IOptionsMonitor<T>`
keeps a bound options value current across a reload and can tell a watcher
the instant the value changes, so a long-lived singleton never has to poll a
file or restart to pick up a new setting. `IValidateOptions<T>` runs rules
against a bound value before anything downstream touches it, and
`ValidateOnStart()` wires that validator into the host's own startup
sequence, so a carrier gateway with a malformed endpoint or a negative retry
budget fails the moment the service starts rather than the moment some
in-flight request first reaches the bad setting. Binding, change
notification, and startup validation are three distinct jobs, and the
Options pattern gives each one its own interface rather than asking one type
to do all three.

{% include excalidraw.html name="30-the-options-pattern" caption="Figure 30.1: CarrierGatewayOptions binds once from configuration, then reaches callers three ways: a long-lived IOptions value, a per-request IOptionsSnapshot, and an IOptionsMonitor that tracks the current value across reloads." %}

## Binding a settable class from configuration

The example behind this chapter lives in `examples/30-the-options-pattern`.
`CarrierGatewayOptions` holds the three settings a carrier gateway call
needs:

```csharp
public sealed class CarrierGatewayOptions
{
    public const string SectionName = "CarrierGateway";

    public string Endpoint { get; set; } = "";

    public int TimeoutSeconds { get; set; }

    public int RetryBudget { get; set; }
}
```

Two things about that declaration are not incidental. `SectionName` names
the configuration section this type binds from, `CarrierGateway`, as a
`const string` on the type itself rather than a magic string repeated at
every call site that registers it. And `CarrierGatewayOptions` is a
`sealed class` with settable properties and an implicit parameterless
constructor, not a record with a positional constructor. The configuration
binder in `Microsoft.Extensions.Configuration.Binder` builds an instance by
calling the parameterless constructor and then setting each property it
finds a matching key for, `Endpoint`, `TimeoutSeconds`, `RetryBudget`, one
assignment per bound key. A record's usual shape, an init-only property
populated through a positional constructor parameter, gives the binder
nothing to call: there is no constructor overload taking one argument per
configuration key, and `init` accessors are reachable from the object
initializer the binder would need to use, but only if the binder is written
to discover and invoke that pattern, which the configuration binder is not.
A settable class with ordinary `get; set;` properties is the shape the
binder is built around, and that is the entire reason `CarrierGatewayOptions`
is a class with mutable properties instead of the immutable record this book
reaches for everywhere else a value does not need to support post-construction
assignment.

`ServiceCollectionExtensions.AddCarrierGateway` is the one place this
binding happens:

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

`AddOptions<CarrierGatewayOptions>()` returns an `OptionsBuilder<CarrierGatewayOptions>`,
and `.Bind(configuration.GetSection(CarrierGatewayOptions.SectionName))`
tells that builder which section of `IConfiguration` supplies the values.
Every consumer that follows in this chapter, regardless of which interface it
asks for, reads from this one registration: there is exactly one place that
names the section, one place that wires the validator, and one place that
turns on startup validation. `OptionsBindingTests.Options_binds_endpoint_timeout_and_retry_budget_from_configuration`
builds an in-memory `IConfigurationRoot` through
`ServiceCollectionExtensions.BuildInMemoryConfiguration`, a dictionary of
string keys like `CarrierGateway:Endpoint` with no file or environment
variable behind it, registers `AddCarrierGateway` against that configuration,
and resolves `IOptions<CarrierGatewayOptions>` to assert the three properties
landed correctly, the endpoint as a URI string, the timeout and retry budget
as the integers the binder parsed out of configuration strings.

## Three ways to read bound options

Binding happens once, but .NET exposes the bound result through three
different interfaces, and the difference between them is entirely about
lifetime and change visibility, not about what gets bound.

`IOptions<CarrierGatewayOptions>` is the simplest of the three: its `Value`
property computes the bound options once, the first time anything resolves
it, and caches the result for the lifetime of the service provider.
`IOptions<T>` is itself registered as a singleton, so every caller that
resolves it, whether that caller is itself a singleton, a scoped service, or
a transient one, gets back the exact same `CarrierGatewayOptions` instance.
`OptionsBindingTests.IOptions_returns_the_same_singleton_instance` resolves
`IOptions<CarrierGatewayOptions>.Value` twice from the same provider and
asserts the two results are reference-equal with `Assert.Same`. That
behavior is the right fit for a long-lived singleton, a background worker or
a client wrapper constructed once at startup, that has no business watching
for runtime changes: it reads its configuration once and keeps using that
same value for as long as it lives.

`IOptionsSnapshot<CarrierGatewayOptions>` is scoped rather than singleton.
Its `Value` is recomputed once per scope, which in a web host lines up with
once per request, so a service that depends on
`IOptionsSnapshot<CarrierGatewayOptions>` sees whatever the configuration
held at the start of its own scope, not necessarily what `IOptions<T>` would
report for the whole process. `OptionsBindingTests.IOptionsSnapshot_rebinds_in_a_new_scope_after_a_reload`
shows the difference concretely: it reads the retry budget inside one scope,
changes the underlying in-memory configuration value and calls
`configuration.Reload()`, then opens a second scope and reads the retry
budget again, asserting the second scope sees the new value while nothing
about `IOptions<T>`'s singleton `Value` would have moved. `IOptionsSnapshot<T>`
is the right choice for a scoped or request-bound consumer that wants
current configuration without taking on the cost of watching for changes
itself.

`IOptionsMonitor<CarrierGatewayOptions>` is registered as a singleton, like
`IOptions<T>`, but unlike `IOptions<T>` its `CurrentValue` property is not
cached once and done: every read of `CurrentValue` reflects whatever the
most recent reload produced, and `IOptionsMonitor<T>` additionally exposes
`OnChange`, a subscription a caller can register to run a callback every
time the bound value changes. `OptionsBindingTests.IOptionsMonitor_current_value_reflects_the_bound_configuration`
confirms `CurrentValue.Endpoint` matches the configured endpoint, the same
assertion `IOptions<T>.Value` would satisfy on first read; the real
difference between the two only shows up after a reload, which is what
change notification is about.

## Change notification after a reload

A singleton that reads its configuration once at construction has no way to
learn a setting changed short of being rebuilt. `IOptionsMonitor<T>` solves
that without rebuilding anything: `RetryBudgetWatcher` subscribes to
`OnChange` exactly once, at construction, and keeps a record of every value
it has seen since:

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

`monitor.OnChange(...)` returns an `IDisposable` representing the
subscription itself, which `RetryBudgetWatcher` holds onto and releases in
its own `Dispose`, so the callback stops firing once the watcher is torn
down rather than outliving it. Every time the options system produces a new
`CarrierGatewayOptions` from a changed configuration source, the callback
runs with that new instance, updating `_current` and appending to `_observed`,
so a caller can distinguish the value captured at construction time from
every value a later reload delivered, instead of only the value captured
at construction.

`OptionsChangeAndValidationTests.IOptionsMonitor_OnChange_fires_with_the_new_value_after_a_reload`
resolves `RetryBudgetWatcher` from the container, changes the retry budget
in the underlying in-memory configuration from `3` to `9`, calls
`configuration.Reload()`, and then asserts two things: `watcher.Observed`
contains a value with `RetryBudget == 9`, and `watcher.CurrentRetryBudget`
itself reports `9`. Nothing in this test restarts the service provider,
reconstructs `RetryBudgetWatcher`, or creates a new scope. The same
long-lived singleton that was constructed before the reload reports the new
retry budget afterward, because `IOptionsMonitor<T>` is what reached into
the singleton's dependency and pushed the update through, not because the
singleton polled anything itself. A client wrapper that caches a carrier
gateway's retry budget this way can raise or lower how aggressively it
retries a flaky carrier without a deployment, and without ever losing track
of whichever value was in effect when a particular retry decision was made.

{% include excalidraw.html name="30-options-change-notification" caption="Figure 30.2: When configuration reloads, CarrierGatewayOptions rebinds and RetryBudgetWatcher is notified immediately. A validator wired in at startup fails the host before any of this runs against bad config." %}

## Validating before anything can go wrong

Binding a value out of configuration says nothing about whether that value
makes sense. An empty endpoint, a zero timeout, or a negative retry budget
all bind without complaint, because the binder's job stops at matching keys
to properties and parsing strings into the right types. `CarrierGatewayOptionsValidator`
is where the Options pattern separates that concern out into its own
interface, `IValidateOptions<T>`:

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

`Validate` checks every rule and appends a reason for each one that fails,
rather than returning on the first problem it finds, so `ValidateOptionsResult.Fail(reasons)`
can carry a complete list. `OptionsChangeAndValidationTests.Validator_rejects_invalid_configuration`
is a `Theory` over three malformed configurations, a blank endpoint, a
non-positive timeout, and a negative retry budget, each one asserted to
produce a failed result with at least one reason attached; `Validator_accepts_a_well_formed_configuration`
confirms the same validator returns `ValidateOptionsResult.Success` for
values that satisfy all three rules.

A validator that exists but is never asked to run only catches a problem if
something happens to call it. `ValidateOnStart()`, chained onto the same
`OptionsBuilder<CarrierGatewayOptions>` that binds the section in
`AddCarrierGateway`, registers an `IStartupValidator` with the container
that runs every `IValidateOptions<T>` registered for that options type
during the host's own startup sequence, before the host finishes starting
and begins accepting requests. `OptionsChangeAndValidationTests.ValidateOnStart_throws_for_invalid_configuration_at_startup`
resolves `IStartupValidator` directly, the same type a real host resolves
and calls internally during startup, builds a provider from configuration
that fails all three of the validator's rules at once, and asserts calling
`startupValidator.Validate()` throws an `OptionsValidationException` whose
`Failures` collection mentions `Endpoint`, `TimeoutSeconds`, and
`RetryBudget` by name, exactly the three reasons the validator would have
collected on its own. `ValidateOnStart_passes_for_valid_configuration` runs
the same path against well-formed values and asserts `startupValidator.Validate()`
raises nothing. A carrier gateway deployed with a malformed endpoint never
gets the chance to accept a shipment request and fail against that bad
setting mid-transaction: the host refuses to finish starting at all, and the
failure a deployment pipeline sees names every bad setting in one message
instead of surfacing one problem at a time as each path happens to be
exercised.

{% include excalidraw.html name="30-options-validation-on-start" caption="Figure 30.3: CarrierGatewayOptionsValidator collects every failure reason instead of stopping at the first, and ValidateOnStart fails the host at startup, before any caller can use the bad configuration." %}

## Build, run, observe

`CarrierGatewayOptions`, `CarrierGatewayOptionsValidator`,
`RetryBudgetWatcher`, and `ServiceCollectionExtensions` bind, read,
validate, and watch configuration entirely in memory, through
`Microsoft.Extensions.Configuration`'s in-memory provider and
`Microsoft.Extensions.DependencyInjection`'s service provider, so the suite
needs nothing beyond the local .NET 10 SDK, no file, no environment
variable, and no host. The example is standalone-testable from its own
directory:

```sh
cd examples/30-the-options-pattern
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 12
  failed: 0
  succeeded: 12
  skipped: 0
  duration: 850ms
```

The twelve cases split across the two test classes.
`OptionsBindingTests` proves the binding-and-lifetime half: a configuration
section binds correctly onto `CarrierGatewayOptions`, `IOptions<T>.Value`
returns the same singleton instance on every resolve, `IOptionsSnapshot<T>`
reflects current configuration within one scope and rebinds in a fresh scope
after a reload, and `IOptionsMonitor<T>.CurrentValue` matches the bound
configuration. `OptionsChangeAndValidationTests` proves the
change-and-validation half: `RetryBudgetWatcher` observes a reload through
`OnChange` on a singleton that was never reconstructed, the validator both
accepts well-formed configuration and rejects three distinct kinds of
malformed configuration with a reason attached to each, and `ValidateOnStart()`'s
`IStartupValidator` throws `OptionsValidationException` for invalid
configuration while raising nothing for valid configuration.

## When not to use it

A single setting with exactly one reader and no runtime change gets nothing
from being wrapped in an options type. If a carrier gateway's base currency
code were fixed for the life of the service and read from one place, a
`const string` or a `static readonly` field would name that value with less
code than registering an `AddOptions<T>()` pipeline, binding a section, and
resolving `IOptions<T>` to read one property. The Options pattern pays for
itself when a value can change independently of a deployment, when more than
one consumer needs the same typed value, or when the value needs validation
before anything acts on it; none of those conditions holds for a constant
that never moves and is only ever read in one place.

`IOptionsSnapshot<T>` and `IOptionsMonitor<T>` both add a layer between a
consumer and the value it reads, a layer that assumes the value might change
underneath the caller between reads. A consumer that wants
configuration fixed for its own lifetime, read once at construction
and never rechecked, gets a stale read at worst from `IOptions<T>` and no
benefit at all from the two interfaces built around change tracking. Reaching
for `IOptionsMonitor<T>` and subscribing to `OnChange` for a value that has
no code path that ever reloads it adds a subscription to manage and dispose
for a notification that will never fire.

`ValidateOnStart()` only protects a value that the host itself resolves
during startup. A value bound through `AddOptions<T>()` but never reached by
anything the host constructs eagerly, a type registered but resolved only
lazily behind a rarely used feature flag, will not be validated until
something finally asks for it, by which point the host has already finished
starting and accepted traffic. `ValidateOnStart()` closes the gap between
"configuration is malformed" and "a request discovers that fact," but only
for options types a startup path touches directly; an options type nothing
touches during startup validates on first use instead, later than the
failure this pattern is meant to catch.

<blockquote class="ai-sidebar" markdown="1">
Asked to add a new configuration setting, an AI pairing
partner will sometimes read it straight out of `IConfiguration` at the call
site with a string key, `configuration["CarrierGateway:RetryBudget"]`,
parsed inline with `int.Parse` or `Convert.ToInt32`, for a value that
`CarrierGatewayOptions` already models with a typed, bound property. That
shortcut skips validation entirely: a malformed value parses with an
exception at the moment some request first reaches that call site, rather
than failing the host at startup the way `ValidateOnStart()` does here. A
related suggestion reaches for `IOptionsMonitor<T>` and an `OnChange`
subscription on every injected options type by default, even one resolved
once by a transient service that is constructed fresh per call and never
outlives a single request, paying for change tracking a value that never
needs to change mid-request will never use. Check a generated answer
against what this chapter built: bind a typed options class instead of
reading configuration keys inline, reach for `IOptionsMonitor<T>` only when
something needs to react to a reload, and wire `ValidateOnStart()`
in wherever a bad value should stop a deployment rather than a request.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Options pattern in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/options): the authority for `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`, and the lifetime differences between them that this chapter's three consumption tests exercise.
- Microsoft Learn, [Options validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation): the authority for `IValidateOptions<T>`, `ValidateOnStart()`, and the `IStartupValidator` path `CarrierGatewayOptionsValidator` relies on to fail a host at startup.
- Microsoft Learn, [Configuration in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration): the authority for `IConfiguration`, the in-memory configuration provider `ServiceCollectionExtensions.BuildInMemoryConfiguration` builds on, and how a configuration source signals a reload.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection): the authority for the singleton, scoped, and transient service lifetimes that `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>` are each built around.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
