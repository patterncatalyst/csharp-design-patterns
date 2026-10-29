# 05-srp-ocp-lsp

Splits an overloaded rate calculator into single-responsibility collaborators:
a pricing calculator (`IRateQuote`), a tax policy (`ITaxPolicy`), an audit
sink (`IAuditSink`), and an open-ended surcharge pipeline (`ISurcharge`).

**What the test proves.**

- *SRP* - `RateQuoteTests` constructs `WeightBasedRateCalculator` and
  `TieredRateCalculator` directly, with no tax policy and no audit sink wired
  in, and gets a correct quote from pricing alone. `PercentageTaxPolicyTests`
  exercises the tax policy on its own, with no pricing or audit collaborator
  in sight. `RateCalculatorOrchestrationTests` substitutes `IAuditSink` with
  NSubstitute to prove `RateCalculator` calls it exactly once with the final
  quote, and never calls it at all when no audit sink is wired in, without
  writing to a real log, queue, or database.
- *OCP* - `SurchargeOcpTests` composes a brand-new `ISurcharge`
  implementation, `HazmatHandlingSurcharge`, defined only in that test file,
  alongside the shipped `FuelSurcharge`. Composing the new surcharge needs no
  change to `WeightBasedRateCalculator`, `TieredRateCalculator`,
  `SurchargedRateQuote`, or `FuelSurcharge`.
- *LSP* - `LspInvariantTests` runs the same four behavioural checks
  (non-negative result, currency preserved, consignment left untouched,
  monotonically non-decreasing as weight increases) as `[Theory]` cases over
  both `IRateQuote` implementations and both `ISurcharge` implementations, so
  a substitution failure in either direction fails the build.

Shipping domain, no containers: `Consignment`, `Money`, and `Weight` model a
single rate quote for a shipment. There is no database, no HTTP call, and
nothing to run beyond the local `dotnet` CLI.

## Build and test

```sh
cd examples/05-srp-ocp-lsp
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
