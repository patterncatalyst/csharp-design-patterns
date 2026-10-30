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

- Explain why Singleton is a guarantee about how many instances exist and
  how they are reached, not a technique tied to any one class shape, and
  name three different mechanisms in modern C# that all supply it.
- Build the classic GoF Singleton, a private constructor and a static
  access point, and explain what an explicit static constructor does to
  the CLR's `beforefieldinit` optimization and why that matters for
  initialization order.
- Wrap an expensive load behind `Lazy<T>` with
  `LazyThreadSafetyMode.ExecutionAndPublication`, and prove with a
  counting collaborator that the wrapped factory runs at most once, even
  under concurrent first access from many threads.
- Register a dependency with `AddSingleton`, `AddScoped`, or
  `AddTransient` and predict, for each lifetime, whether two resolutions
  from the same provider return the same object.
- Recognize a captive dependency, a longer-lived service holding a
  shorter-lived one, and explain how `ServiceProviderOptions
  { ValidateScopes = true }` turns that mistake into an
  `InvalidOperationException` at resolution time instead of a silent bug.

## Why this matters

A tariff table lists the per-kilogram rate for every lane a shipping
network prices: Rotterdam to Santos, Rotterdam to Shanghai, Santos to
Shanghai, and every other published route a rate quote, an invoice, or a
customs declaration might need to look up. Loading that table is the
expensive part. A real tariff source calls a rates service over the
network or parses a large published rate sheet, and neither operation is
something a rate-quoting endpoint, a billing job, and an invoice renderer
should each pay for separately when all three need the exact same rates.
The table itself, once loaded, is cheap: a dictionary keyed by origin and
destination, read with a lookup that either returns a rate or tells the
caller the lane was never published. The entire design problem this
chapter works through is getting from one expensive load to many cheap
reads, shared correctly across every part of an application that needs a
rate, without reloading the table for each one and without two different
parts of the same process somehow working from two different tables.

That problem is older than dependency injection, older than `Lazy<T>`, and
older than C#. The Gang of Four's Singleton pattern names it directly: a
class responsible for ensuring exactly one instance of itself exists, with
a well-known access point for reaching it. The pattern's intent has held
up decades after its original 1994 description; what has changed is the
mechanism. Where Singleton originally meant a specific class shape, a
private constructor and a static field, modern C# answers the same
intent, exactly one instance, reached safely from anywhere that needs it,
with a widening set of tools: `Lazy<T>` defers and synchronizes the
expensive step without a static field anywhere in sight, and the built-in
dependency injection container's singleton lifetime gets the same
guarantee from a single registration line, with no hand-written
synchronization at all. This chapter builds the same tariff table three
ways, the classic static shape, a `Lazy<T>`-wrapped instance, and a
container-managed singleton, so the guarantee each one makes, and the
guarantee each one fails to make, is visible side by side rather than
taken on faith.

{% include excalidraw.html name="17-singleton-lazy-and-di-lifetimes" caption="Figure 17.1: three ways to guarantee one shared TariffTable: ClassicTariffTable's private constructor and static Instance property, LazyTariffTable's Lazy<T> built with ExecutionAndPublication, and the DI container's AddSingleton registration resolving ITariffSource and TariffTable through GetRequiredService; a CountingTariffSource behind all three proves the load runs exactly once." %}

## Singleton as a lifetime, not a class trick

The example behind this chapter lives in
`examples/17-singleton-lazy-and-di-lifetimes`. Every tier in it answers
lookups against the same three fixed lanes, each a
`sealed record TariffEntry(string Origin, string Destination, decimal RatePerKilogram)`:
4.00 per kilogram from Rotterdam to Santos, 6.50 from Rotterdam to
Shanghai, and 5.25 from Santos to Shanghai. `ITariffSource` is the one
thing all three tiers depend on to get that data, with a single `Load`
method and a `LoadCount` property that exists purely so the example can
prove how many times `Load` actually ran:

```csharp
public interface ITariffSource
{
    IReadOnlyList<TariffEntry> Load();
    int LoadCount { get; }
}
```

