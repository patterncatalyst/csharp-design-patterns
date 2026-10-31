# 30-the-options-pattern

The Options pattern applied to a single outbound dependency: a carrier
gateway's endpoint, timeout, and retry budget, bound from configuration,
validated before anything can start with a bad value, and re-read live when
configuration changes.

- **Binding.** `CarrierGatewayOptions` is a settable class, not a record,
  because the configuration binder populates it by setting properties after
  calling its public parameterless constructor. `ServiceCollectionExtensions
  .AddCarrierGateway` binds it to the `CarrierGateway` section with
  `AddOptions<T>().Bind(...)`.
- **Validation.** `CarrierGatewayOptionsValidator` implements
  `IValidateOptions<CarrierGatewayOptions>` and collects every violation
  (a blank or relative endpoint, a non-positive timeout, a negative retry
  budget) into one `ValidateOptionsResult.Fail`, rather than stopping at the
  first problem it finds.
- **ValidateOnStart.** `AddCarrierGateway` also calls `.ValidateOnStart()`,
  which registers an `IStartupValidator` in the container. A real host calls
  it once during startup, before accepting any traffic; this example resolves
  it directly and calls `Validate()` to prove a misconfigured deployment fails
  fast with every reason listed, instead of surfacing a single
  `OptionsValidationException` the first time some unrelated code happens to
  read the value.
- **Live reload.** `RetryBudgetWatcher` subscribes to
  `IOptionsMonitor<CarrierGatewayOptions>.OnChange` once, at construction, and
  records every value it observes. Reloading the in-memory configuration
  (setting `configuration["CarrierGateway:RetryBudget"]` and calling
  `Reload()`) fires that subscription, updates `IOptionsMonitor.CurrentValue`
  immediately for every consumer, and rebinds a fresh value for any new
  `IOptionsSnapshot<CarrierGatewayOptions>` resolved from a new scope. The
  original `IOptionsSnapshot` from an earlier scope, and the long-lived
  `IOptions<CarrierGatewayOptions>` singleton, never change.
- **No file I/O, no host.** `ServiceCollectionExtensions
  .BuildInMemoryConfiguration` builds an `IConfigurationRoot` from a plain
  dictionary with `AddInMemoryCollection`. The in-memory provider supports
  setting a key directly on the built `IConfigurationRoot` (`root["Section:Key"]
  = newValue`); calling `Reload()` afterward is what pushes that change
  through to `IOptionsSnapshot` and `IOptionsMonitor`.

**What the tests prove.** `OptionsBindingTests` shows binding picks up all
three settings from configuration, that `IOptions<T>.Value` returns the same
singleton instance across resolutions, that `IOptionsSnapshot<T>.Value`
reflects the bound configuration within a scope, and that a new scope created
after a reload sees the new value while the pattern's long-lived singleton
would not. `OptionsChangeAndValidationTests` shows
`IOptionsMonitor<T>.OnChange` fires with the new value after a reload (the
assertion checks `RetryBudgetWatcher.Observed` contains the new value, not an
exact callback count, since `OnChange` can fire more than once per
reload depending on how many providers changed), that the validator accepts a
well-formed configuration and rejects each of the three invalid shapes with a
theory, and that `IStartupValidator.Validate()` throws
`OptionsValidationException` with all three reasons for an invalid
configuration and does not throw at all for a valid one.

Shipping domain, no containers: the whole example is pure in-memory
configuration and DI wiring, no network, file, or host to fake out.

## Build and test

```sh
cd examples/30-the-options-pattern
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
