---
title: "SRP, OCP, and LSP"
order: 5
layout: tutorial
sectionid: docs
part: "principles"
description: "The first three SOLID principles in idiomatic C#, taught by splitting one overloaded rate calculator."
---

<!-- EXAMPLE (shipping domain): A RateCalculator doing pricing, tax, and audit, split into single responsibilities. -->

## Learning objectives

By the end of this chapter you can:

- State SRP, OCP and LSP as a reason to change, an extension point and a
  behavioral contract.
- Split a method that prices, taxes and audits a shipment into collaborators
  you can construct, test and change separately.
- Add a charge to a pricing pipeline by writing a new type instead of editing
  one.
- Check substitutability by calling each implementation of an interface and
  checking its results, not its signature.
- Recognize the symptom of a missing responsibility boundary: an unrelated
  change breaks a method that looks untouched.

## Why this matters

A rate quote looks like one calculation: weight times rate. In a carrier system
the same method usually also applies a jurisdiction's tax, because the tax lands
on the subtotal the price produced, and writes an audit record, because a quote
that changes without a trail becomes a billing dispute. That's three jobs with
three reasons to change: a new pricing tier, a tax rate update from finance, a
new compliance field. The three share local variables, a call order and one set
of tests, so a change to one risks the other two.

Robert C. Martin's SOLID principles, from *Agile Software Development:
Principles, Patterns, and Practices*, name the shape of code that resists this.
The first three:

| Principle | Statement | In the rate quote |
|---|---|---|
| Single Responsibility (SRP) | A class has one reason to change. | Pricing owes nothing to tax or audit. |
| Open/Closed (OCP) | New behavior is added without editing proven code. | A surcharge pipeline grows by adding types. |
| Liskov Substitution (LSP) | Every implementation of a contract honors the same guarantees, not just the same signatures. | Two pricing strategies are interchangeable, and their results show it. |

{% include excalidraw.html name="05-srp-ocp-lsp" caption="Figure 5.1: A single overloaded rate calculator splits into focused collaborators for pricing, tax, and audit; a surcharge decorator adds charges without editing existing code; two pricing strategies substitute for each other safely." %}

## One overloaded calculator, three reasons to break

Start from one method that multiplies a consignment's weight by a rate, applies
a tax percentage and writes a compliance record, all inline. Each routine change
costs more than it should:

| Change | Cost in the one-method version |
|---|---|
| Finance changes a jurisdiction's tax | Read the pricing math to find the line; rerun every pricing test though pricing didn't change. |
| Compliance adds an audit field | The reviewer approving the audit change also re-approves pricing and tax in the same diff. |
| A new pricing tier | Testing it needs a tax policy and an audit destination to get a price out. |

## Single Responsibility: one reason to change per collaborator

Give pricing, taxation and auditing one type each:

```csharp
public interface IRateQuote
{
    Money Quote(Consignment consignment);
}

public interface ITaxPolicy
{
    Money ApplyTax(Money subtotal);
}

public interface IAuditSink
{
    Task RecordQuoteAsync(Consignment consignment, Money finalQuote, CancellationToken cancellationToken = default);
}
```

| Type | Does | Depends on |
|---|---|---|
| `WeightBasedRateCalculator : IRateQuote` | Weight times a configured rate per kilogram | The rate only; it's constructed and quoted with no tax or audit collaborator. |
| `PercentageTaxPolicy : ITaxPolicy` | Adds a configured percentage to a subtotal | A `Money` value; it never sees a `Consignment`. |
| `IAuditSink` | Records the final quote | A real destination in production; NSubstitute stands in for it in tests. |

A tax-rate change now touches one file and one focused test class. A reviewer
approving a change to `PercentageTaxPolicy` reviews a tax change and nothing
else.

SRP isn't about size. A class twice as long with one axis of change satisfies
it; the original method failed because it had three unrelated reasons to be
edited, not because of its line count. Split along reasons to change.

## Open/Closed: a surcharge pipeline that grows without edits

A quote also carries charges on top: a fuel surcharge on the running subtotal,
an insurance surcharge on declared value, and whatever the next regulation or
service tier adds. OCP asks whether the next charge means editing a proven type
or adding a new one. A fourth interface and a decorator answer it:

```csharp
public interface ISurcharge
{
    Money Apply(Money subtotal, Consignment consignment);
}

public sealed class SurchargedRateQuote(IRateQuote basePricing, IReadOnlyList<ISurcharge> surcharges) : IRateQuote
{
    public Money Quote(Consignment consignment)
    {
        var subtotal = basePricing.Quote(consignment);
        return surcharges.Aggregate(subtotal, (running, surcharge) => running + surcharge.Apply(running, consignment));
    }
}
```

