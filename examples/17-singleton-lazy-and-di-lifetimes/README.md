# 17-singleton-lazy-and-di-lifetimes

Singleton dissolved across three levels of ceremony, all guarding the same
scenario: a tariff table that is expensive to load and needed everywhere.

- **Classic (Tier 1).** `ClassicTariffTable` is the textbook GoF shape: a
  private constructor, a single `static readonly` field, and a public static
  `Instance` property. An explicit static constructor assigns that field,
  which suppresses the compiler's `beforefieldinit` flag so the load happens
  lazily, on first access to the type, not at some earlier unspecified point.
- **Lazy\<T\> (Tier 2).** `LazyTariffTable` is an ordinary, constructible
  object, no static access point, wrapping its `TariffTable` in a `Lazy<T>`
  built with `LazyThreadSafetyMode.ExecutionAndPublication`. The first read of
  `Table` triggers the load; every read after that, from any thread, returns
  the same already-built instance, and concurrent first reads do not race
  each other into loading twice.
- **DI lifetimes (Tier 3).** `ServiceCollectionExtensions.AddTariffTables`
  registers `ITariffSource` and `TariffTable` as singletons in the built-in
  container, so the whole application shares one loaded table with no static
  field and no hand-written synchronization at all. The container also
  demonstrates the two lifetimes beyond Singleton: Transient hands back a new
  instance on every resolve, and Scoped shares one instance within a scope but
  not across scopes. A fourth test shows the lifetime mistake Singleton
  invites: a singleton that captures a scoped dependency fails fast when the
  container is built with scope validation turned on, instead of silently
  pinning a short-lived dependency for the application's whole lifetime.

`CountingTariffSource` stands in for the expensive load (a rates-service call,
a large published tariff file): it increments `LoadCount` every time `Load()`
actually runs, so every test proves single-init deterministically instead of
inferring it from timing or absence of errors.

**What the tests prove.** `TariffTableTests` shows `Lookup` returns a
published lane's rate and throws for an unpublished one, and that construction
loads the source exactly once. `LazyTariffTableTests` shows `LoadCount` stays
at zero until `Table` is first read, stays at one across five more reads, and
stays at one when sixty-four threads hit `Table` through `Parallel.For`
simultaneously. `ClassicTariffTableTests` shows `Instance` is reference-equal
across calls. `ServiceLifetimeTests` builds a real `ServiceProvider` from
`AddTariffTables()` and shows two resolutions of `TariffTable` return the same
reference while the source loads exactly once; a separately registered
`AddTransient` service returns a different instance on every resolve; an
`AddScoped` service returns the same instance within one `IServiceScope` and a
different instance in another; and building the provider with
`ServiceProviderOptions { ValidateScopes = true }` makes resolving a singleton
that depends on a scoped `ITariffSource` throw `InvalidOperationException`
rather than silently capturing it.

Shipping domain, no containers: `TariffEntry` is a plain record, and
`CountingTariffSource` returns a small fixed set of lanes with no network or
file I/O to fake out.

## Build and test

```sh
cd examples/17-singleton-lazy-and-di-lifetimes
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
