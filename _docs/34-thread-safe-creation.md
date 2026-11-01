---
title: "Thread-safe creation"
order: 34
layout: tutorial
sectionid: docs
part: "concurrency-patterns"
description: "Lazy<T>, a thread-safe Singleton, and why Double-Checked Locking is now a historical note."
---

<!-- EXAMPLE (shipping domain): The tariff table again, now under concurrent first use. -->

## Learning objectives

- Build an expensive shared object exactly once under concurrent first use with `Lazy<T>` in `LazyThreadSafetyMode.ExecutionAndPublication`.
- Write the same guarantee by hand with a `System.Threading.Lock`-guarded field, and explain why that path only works for synchronous creation.
- Use a static field initializer as the simplest thread-safe singleton, and state the runtime guarantee it relies on.
- Recognize and reason about the `Lazy<T>` exception-caching behavior: a failed build is remembered and rethrown rather than retried.
- Place Double-Checked Locking as a historical idiom that `Lazy<T>` supersedes, not as a pattern to write today.

## Why this matters

A shipping platform builds its tariff table once and hands the same table to every caller after that. The table itself is cheap to read from, a dictionary lookup by lane code, but building it is not: it means parsing a published rate sheet, validating lane codes, and assembling a data structure a fleet of request handlers all want at the same time. If the first request to arrive during a cold start pays that cost, that is fine. If sixteen requests arrive at once during that same cold start and each one builds its own copy of the table, that is sixteen redundant parses, sixteen allocations the garbage collector has to clean up, and in the worst case sixteen slightly different tables if the underlying rate feed is not perfectly stable between calls.

This is a creation problem, not a usage problem. Once the table exists, reading `RatePerKilogram` is ordinary, side-effect-free work that many threads can do at once without coordination. The hazard lives entirely at the moment of construction: the first touch, the race to decide who builds, and the need for every other caller to wait for and then share that one build rather than run their own. A shipping platform that gets this wrong under load does not crash, it just wastes work and, if the builder has side effects such as incrementing a counter or calling an external feed, produces answers that depend on how many threads happened to race at once rather than on the data itself.

The example (`ThreadSafeTariffs`) isolates exactly that moment. `ITariffBuilder` is the seam: `StandardTariffBuilder` does the real work of seeding `TariffTable` with the `STD` and `EXP` lane rates, `CountingTariffBuilder` wraps it to prove how many times the real build ran, and `ThrowingTariffBuilder` stands in for a feed that fails. Three providers sit in front of that seam and publish a single `TariffTable` to every caller: `LazyTariffProvider`, `LockTariffProvider`, and the static `TariffRegistry`. All three solve the same problem. The interesting part of the chapter is in how each one solves it, what guarantee it gives you, and which one to reach for first.

{% include excalidraw.html name="34-thread-safe-creation" caption="Figure 34.1: Sixteen concurrent readers call LazyTariffProvider.Value at once, but CountingTariffBuilder proves the underlying build runs exactly once and every reader gets the same table." %}

## The default: `Lazy<T>` with `ExecutionAndPublication`

`LazyTariffProvider` wraps a `Lazy<TariffTable>` around whatever `ITariffBuilder` it receives:

```csharp
public sealed class LazyTariffProvider(ITariffBuilder builder)
{
    private readonly Lazy<TariffTable> _lazy = new(builder.Build, LazyThreadSafetyMode.ExecutionAndPublication);

    public TariffTable Value => _lazy.Value;

    public bool IsValueCreated => _lazy.IsValueCreated;
}
```

Nothing runs until the first call to `Value`. `IsValueCreated` lets a caller ask whether that first touch has happened yet without forcing it, which is useful for diagnostics or for a health check that wants to report "not yet warmed up" rather than paying for a build it does not need. The test `Lazy_provider_does_not_build_until_the_value_is_touched` pins exactly this: `IsValueCreated` is false and `CountingTariffBuilder.BuildCount` is zero before anyone reads `Value`, and both flip the first time something does.

