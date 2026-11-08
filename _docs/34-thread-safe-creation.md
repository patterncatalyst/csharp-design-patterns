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

By the end of this chapter you can:

- Build an expensive shared object exactly once under concurrent first use with `Lazy<T>` in `LazyThreadSafetyMode.ExecutionAndPublication`.
- Write the same guarantee by hand with a `System.Threading.Lock`-guarded field, and explain why that path only works for synchronous creation.
- Use a static field initializer as the simplest thread-safe singleton, and state the runtime guarantee it relies on.
- Reason about `Lazy<T>` exception caching: a failed build is remembered and rethrown, not retried.
- Place Double-Checked Locking as a historical idiom that `Lazy<T>` supersedes.

## Why this matters

A shipping platform builds its tariff table once and shares it. Reading it is a dictionary lookup by lane code; building it means parsing a published rate sheet, validating lane codes and assembling the structure. If sixteen requests arrive at once during a cold start and each builds its own copy, that's sixteen redundant parses, sixteen allocations for the garbage collector and, if the rate feed shifts between calls, sixteen slightly different tables.

The hazard is construction, not use. Once the table exists, many threads read `RatePerKilogram` without coordination. Getting creation wrong doesn't crash: it wastes work, and a builder with side effects, such as a counter or an external call, gives answers that depend on how many threads raced.

`ITariffBuilder` is the seam. `StandardTariffBuilder` seeds `TariffTable` with the `STD` and `EXP` lane rates, `CountingTariffBuilder` wraps it to count real builds, and `ThrowingTariffBuilder` stands in for a feed that fails. Three providers publish one `TariffTable` to every caller: `LazyTariffProvider`, `LockTariffProvider` and the static `TariffRegistry`.

{% include excalidraw.html name="34-thread-safe-creation" caption="Figure 34.1: Sixteen concurrent readers call LazyTariffProvider.Value at once, but CountingTariffBuilder proves the underlying build runs exactly once and every reader gets the same table." %}

## The default: `Lazy<T>` with `ExecutionAndPublication`

```csharp
public sealed class LazyTariffProvider(ITariffBuilder builder)
{
    private readonly Lazy<TariffTable> _lazy = new(builder.Build, LazyThreadSafetyMode.ExecutionAndPublication);

    public TariffTable Value => _lazy.Value;

    public bool IsValueCreated => _lazy.IsValueCreated;
}
```

Nothing runs until the first read of `Value`. `IsValueCreated` reports whether that has happened without forcing it, which suits a health check that says "not yet warmed up" instead of paying for a build.

`ExecutionAndPublication` lets only one thread run the factory. Threads arriving during that run block, then receive the same published result. It's the default for a `Lazy<T>` built with no arguments or with only a factory; naming it makes the guarantee visible in the code.

Sixteen `Task.Run` readers released together through a `Barrier` all read `provider.Value`. The builder runs once, and every reader gets the identical `TariffTable` reference. Both facts matter: one build, and that one table is what everyone received. `TariffTable` keeps default reference identity for this reason. With value equality, two equal-looking tables from two racing threads would compare equal and hide a double build.

Reach for this provider first. It needs no locking code, works whether the first touch comes from one thread or many, and its thread safety is one constructor argument.

## Writing the same guarantee by hand with `Lock`

`LockTariffProvider` gives the same contract with a `System.Threading.Lock` field:

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

`System.Threading.Lock` is the dedicated mutual-exclusion type. The `lock` statement recognizes a `Lock` field and compiles to its `EnterScope` API; locking on an arbitrary `object` also worked, but gave no type-level signal that the field existed only as a lock target. Inside the gate, `_table ??= builder.Build()` is the whole check, build and cache: the first thread builds and assigns, and every later thread sees a non-null field. The critical section stays one statement, with no window for a half-updated read.

Under the same sixteen-reader race the build runs once and every reader shares the table. The difference is ownership: the gate, the field and the `??=` are this class's code to get right. That includes never putting an `await` inside the `lock`. Holding a lock across an `await` can suspend the method with the gate held, block an unrelated thread that resumes the continuation, and deadlock. This provider is for synchronous creation, which a tariff parse is.

| Choose | When |
| --- | --- |
| `LazyTariffProvider` | By default |
| `LockTariffProvider` | The class already has a `Lock` for other members, or the team wants creation logic visible in the class |

{% include excalidraw.html name="34-lazy-vs-lock-vs-static" caption="Figure 34.2: Lazy with ExecutionAndPublication, a Lock-guarded field, and a static field initializer each build a TariffTable exactly once, differing in who owns the gate and when the build runs." %}

## The simplest form: a static field initializer

```csharp
public static class TariffRegistry
{
    private static readonly TariffTable _instance = new StandardTariffBuilder().Build();

    public static TariffTable Instance => _instance;
}
```