`CountingTariffSource` implements it over the fixed three-lane set, and
increments `LoadCount` with `Interlocked.Increment` on every call so the
count stays correct even when `Load` runs from more than one thread.
`TariffTable` is the already-loaded, queryable result: its constructor
takes an `ITariffSource`, calls `Load` exactly once, and indexes the
result into a dictionary keyed by origin and destination, so every
`Lookup` after construction is a plain dictionary read that throws
`KeyNotFoundException` for an unpublished lane rather than silently
answering zero. Everything from here on is about how many `TariffTable`
instances a running application ends up with and how many times
`ITariffSource.Load` runs to build them.

The textbook GoF shape for guaranteeing exactly one is `ClassicTariffTable`,
and it is worth building once, by hand, before reaching for anything else:

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

Three things make this the classic shape rather than an ordinary class.
The constructor is private, so nothing outside this type can call `new
ClassicTariffTable(...)`; the only way to get an instance is through the
static `Instance` property. The backing field is `static readonly`, so
once assigned it never changes for the lifetime of the process. And the
static constructor is written out explicitly, even though its body does
nothing more than assign a field the declaration already marked `static
readonly`. That third detail is a CLR optimization that matters more than
it looks. A type with no explicit static constructor and only static field
initializers gets the `beforefieldinit` flag, which tells the runtime it
may run those initializers at any point before the type's first use,
including earlier than a developer might expect if another static member
on the same type is touched first. Writing an explicit static constructor,
even an apparently redundant one, suppresses `beforefieldinit` and pins
initialization to happen lazily, immediately before the first access to
any static member of the type. `ClassicTariffTableTests.Instance_is_reference_equal_across_calls`
confirms the payoff directly: two separate reads of `ClassicTariffTable.Instance`
return the same object, verified with `Assert.Same`, and
`Instance_looks_up_a_published_lane` confirms that shared instance answers
a real lookup, 4.00 for Rotterdam to Santos, the same number any other
tier in this example produces from the same fixed data.

What this shape costs becomes visible the moment a consumer of
`ClassicTariffTable` needs a test double instead of the real, loaded
table. Nothing about `Instance` is an interface; it is a concrete static
property returning a concrete sealed class, with no constructor a test can
call to supply a fake `ITariffSource`. A class that calls
`ClassicTariffTable.Instance.Lookup(...)` directly has hard-wired itself to
exactly that one static access point, and testing it in isolation means
either accepting the real loaded table as a fixed input to every test or
reaching for a technique heavier than the problem deserves, like a seam
injected purely to let a test substitute something else. The static
constructor also runs the real `CountingTariffSource` unconditionally, the
first time anything touches the type, which means the expensive load this
whole example is built around happens whether or not a given test run
actually needs a tariff lookup. Neither limitation makes
`ClassicTariffTable` wrong to understand; the shape is the historical
baseline the next two sections measure against, and recognizing it in an
unfamiliar codebase, a private constructor paired with a static accessor,
is recognizing the same guarantee built two more ways below, with
different tradeoffs each time.

## Lazy&lt;T&gt; for thread-safe deferred initialization

`LazyTariffTable` keeps the same guarantee, one load no matter how many
times the table is read, but drops the static access point entirely. It
is an ordinary object: nothing stops a caller from constructing two of
them, each independently satisfying the single-load guarantee for itself.
What makes that guarantee hold is `Lazy<T>`:

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

The constructor does no loading at all; it only captures `source` in a
closure and hands that closure to a `Lazy<TariffTable>`. Nothing runs
until something reads `Table`, which forwards to `_lazyTable.Value`. The
first read anywhere triggers the factory, which constructs a `TariffTable`
from the captured source, and every read after that, from any caller,
returns the same already-built `TariffTable` without touching the factory
again. `LazyTariffTableTests.Does_not_load_the_source_until_the_table_is_first_accessed`
proves the deferred half of that directly: a freshly constructed
`LazyTariffTable` has a `CountingTariffSource` sitting at `LoadCount` of
zero until the test reads `lazyTable.Table` once, after which `LoadCount`
is exactly one. `Repeated_access_loads_the_source_exactly_once` reads
`Table` five times in a loop and still finds `LoadCount` at one, confirming
the cached half of the guarantee.

