---
title: "Singleton, Lazy<T>, and DI lifetimes"
order: 17
layout: tutorial
sectionid: docs
part: "creating-objects-under-control"
description: "Singleton as a lifetime registration rather than a class trick, and where Lazy<T> fits."
---

<!-- EXAMPLE (shipping domain): The tariff table: expensive to load, needed everywhere. -->

## Learning objectives

By the end of this chapter you can:

- Treat Singleton as a guarantee about how many instances exist, and name three C# mechanisms that supply it.
- Build the classic Singleton and explain what an explicit static constructor does to `beforefieldinit`.
- Defer an expensive load with `Lazy<T>` and `LazyThreadSafetyMode.ExecutionAndPublication` so it runs once under concurrent first access.
- Predict whether two resolutions return the same object under `AddSingleton`, `AddScoped` and `AddTransient`.
- Catch a captive dependency with `ServiceProviderOptions { ValidateScopes = true }`.

## Why this matters

A tariff table lists the per-kilogram rate for every lane the network prices. Loading it is expensive: a call to a rates service or a parse of a large rate sheet. Reading it is cheap: a dictionary lookup by origin and destination. A rate-quoting endpoint, a billing job and an invoice renderer all need the same rates, and none should pay for its own load or work from a different table.

The GoF Singleton names this: a class ensures exactly one instance of itself and provides a well-known access point. The intent holds; the mechanism has moved. `Lazy<T>` defers and synchronizes the expensive step with no static field, and the DI container's singleton lifetime gives the same guarantee from one registration line with no synchronization code. Below, the same table is built all three ways.

{% include excalidraw.html name="17-singleton-lazy-and-di-lifetimes" caption="Figure 17.1: Three ways to guarantee one shared TariffTable: a classic private-constructor Singleton, a Lazy-wrapped instance, and a DI container singleton registration. A counting tariff source proves each one loads the table exactly once." %}

## Pattern: Singleton

### The problem

Every tier answers lookups against three fixed lanes, each a `sealed record TariffEntry(string Origin, string Destination, decimal RatePerKilogram)`: 4.00 per kilogram from Rotterdam to Santos, 6.50 from Rotterdam to Shanghai, and 5.25 from Santos to Shanghai. The data comes from one interface, with a `LoadCount` that records how many times `Load` ran:

```csharp
public interface ITariffSource
{
    IReadOnlyList<TariffEntry> Load();
    int LoadCount { get; }
}
```

`CountingTariffSource` implements it and increments `LoadCount` with `Interlocked.Increment`, so the count is correct across threads. `TariffTable` calls `Load` once in its constructor and indexes the result; `Lookup` throws `KeyNotFoundException` for an unpublished lane instead of answering zero. The question is how many `TariffTable` instances an application ends up with and how many times `Load` runs to build them.

### The solution

A **Singleton** ensures a class has exactly one instance and provides a single point of access to it.

| Mechanism | Who enforces one instance | Substitutable in tests | Load timing |
| --- | --- | --- | --- |
| `ClassicTariffTable` | Private constructor and static field | No | First touch of the type |
| `LazyTariffTable` | `Lazy<T>`, per wrapper object | Yes, through the constructor | First `Table` read |
| `AddSingleton` registration | The DI container | Yes, through the registration | First resolution |

### How C# models it

The classic shape is a private constructor and a static access point:

```csharp
public sealed class ClassicTariffTable
{
    private static readonly ClassicTariffTable InstanceValue;

    private readonly TariffTable _table;

    static ClassicTariffTable()
    {
        InstanceValue = new ClassicTariffTable(new CountingTariffSource());
    }

    private ClassicTariffTable(ITariffSource source)
    {
        _table = new TariffTable(source);
    }

    public static ClassicTariffTable Instance => InstanceValue;

    public decimal Lookup(string origin, string destination) => _table.Lookup(origin, destination);
}
```

Nothing outside the type can call the constructor, and the `static readonly` field never changes after assignment. The explicit static constructor matters. A type with only static field initializers gets the `beforefieldinit` flag, which lets the runtime run them at any point before first use, possibly earlier than expected. An explicit static constructor suppresses the flag and pins initialization to immediately before the first access to a static member. Every read of `Instance` returns the same object, and it answers 4.00 for Rotterdam to Santos.

The cost shows when a consumer needs a test double. `Instance` is a concrete static property on a sealed class, with no constructor a test can call to supply a fake `ITariffSource`. A class that calls `ClassicTariffTable.Instance.Lookup(...)` is wired to that one access point. The static constructor also runs the real source the first time anything touches the type, whether or not the caller needed a lookup. Recognize the shape in existing code; the next two mechanisms keep the guarantee without these costs.

### Lazy&lt;T&gt; for thread-safe deferred initialization

`LazyTariffTable` drops the static access point. It's an ordinary object, and each instance guarantees one load for itself:

```csharp
public sealed class LazyTariffTable
{
    private readonly Lazy<TariffTable> _lazyTable;

    public LazyTariffTable(ITariffSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _lazyTable = new Lazy<TariffTable>(() => new TariffTable(source), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public TariffTable Table => _lazyTable.Value;

    public decimal Lookup(string origin, string destination) => Table.Lookup(origin, destination);
}
```

