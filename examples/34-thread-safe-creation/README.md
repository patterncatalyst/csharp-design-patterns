# 34-thread-safe-creation

Three ways to build a shipping tariff table exactly once under concurrent
first use, over the same `ITariffBuilder` seam, so the strategies can be
compared on identical input.

- **Lazily published, framework-guaranteed.** `LazyTariffProvider` wraps an
  `ITariffBuilder` in a `Lazy<TariffTable>` running in
  `LazyThreadSafetyMode.ExecutionAndPublication`, the mode that runs the
  factory on at most one winning thread and publishes its result to every
  other caller. A build failure is cached too: once the factory throws, every
  later access rethrows the same exception instance instead of retrying it.
- **Lazily published, hand-written.** `LockTariffProvider` does the same job
  with the modern `System.Threading.Lock` type instead of `Lazy<T>`: a gate
  guards a nullable backing field, and the null-coalescing assignment
  `_table ??= builder.Build()` builds the table only the first time the gate
  is entered. The creation itself is synchronous, so there is no `await`
  inside the lock body.
- **Eagerly built, no lock at all.** `TariffRegistry.Instance` relies on the
  runtime's own guarantee that a type's static field initializers run at most
  once, before the first access to any of its static members. No `Lazy<T>`
  and no explicit lock are needed when the cost of building the table up
  front is acceptable.
- **Test seam.** `CountingTariffBuilder` decorates another `ITariffBuilder`
  and counts, with `Interlocked`, how many times `Build` actually ran, so a
  test can assert a provider built its table exactly once even under
  concurrent contention. `StandardTariffBuilder` is the production builder
  both lazily-published providers and the static registry build from.

**What the tests prove.** For both `LazyTariffProvider` and
`LockTariffProvider`, a `Barrier` releases 16 `Task.Run` readers as close to
simultaneously as the scheduler allows, and the test asserts the wrapped
`CountingTariffBuilder` built exactly once and every reader got back the
identical `TariffTable` reference. Those assertions hold whether or not the
race actually manifests on a given run, which is what keeps the test
deterministic: the `Barrier` only maximizes the chance of real contention. A
separate test proves `LazyTariffProvider` does not build at all until
`Value` is first touched, and another proves a build failure is cached and
rethrown as the same exception instance on a second access. The static
`TariffRegistry` gets the same concurrent-readers treatment, but only for
reference identity, since its build already happened before the test ever
ran and there is no per-process counter to assert on. `TariffTableTests`
checks `TariffTable` itself returns the rate it was seeded with.

No `Thread.Sleep`, no `Task.Delay`, and no wall-clock reads anywhere in this
example: every test either forces contention with a `Barrier` or asserts
synchronously.

## Build and test

```sh
cd examples/34-thread-safe-creation
dotnet test --solution ThreadSafeTariffs.slnx
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