The explicit `LazyThreadSafetyMode.ExecutionAndPublication` argument is
the detail that separates this from a naive null-check-and-assign pattern
a developer might reach for without `Lazy<T>` at all. `ExecutionAndPublication`
tells `Lazy<T>` that if two threads call `Value` before initialization
completes, only one of them actually runs the factory; the other blocks
until the first finishes and then receives the same result, rather than
both racing to build and publish their own competing `TariffTable`
instances. `LazyTariffTableTests.Concurrent_access_from_many_threads_still_loads_exactly_once`
exercises exactly that race with `Parallel.For(0, 64, index => _ = lazyTable.Table)`,
sixty-four iterations hitting `Table` with real thread-pool parallelism,
and still asserts `LoadCount` equal to one afterward. A hand-rolled
substitute for this, checking a nullable field for `null` and assigning it
if so, has no such guarantee: two threads can both observe `null`, both
run the expensive load, and both assign the field, with whichever
assignment lands last silently winning and the other load's result thrown
away after paying its full cost. `Lazy<T>` with `ExecutionAndPublication`
closes that race without a single `lock` statement written in
`LazyTariffTable` itself. `Looks_up_the_published_rate_once_loaded` closes
the loop by asserting `lazyTable.Lookup("Santos", "Shanghai")` returns
5.25, the same figure every other tier in this example produces from the
identical fixed data.

## DI lifetimes: singleton, scoped, and transient

Both `ClassicTariffTable` and `LazyTariffTable` solve sharing at the level
of one object deciding, on its own, how many times it initializes itself.
The built-in dependency injection container answers the same question
differently: sharing becomes a property of how a dependency is
registered, not something any class has to implement for itself.
`ServiceCollectionExtensions.AddTariffTables` is the one composition root
in this example that wires up the tariff table through the container:

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

Neither `TariffTable` nor `CountingTariffSource` carries a private
constructor, a static field, or a `Lazy<T>` wrapper anywhere in its own
code. The sharing guarantee comes entirely from the word `AddSingleton`
at the registration call. `ServiceLifetimeTests.AddTariffTables_resolves_the_same_TariffTable_instance_every_time`
resolves `TariffTable` twice from the same built provider with
`GetRequiredService<TariffTable>()` and asserts the two results are
`Assert.Same`, the identical object. `AddTariffTables_loads_the_source_exactly_once_across_resolutions`
resolves `TariffTable` three times and then checks the registered
`ITariffSource`, cast back to `CountingTariffSource`, has a `LoadCount` of
exactly one: the container builds the `TariffTable` once, the first time
anything asks for it, and every later resolution, anywhere in the
application, hands back that same built instance with no second call to
`Load`.

`AddSingleton` is one of three lifetimes the container ships, and this
example proves the other two directly rather than describing them only in
prose. A transient registration rebuilds its target on every resolve:
`ServiceLifetimeTests.A_transient_registration_returns_a_different_instance_each_resolve`
registers `CountingTariffSource` with `AddTransient` and asserts two
resolutions from the same provider are `Assert.NotSame`, two distinct
objects. A scoped registration sits between the two: one instance per
scope, shared within that scope and distinct across scopes.
`A_scoped_registration_returns_the_same_instance_within_one_scope`
registers the same type with `AddScoped`, creates one scope with
`provider.CreateScope()`, and resolves twice from that one scope's own
`IServiceProvider`, getting `Assert.Same` back. `A_scoped_registration_returns_a_different_instance_across_scopes`
creates two separate scopes and resolves once from each, asserting
`Assert.NotSame` between them. Three lifetimes, three different answers to
"does the next resolution get the same object," chosen per registration
rather than baked into the registered class.