The mode argument is the whole point. `LazyThreadSafetyMode.ExecutionAndPublication` tells `Lazy<T>` that only one thread may ever run the factory delegate, and every other thread that arrives while that run is in progress blocks until it finishes and then receives the same published result rather than running the factory itself. This is the mode `Lazy<T>` defaults to when you construct it with a parameterless constructor or with just a factory delegate, and it is the mode this example picks explicitly so the guarantee is visible in the code rather than inherited silently. `CountingTariffBuilder` makes the guarantee checkable: it increments an `Interlocked` counter inside `Build`, so `BuildCount` tells you, after the fact, exactly how many times the real work ran.

The concurrency test releases sixteen `Task.Run` readers through a `Barrier` so they all call `provider.Value` as close to simultaneously as the scheduler allows, then asserts `counting.BuildCount` equals one and every result is the identical `TariffTable` reference with `Assert.Same`. That second assertion matters as much as the first. A count of one tells you the builder only ran once; sameness tells you the one table that got built is the table every caller received, not a copy or a different instance that happens to look equal. `TariffTable` relies on its default reference identity for exactly this reason: if it defined value equality instead, two equal-looking tables built by two racing threads would pass an `Assert.Equal` check while still proving nothing about whether the single-build guarantee held.

This is the provider to reach for first. It needs no explicit locking code, it works correctly whether `Value` is first touched from one thread or many, and the thread safety mode is a single constructor argument rather than something you have to get right by hand.

## Writing the same guarantee by hand with `Lock`

`LockTariffProvider` gives the identical external contract, build once, publish to everyone, but spells out the mechanism with a `System.Threading.Lock` field instead of delegating to `Lazy<T>`:

```csharp
public sealed class LockTariffProvider(ITariffBuilder builder)
{
    private readonly Lock _gate = new();
    private TariffTable? _table;

    public TariffTable Value
    {
        get
        {
            lock (_gate)
            {
                return _table ??= builder.Build();
            }
        }
    }
}
```

`System.Threading.Lock` is the dedicated mutual-exclusion type: the `lock` statement recognizes a `Lock` field and compiles to its `EnterScope` API, which carries a real guarantee that only one thread holds the gate at a time, rather than the older pattern of locking on an arbitrary `object` field, which worked but offered no type-level signal that the field existed only to be a lock target. The body inside the gate is a single expression, `_table ??= builder.Build()`: the first thread through sees `_table` as null, calls `Build`, and assigns the result; every thread that arrives after that, including every other thread that was waiting on the gate, sees a non-null `_table` and the right-hand side never runs again. The null-coalescing assignment operator makes the check-then-build-then-cache sequence a single statement, which keeps the entire critical section inside the gate small and leaves no window where a thread could read a half-updated field.

The same `Barrier`-driven concurrency test exercises this provider: sixteen readers race for `provider.Value`, `CountingTariffBuilder.BuildCount` comes back as one, and `Assert.Same` confirms every reader shares the one table. The guarantee is identical to the `Lazy<T>` path. What is different is who is responsible for it. With `Lazy<T>`, the framework owns the gate and the cached result. With `LockTariffProvider`, the gate, the backing field, and the `??=` check are all code this class owns and must get right, including remembering never to let the lock body run anything asynchronous. A `lock` block is a synchronous construct: there is no `await` inside it in this example, and there must never be one, because holding a `Lock` across an `await` can suspend the method while still holding the gate, block an unrelated thread that later resumes the continuation, and in the worst case deadlock. When the work behind the gate needs to be asynchronous, that is a different problem with different tools; this provider is for creation that is purely synchronous, which a tariff table parse is.

So when is `LockTariffProvider` the better choice over `LazyTariffProvider`? When the class already needs a `Lock` field for other members and adding one more guarded assignment costs nothing, or when a team wants the creation logic visible in the class itself rather than behind a `Lazy<T>` field for a reader unfamiliar with that type. Absent one of those reasons, `Lazy<T>` says the same thing with less code and nothing left to get wrong.

{% include excalidraw.html name="34-lazy-vs-lock-vs-static" caption="Figure 34.2: Lazy with ExecutionAndPublication, a Lock-guarded field, and a static field initializer each build a TariffTable exactly once, differing in who owns the gate and when the build runs." %}

## The simplest form: a static field initializer

`TariffRegistry` does not lock anything and does not wrap `Lazy<T>`:

```csharp
public static class TariffRegistry
{
    private static readonly TariffTable _instance = new StandardTariffBuilder().Build();

    public static TariffTable Instance => _instance;
}
```

The guarantee here comes from the runtime rather than from any code in the class. A static field initializer runs as part of the type's static constructor, called a type initializer, and the Common Language Runtime guarantees a type initializer runs at most once, before the first access to any static member of that type, with the runtime itself handling the concurrent-first-access race. If several threads reach `TariffRegistry.Instance` before the type has been initialized, the runtime blocks the ones that lose the race and lets only one of them run the initializer, then releases the rest once it completes. This is the same `Assert.Same` guarantee the other two providers earn with explicit code, proven here by `Static_singleton_hands_every_caller_the_same_instance` using the same sixteen-reader `Barrier` test shape.

This is the right choice when the shared instance can be built eagerly, as soon as the type is touched at all, with no need to defer the cost until some later first use and no need to ever swap in a different builder for a test. `TariffRegistry` always builds from `StandardTariffBuilder`, hardcoded, because a static singleton is not a seam you can substitute a test double into; the other two providers take their `ITariffBuilder` through the constructor precisely so `CountingTariffBuilder` and `ThrowingTariffBuilder` can be substituted in test code. That trade-off, simplicity against testability, is the one to weigh before reaching for a static singleton anywhere a build might need to vary.

## The `Lazy<T>` exception-caching trap

`Lazy<T>` in `ExecutionAndPublication` mode does not just cache a successful build. It caches a failed one, too, and rethrows the exact same exception instance to every caller who touches `Value` afterward, rather than giving the next caller a fresh chance to retry. The test `Lazy_provider_caches_a_build_failure_and_rethrows_it` pins this directly: `provider.Value` is read twice against a `LazyTariffProvider` backed by `ThrowingTariffBuilder`, both reads throw `InvalidOperationException` with the message `"Tariff feed unavailable."`, and `Assert.Same` confirms the second caller receives the identical exception object the first caller received, not a new exception with the same message.

This behavior exists because `ExecutionAndPublication` treats the factory delegate as something that runs at most once, whether it succeeds or throws. A rate feed that is down when the first request arrives stays recorded as down for the lifetime of that `Lazy<T>` instance, and no amount of later requests, even after the feed recovers, will make it try again. For a value that only needs to be computed once and is expected to either succeed or fail consistently, that is the right behavior: a transient glitch does not retry on every subsequent read and burn work repeating a failure. For a value backed by something that can recover, an external feed, a database connection, a configuration file that gets fixed and redeployed, caching the failure forever is a liability. The fix is not a different `LazyThreadSafetyMode`; it is choosing a different provider lifetime altogether, discussed further below.

{% include excalidraw.html name="34-exception-caching" caption="Figure 34.3: LazyTariffProvider.Value throws on the first call when the builder fails, and the second call rethrows the identical exception instance rather than retrying the build." %}

## Double-Checked Locking, for context

Before `Lazy<T>` existed in the framework, the idiom for exactly this problem, build an expensive shared object once, under concurrent first use, was the Double-Checked Locking pattern cataloged in *Pattern-Oriented Software Architecture, Volume 2* around the year 2000. It is shown here only to place where `Lazy<T>` and the `Lock`-guarded field in this example came from; nothing in `ThreadSafeTariffs` is written this way, and this is not an idiom to reach for today.

```csharp
// Historical idiom (POSA2, circa 2000), shown for context, not written today.
public sealed class LegacyTariffRegistry
{
    private static TariffTable? _instance;
    private static readonly object _gate = new();

    public static TariffTable Instance
    {
        get
        {
            if (_instance is null)
            {
                lock (_gate)
                {
                    if (_instance is null)
                    {
                        _instance = new StandardTariffBuilder().Build();
                    }
                }
            }
            return _instance;
        }
    }
}
```