The guarantee comes from the runtime. A static field initializer runs in the type initializer, and the CLR runs a type initializer at most once, before first access to any static member, blocking threads that lose the race until it completes. Sixteen concurrent readers all get the same instance.

Use it when the instance can be built eagerly, as soon as the type is touched, and never needs a different builder. `TariffRegistry` hardcodes `StandardTariffBuilder` because a static singleton isn't a seam for a test double; the other two providers take `ITariffBuilder` through the constructor so counting and throwing builders can be substituted. The trade-off is simplicity against testability.

## The `Lazy<T>` exception-caching trap

In `ExecutionAndPublication` mode, `Lazy<T>` caches a failed build as well as a successful one. Every later read of `Value` rethrows the same exception instance. Against `ThrowingTariffBuilder`, two reads both throw `InvalidOperationException` with `"Tariff feed unavailable."`, and the second is the identical object, not a fresh exception.

The mode treats the factory as run-at-most-once, success or failure. A rate feed that's down at the first request stays down for the life of that `Lazy<T>`, even after it recovers. For a value that succeeds or fails consistently, that's right: no repeated work on every read. For something that can recover, such as an external feed, a database connection or a redeployed configuration file, caching the failure forever is a liability, and no other `LazyThreadSafetyMode` fixes it; the fix is a different provider lifetime (see "When not to use it").

{% include excalidraw.html name="34-exception-caching" caption="Figure 34.3: LazyTariffProvider.Value throws on the first call when the builder fails, and the second call rethrows the identical exception instance rather than retrying the build." %}

## Double-Checked Locking, for context

Before `Lazy<T>`, this problem was solved with Double-Checked Locking, cataloged in *Pattern-Oriented Software Architecture, Volume 2* around 2000. It appears here only to show where `Lazy<T>` and the `Lock`-guarded field came from:

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

The outer check lets every caller after the first skip the lock; the inner check, under the lock, stops two threads that both passed the outer check from building twice. Correctness depended on memory-model guarantees that were easy to misstate. `Lazy<T>` now carries that burden. `LockTariffProvider` keeps the guarded field and drops the double check, because one `Lock` acquisition per call is cheap enough on current hardware that the unlocked fast path isn't worth the memory-model risk.

## Build, run, observe

```
cd examples/34-thread-safe-creation && dotnet test
```

All 6 tests pass: the not-yet-built state, the cached-failure rethrow, the sixteen-reader race against each of the three providers, and a plain rate lookup. The `Barrier` forces real contention on every run, and the results (one build, one shared instance) hold whatever interleaving the scheduler picks.

## When not to use it

Thread-safe lazy creation pays when the object is expensive and several callers may reach for it before it exists.

- **Cheap objects.** When building costs about as much as checking whether it's built, construct eagerly. A `Lock` acquisition and a `Lazy<T>` indirection aren't free.
- **Builders that can recover.** If a caller should be able to retry after a transient failure, `Lazy<T>` in `ExecutionAndPublication` is the wrong tool. Re-create the `Lazy<T>` wrapper after a failure, or use the `Lock` form, where the provider decides what a failed build does, including clearing `_table` so the next caller retries.
- **Container-managed objects.** A singleton registration in a dependency injection container already gives built-once sharing, and most containers handle concurrent first resolution. Layering `Lazy<T>`, a lock or a static initializer on top adds a second, redundant source of truth for how many instances exist.

<blockquote class="ai-sidebar" markdown="1">
An assistant can wire `Lazy<T>` or a `Lock`-guarded field around an existing builder: naming the provider, choosing constructor shapes and writing a `Barrier`-driven concurrency check are mechanical enough that a first draft is often close. Two decisions stay with the reviewer: which `LazyThreadSafetyMode` to pick, and whether exception caching fits the collaborator being wrapped. A generated `Lazy<T>` compiles and passes the happy path without asking whether the builder can fail in a way that should be retried.
</blockquote>

## For deeper coverage

- Schmidt, Stal, Rohnert, Buschmann, [*Pattern-Oriented Software Architecture, Volume 2: Patterns for Concurrent and Networked Objects*](https://www.wiley.com/en-us/Pattern-Oriented+Software+Architecture,+Volume+2,+Patterns+for+Concurrent+and+Networked+Objects-p-9781118725177) (Wiley, 2000): the catalogue entry behind Double-Checked Locking, the idiom `Lazy<T>` and a `Lock`-guarded field both replace.
- Microsoft Learn, [Lazy\<T\> class](https://learn.microsoft.com/en-us/dotnet/api/system.lazy-1): the authority for `LazyThreadSafetyMode`, `IsValueCreated`, and the exception-caching behavior under `ExecutionAndPublication`.
- Microsoft Learn, [lock statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock): the language reference for `lock` and the dedicated `System.Threading.Lock` type `LockTariffProvider` guards its field with.
- Microsoft Learn, [Static constructors](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-constructors): the runtime guarantee behind `TariffRegistry`, that a type initializer runs at most once before first access to any static member.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