Registering `TariffTable` as a singleton is doing the same job
`ClassicTariffTable`'s private constructor and static field did, sharing
one instance across an entire application, and the same job
`LazyTariffTable`'s `Lazy<T>` field did, deferring the expensive load until
something actually needs the table. The container's singleton lifetime
gets both at once, from one line in a composition root, with no private
constructor, no static field, and no `Lazy<T>` anywhere in `TariffTable`
itself. That shift changes what the class under test looks like:
`TariffTable`'s own constructor takes an `ITariffSource` directly, with
none of the three mechanisms visible inside the type.
`TariffTableTests.Looks_up_the_published_rate_for_a_known_lane` and its
neighbors construct a `TariffTable` against a real `CountingTariffSource`
with no container and no scaffolding to work around, while the sharing
guarantee production code relies on lives entirely in the registration,
not the type.

{% include excalidraw.html name="17-di-lifetime-resolution" caption="Figure 17.2: A singleton resolution returns the identical TariffTable every time; a transient CountingTariffSource returns a new instance every call; a scoped CountingTariffSource returns the same instance within one scope and a different one in the next. Registering ITariffSource as scoped under a singleton LazyTariffTable and building the provider with ValidateScopes = true turns that captive dependency into an InvalidOperationException at resolution time, with LoadCount never exceeding one." %}

## The captive-dependency pitfall