The two checks give the pattern its name: the outer check lets every caller after the first skip the lock entirely once `_instance` is set, and the inner check, taken only after acquiring the lock, protects against two threads both passing the outer check before either one has built the value. Getting this right by hand depended on memory-model guarantees that were easy to misstate, which is exactly the kind of subtle correctness burden a framework-provided `Lazy<T>` or a language-level `Lock` now carries instead. `LockTariffProvider` in this example keeps the spirit of a guarded field but drops the double check entirely, because a single `Lock` acquisition per call is cheap enough on current hardware that the outer unlocked fast path this historical idiom was chasing is no longer worth the risk of getting the memory model wrong.

## Build, run, observe

```
cd examples/34-thread-safe-creation && dotnet test
```

```
Test run summary: Passed!
  total: 6
  failed: 0
  succeeded: 6
  skipped: 0
```

The suite covers both `LazyTariffProvider` tests that matter beyond the concurrency guarantee, the not-yet-built state before `Value` is touched and the cached-failure rethrow, alongside the three `Barrier`-driven concurrency tests (`Lazy`, `Lock`, and the static `TariffRegistry`) and the plain rate lookup in `TariffTableTests`. All six are deterministic: the `Barrier` forces real contention on every run, but the assertions, exactly one build and every caller sharing the same instance, hold regardless of how the scheduler happens to interleave the sixteen readers.

## When not to use it

Thread-safe lazy creation is worth the extra code when the shared object is expensive to build and several callers might reach for it before it exists. Reach for something simpler when that is not true.

If the object is cheap, a small struct, a short-lived lookup, anything that costs about as much to build as it costs to check whether it has already been built, plain eager construction is faster and easier to read than any of the three providers in this example. A `Lock` acquisition and a `Lazy<T>` indirection are not free; they only pay for themselves against real construction cost.

The exception-caching behavior in `Lazy<T>` is a hazard, not a bug, but it is a hazard a team needs to decide about up front rather than discover in production. If the underlying builder talks to something that can recover, a feed, a connection, a remote config source, and a caller should be able to retry after a transient failure, `Lazy<T>` in `ExecutionAndPublication` mode is the wrong tool, because the first failure is permanent for the lifetime of that instance. The fix is usually to re-create the `Lazy<T>` wrapper itself rather than reuse the one that already failed, or to drop down to the hand-written `Lock` form, where the provider controls exactly what happens on a failed build, including clearing `_table` so the next caller gets a fresh attempt.

If the object in question is already managed by a dependency injection container, registering it with a singleton lifetime gives the same "built once, shared by every caller" guarantee the container's own way, and most containers already handle concurrent first resolution safely. Reaching for `Lazy<T>`, a `Lock`-guarded field, or a static field initializer on top of a container-managed singleton is solving a problem the container already solved, and it adds a second, usually redundant, source of truth for how many instances actually get built.

<blockquote class="ai-sidebar" markdown="1">
An assistant is a reasonable pair for wiring `Lazy<T>` or a `Lock`-guarded field around an existing builder: naming the provider class, picking constructor shapes, and writing the concurrency test with a `Barrier` are mechanical enough that a generated first draft is often close to right. The two decisions that stay with the person reviewing the code are which `LazyThreadSafetyMode` to pick and whether the exception-caching behavior fits the collaborator being wrapped. An assistant will produce a `Lazy<T>` that compiles and passes a happy-path test without ever asking whether the underlying builder can fail in a way that should be retried, and that question only gets asked if someone on the team asks it.
</blockquote>

## For deeper coverage

- Schmidt, Stal, Rohnert, Buschmann, [*Pattern-Oriented Software Architecture, Volume 2: Patterns for Concurrent and Networked Objects*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+2,+Patterns+for+Concurrent+and+Networked+Objects-p-9781118725177) (Wiley, 2000): the catalogue entry behind Double-Checked Locking, the idiom `Lazy<T>` and a `Lock`-guarded field both replace.
- Microsoft Learn, [Lazy\<T\> class](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1): the authority for `LazyThreadSafetyMode`, `IsValueCreated`, and the exception-caching behavior under `ExecutionAndPublication`.
- Microsoft Learn, [lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock): the language reference for `lock` and the dedicated `System.Threading.Lock` type `LockTariffProvider` guards its field with.
- Microsoft Learn, [Static constructors](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-constructors): the runtime guarantee behind `TariffRegistry`, that a type initializer runs at most once before first access to any static member.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