`SurchargedRateQuote` implements `IRateQuote`, the interface it wraps, so a
caller can't tell a bare calculator from one wrapped in any number of
surcharges. `FuelSurcharge` scales the running subtotal; `InsuranceSurcharge`
scales the declared value. Neither knows the other exists, and the fold applies
them in order however many there are.

A new flat 15.00 hazmat handling fee, defined outside the library and composed
with the shipped 10 percent fuel surcharge on a 25.00 base, quotes 42.50. No
edit to `WeightBasedRateCalculator`, `TieredRateCalculator`,
`SurchargedRateQuote` or `FuelSurcharge` was needed.

The abstraction costs an interface, a constructor parameter and some
indirection. It buys a new surcharge that ships as a new file with its own
test, reviewed alone, without reopening `SurchargedRateQuote`. With one
surcharge and no second coming, that cost is speculation. OCP responds to
variation you have, not variation you imagine.

## Liskov Substitution: a contract both calculators honor

`TieredRateCalculator` bills a first band of kilograms at one rate and the rest
at a lower one, a different formula from `WeightBasedRateCalculator`'s flat
multiplication. LSP asks whether the formula is the only difference. A caller
holding an `IRateQuote` can't see these from the signature:

- a negative amount for some input;
- a silent currency conversion;
- a mutated consignment;
- a lower price for a heavier shipment.

Any of them makes the two types compatible at compile time and incompatible at
runtime. All four run as `[Theory]` cases over a `TheoryData`
table of both `IRateQuote` implementations, and again over both `ISurcharge`
implementations, calling each with a real `Consignment` and `Money` and checking
the number returned. The compiler confirms `TieredRateCalculator` has a `Quote`
method of the right shape; only calling it confirms the behavior every caller is
entitled to assume.

`IRateQuote`'s doc comment states the contract: a quote returns a non-negative
amount in the consignment's own currency, leaves the consignment untouched, and
never charges less for a heavier consignment. A third strategy written later has
those sentences to satisfy, and the `[Theory]` cases catch one that violates
them.

## Wiring it back together

`RateCalculator` composes the pieces into one quote:

```csharp
public sealed class RateCalculator(IRateQuote pricing, ITaxPolicy? taxPolicy = null, IAuditSink? auditSink = null)
{
    public async Task<Money> QuoteAsync(Consignment consignment, CancellationToken cancellationToken = default)
    {
        var subtotal = pricing.Quote(consignment);
        var total = taxPolicy is null ? subtotal : taxPolicy.ApplyTax(subtotal);

        if (auditSink is not null)
        {
            await auditSink.RecordQuoteAsync(consignment, total, cancellationToken).ConfigureAwait(false);
        }

        return total;
    }
}
```

| Principle | What it gives the orchestrator |
|---|---|
| SRP | `pricing` is required; `taxPolicy` and `auditSink` are optional, because tax and audit were never part of pricing. Pricing alone is a complete configuration. |
| OCP | A new surcharge or pricing strategy slots in without `RateCalculator` changing. |
| LSP | The constructor accepts any `IRateQuote`: either calculator, bare or wrapped in a `SurchargedRateQuote`. |

SRP draws the boundaries, OCP says how each grows, and LSP makes it safe to
depend on the boundary instead of the implementation behind it.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a SOLID change.** "Apply SOLID to the rate calculator" doesn't say which change is coming, so it leaves room for an interface per class and a strategy that breaks the contract.

> Add fuel and insurance charges without editing `WeightBasedRateCalculator` or `RateCalculator`: write `FuelSurcharge` and `InsuranceSurcharge` as sealed classes implementing `ISurcharge`, and compose them in `SurchargedRateQuote`, which is itself an `IRateQuote`. Keep tax behind `ITaxPolicy` and audit behind `IAuditSink`, both optional constructor parameters of `RateCalculator`. Every `IRateQuote` and `ISurcharge` must return a non-negative `Money` in the input currency and charge no less for a heavier `Consignment`. Add no other interface.

Check with `dotnet test --project RateCalculation.Tests --filter LspInvariantTests`; a new type belongs in its `RateQuoteImplementations` or `SurchargeImplementations` list.
</blockquote>

## Build, run, observe

```sh
cd examples/05-srp-ocp-lsp
dotnet test
```

All 29 tests pass; `./scripts/verify.sh` runs the same suite. Each calculator
and the tax policy are quoted on their own with no other collaborator. With a
substituted `IAuditSink`, `RateCalculator` records the final total exactly once,
and with no sink wired in it still quotes. A new surcharge composes with no
edit to existing types. The four substitution checks pass for both calculators
and both surcharges.

## For deeper coverage

- Martin's *Agile Software Development: Principles, Patterns, and Practices*
  is the source of the SOLID principles applied here, SRP, OCP, and LSP among
  them.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