A singleton that depends on something shorter-lived is the specific
lifetime mistake the container is built to catch rather than merely
describe. A scoped service is meant to live for the length of one scope,
one web request, one unit of work, and to be discarded and rebuilt for
the next one. If a singleton resolves that scoped service once, during
its own construction, and holds onto the result for the application's
entire lifetime, the scoped service stops behaving like a scoped service
at all: it becomes a captive of the singleton holding it, frozen at
whatever state it had the moment the singleton first resolved it, never
rebuilt for any later scope. The [dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
describe this directly as a captive dependency, a dependency "meant to
have a shorter lifetime" that gets "unintentionally captured by a
longer-lived service."

This example proves the container catches that mistake rather than
silently allowing it. `ServiceLifetimeTests.A_singleton_depending_on_a_scoped_service_throws_when_resolved_from_the_root_provider`
registers `ITariffSource` as scoped and `LazyTariffTable` as singleton,
the exact shape of the pitfall, a longer-lived singleton sitting on top of
a shorter-lived scoped dependency:

```csharp
var services = new ServiceCollection();
services.AddScoped<ITariffSource, CountingTariffSource>();
services.AddSingleton<LazyTariffTable>();

using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<LazyTariffTable>());
```

`ServiceProviderOptions { ValidateScopes = true }` is what turns this from
a silent bug into an immediate, loud failure. Resolving
`LazyTariffTable` straight from the root provider, never from inside a
scope, throws `InvalidOperationException` the moment the container tries
to satisfy `LazyTariffTable`'s constructor dependency on the scoped
`ITariffSource`, because [scope validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
specifically checks for "a scoped service being resolved from the root
provider," which is exactly what constructing a singleton at the root
would otherwise do quietly. Without `ValidateScopes` turned on, the same
registration would not throw; it would simply resolve the scoped
`CountingTariffSource` once, at root level, hand it to `LazyTariffTable`
forever, and never create a second one, no matter how many scopes the
application later opens. That silent version of the bug is strictly
worse than the loud one this test exercises, because nothing about it
looks wrong until a reader notices that a value that was supposed to
change per request never does.

## Build, run, observe

Every command below runs with the local .NET 10 SDK; nothing in this loop
needs a container. The example is standalone-testable from its own
directory:

```sh
cd examples/17-singleton-lazy-and-di-lifetimes
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. The suite splits across four files: three
`TariffTableTests` cases proving the base `TariffTable` type looks up a
published lane, throws on an unpublished one, and loads its source
exactly once from its own constructor; two `ClassicTariffTableTests`
cases proving the classic static `Instance` is reference-equal across
calls and answers a real lookup; four `LazyTariffTableTests` cases
proving `LazyTariffTable` defers loading until first access, loads at
most once across repeated reads, loads at most once under sixty-four-way
parallel access, and answers lookups correctly once loaded; and six
`ServiceLifetimeTests` cases covering the container's own singleton
guarantee, transient and scoped lifetimes, and the captive-dependency
throw. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 15
  failed: 0
  succeeded: 15
  skipped: 0
  duration: 1s 790ms
```

## When not to use it

A static Singleton, the `ClassicTariffTable` shape, is the wrong reach the
moment more than one implementation of the thing it wraps could plausibly
exist: a test that wants a fake tariff source instead of the real one, a
sandbox environment that needs a flat test rate, or a second consumer that
would benefit from substituting something else behind the same lookup.
None of those are reachable through a private constructor and a static
`Instance` property, because the static shape bakes in exactly one
construction path, chosen once, at the moment the static constructor
runs, with no seam anywhere for a caller to supply a different
`ITariffSource`. A DI singleton registration gives the identical sharing
guarantee, one instance for the life of the application, while keeping
the constructor open: `AddTariffTables` can be edited to register a
different `ITariffSource` implementation, or a test can build its own
`ServiceCollection` with a substitute source, without touching
`TariffTable` itself. Reach for the DI singleton lifetime by default for
anything registered in a composition root already built around the
container, and keep the hand-written static shape only for the rare case
where no container exists, nothing will ever need a substitute, and the
type's own file is the right place to own its lifetime.

The choice between eager and lazy initialization matters less for
correctness than for when a failure surfaces. A tariff source that might
fail to load, a rates service that is down, a tariff file that is missing
from disk, fails at a very different moment depending on whether loading
happens eagerly, at application startup, or lazily, on first use. Eager
loading at startup means a broken rates service fails the deployment
immediately, before any traffic is served, which is usually what a team
wants for a dependency every request needs. Lazy loading defers that same
failure until whichever request happens to be the first one to touch the
table, which can mean a service reports itself healthy, accepts traffic,
and then fails on an unlucky customer's request instead of failing loudly
at startup. `LazyTariffTable` and the DI singleton registration in this
example both default to lazy: nothing loads until the first
`Table` read or the first `GetRequiredService<TariffTable>()` call.
That default is right when the cost of loading rarely needs to be paid at
all, a background job that only occasionally needs tariff data, but wrong
for a service whose entire purpose depends on the table being ready, where
loading it during startup and failing fast if it cannot load is the safer
shape.

> **AI sidebar.** Ask an AI pairing partner for "a thread-safe singleton"
> in C# and a common answer is still the double-checked-locking pattern
> from well before `Lazy<T>` existed in the base class library: a nullable
> static field, a null check, a `lock` statement, a second null check
> inside the lock, then the assignment. That shape compiles and usually
> works, but it reimplements exactly what `Lazy<T>` with
> `LazyThreadSafetyMode.ExecutionAndPublication` already does, tested and
> maintained as part of the runtime, with none of the subtle ordering
> bugs a hand-rolled double check can introduce if a field read gets
> reordered relative to its write. The same prompt pattern sometimes
> produces a static-mutable Singleton for a type that a test later needs
> to substitute, a cache, a clock, a configuration source, with no
> constructor parameter anywhere for a test to inject a fake. Check a
> generated singleton against two questions this chapter answers directly:
> does `Lazy<T>` already give the same guarantee with far less code to
> review, and will any test, sandbox, or second consumer ever need to
> supply something other than the one baked-in implementation, which is
> exactly what a DI singleton registration keeps open and a static field
> forecloses.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the
  source for the Singleton pattern this chapter carries from a private
  static field to a `Lazy<T>` wrapper to a container-managed lifetime.
- The official Microsoft Learn documentation cited throughout this
  chapter: [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview)
  and [Service lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes),
  the primary source for how `AddSingleton`, `AddScoped`, and
  `AddTransient` behave, and [Dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines),
  the source for the captive-dependency failure mode and scope
  validation this chapter's last test proves directly.
- [`Lazy<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1)
  and [`LazyThreadSafetyMode`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lazythreadsafetymode)
  in the API reference: the source for what `ExecutionAndPublication`
  guarantees under concurrent first access, proven directly by
  `LazyTariffTableTests` in the runnable example.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