The constructor loads nothing; `LoadCount` stays at zero until the first `Table` read, then stays at one through any number of reads. `Lookup("Santos", "Shanghai")` returns 5.25.

`ExecutionAndPublication` means that if two threads read `Value` before initialization completes, one runs the factory and the other blocks and receives the same result. Sixty-four parallel reads through `Parallel.For` still load once. A hand-rolled null check and assign has no such guarantee: two threads can both see `null`, both run the load, and the last assignment wins after both paid the full cost. `Lazy<T>` closes that race with no `lock` in `LazyTariffTable`.

### DI lifetimes: singleton, scoped, and transient

With the container, sharing is a property of the registration, not of the class:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTariffTables(this IServiceCollection services)
    {
        services.AddSingleton<ITariffSource, CountingTariffSource>();
        services.AddSingleton(provider => new TariffTable(provider.GetRequiredService<ITariffSource>()));

        return services;
    }
}
```

Neither `TariffTable` nor `CountingTariffSource` has a private constructor, a static field or a `Lazy<T>`. Two resolutions of `TariffTable` from one provider return the same object, and three resolutions leave `LoadCount` at one: the container builds the table on first request and hands back that instance afterward.

| Lifetime | Two resolves in one scope | One resolve in each of two scopes |
| --- | --- | --- |
| `AddSingleton` | Same object | Same object |
| `AddScoped` | Same object | Different objects |
| `AddTransient` | Different objects | Different objects |

The singleton registration does the job of both earlier mechanisms: one instance for the application, built only when first needed. `TariffTable`'s constructor takes an `ITariffSource` directly, so it's tested against a `CountingTariffSource` with no container at all.

{% include excalidraw.html name="17-di-lifetime-resolution" caption="Figure 17.2: A singleton resolution always returns the same TariffTable; transient and scoped registrations return new or per-scope instances. Resolving a singleton that depends on a scoped service throws instead of silently capturing it." %}

### The captive-dependency pitfall

A scoped service lives for one scope, such as one web request or one unit of work. If a singleton resolves it during construction and holds it, the scoped service is frozen at that state for the application's lifetime and never rebuilt. The [dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines) call this a captive dependency, one "meant to have a shorter lifetime" that gets "unintentionally captured by a longer-lived service."

Registering `ITariffSource` as scoped and `LazyTariffTable` as singleton reproduces it:

```csharp
var services = new ServiceCollection();
services.AddScoped<ITariffSource, CountingTariffSource>();
services.AddSingleton<LazyTariffTable>();

using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<LazyTariffTable>());
```

With `ValidateScopes = true`, resolving `LazyTariffTable` from the root provider throws `InvalidOperationException`, because [scope validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines) checks for "a scoped service being resolved from the root provider." Without it, the container resolves one `CountingTariffSource` at the root, hands it to `LazyTariffTable` for good, and never builds another however many scopes open. That silent version is worse: nothing looks wrong until someone notices that a per-request value never changes.

## When not to use it

- **Static Singleton.** Avoid it once more than one implementation could exist: a fake tariff source in a test, a flat sandbox rate, a second consumer with a different source. The static shape fixes one construction path when the static constructor runs. A DI singleton gives the same sharing and keeps the constructor open. Keep the static shape only where no container exists and nothing will need a substitute.
- **Lazy loading of a required dependency.** Both `LazyTariffTable` and the DI singleton load on first use. If the rates service is down, the failure lands on whichever request touches the table first, after the service has reported healthy and accepted traffic. For a table every request needs, load it at startup and fail the deployment. Lazy suits a dependency that's rarely needed, such as a background job that occasionally reads tariffs.

<blockquote class="ai-sidebar" markdown="1">
Ask an assistant for "a thread-safe singleton" in C# and a common answer is still double-checked locking: a nullable static field, a null check, a `lock`, a second check, then the assignment. It reimplements what `Lazy<T>` with `LazyThreadSafetyMode.ExecutionAndPublication` already does, and can carry ordering bugs if a field read is reordered relative to its write. The same prompt sometimes produces a static mutable Singleton for a cache, a clock or a configuration source that a test later needs to replace. Check a generated singleton with two questions: does `Lazy<T>` give the same guarantee with less code, and will a test, sandbox or second consumer ever need a different implementation? A DI singleton keeps that open; a static field closes it.
</blockquote>

## Build, run, observe

```sh
cd examples/17-singleton-lazy-and-di-lifetimes
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. All 15 tests pass, covering the base table, the classic instance, the `Lazy<T>` wrapper under 64-way parallel access, the three container lifetimes and the captive-dependency throw.

- **Turn off scope validation.** Build the captive-dependency provider without `ValidateScopes = true`. Resolution succeeds and the `Assert.Throws` fails.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the
  source for the Singleton pattern.
- Microsoft Learn: [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview)
  and [Service lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes),
  for how `AddSingleton`, `AddScoped`, and `AddTransient` behave, and
  [Dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines),
  for the captive-dependency failure mode and scope validation.
- [`Lazy<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1)
  and [`LazyThreadSafetyMode`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lazythreadsafetymode)
  in the API reference: what `ExecutionAndPublication` guarantees under
  concurrent first access.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
