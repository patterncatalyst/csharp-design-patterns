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

- State the Single Responsibility, Open/Closed, and Liskov Substitution
  principles in terms of a reason to change, an extension point, and a
  behavioral contract, rather than as abstract slogans.
- Split a method that prices, taxes, and audits a shipment into collaborators
  that can each be constructed, tested, and changed on their own.
- Add a new charge to a pricing pipeline by writing a new type instead of
  editing an existing one, and explain what change that closes off.
- Write a Liskov substitution test that calls two differently implemented
  collaborators through the same interface and asserts on their actual
  results, not merely on whether they compile against that interface.
- Recognize the specific symptom, an unrelated change breaking a method that
  looks untouched, that signals a responsibility boundary was never drawn.

## Why this matters

A rate quote for a shipment looks like a single calculation: take a weight,
apply a rate, done. In a production carrier system it is never only that. The
same method that prices the shipment usually also applies a jurisdiction's
tax, because the tax has to land on the same subtotal the price produced.
It often writes an audit record too, because a quote that later changes
without a trail is a billing dispute waiting to happen. One method, three
jobs, and three different reasons for someone to come back and change it: a
new pricing tier, a tax rate update from finance, a new compliance field the
audit record has to carry. None of those three changes has anything to do
with either of the other two, and in a method that mixes all three, each one
risks breaking the other two anyway, because they share local variables, a
call order, and a single set of tests that has to keep passing for all three
jobs at once.

Robert C. Martin's SOLID principles, laid out in *Agile Software Development:
Principles, Patterns, and Practices*, name the forces that make this growth
expensive and describe the shape of code that resists it. This chapter
works through the first three against exactly the rate-quote problem just
described: Single Responsibility Principle (SRP) says a class should have one
reason to change, Open/Closed Principle (OCP) says new behavior should be
addable without editing proven code, and Liskov Substitution Principle (LSP)
says any implementation of a shared contract has to honor the same
guarantees, not merely the same method signatures. None of the three is a
style preference. Each one names a specific category of unplanned work a
design either invites or closes off, and the example in this chapter is
built to make all three visible: a pricing collaborator that owes nothing to
tax or audit, a surcharge pipeline that grows by addition, and two pricing
strategies that substitute for each other because a test proves it rather
than assumes it.

{% include excalidraw.html name="05-srp-ocp-lsp" caption="Figure 5.1: A single overloaded rate calculator splits into focused collaborators for pricing, tax, and audit; a surcharge decorator adds charges without editing existing code; two pricing strategies substitute for each other safely." %}

## One overloaded calculator, three reasons to break

Picture the rate quote as a single method before any of this chapter's
splitting happens: it takes a consignment, multiplies its weight by a rate,
applies a tax policy's percentage to the result, and writes a record of the
final number somewhere for compliance, all three steps inline, in one method
body. The method works, and it stays working right up until one of its three
jobs needs to change independently of the other two. Finance announces a new
tax calculation for a jurisdiction, and whoever fixes it has to read the
pricing math first to find the line that needs editing, then re-run every
test that exercises pricing even though pricing itself never changed.
Compliance asks for an additional field on the audit record, and the fix sits
inside the same method, which means a reviewer approving an audit change is
also implicitly re-approving the pricing and tax logic sitting in the same
diff. A new pricing tier shows up, and testing it means standing up a tax
policy and an audit destination just to get a price out of the method, even
though neither one has anything to do with the tier logic under test. None of
these three changes is unusual. Each one is routine, and each one is harder
than it needs to be only because the method never decided which of the three
jobs it owns.

## Single Responsibility: one reason to change per collaborator

The fix isn't to make the method smarter. It's to decide that pricing,
taxation, and auditing are three different reasons to change, and to give
each one its own type. The shipping example in this chapter does exactly
that with three small interfaces:

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

`WeightBasedRateCalculator` implements `IRateQuote` by multiplying a
consignment's weight by a configured rate per kilogram, and nothing else. It
takes no tax policy and no audit sink in its constructor, because it has no
use for either; `RateQuoteTests.WeightBasedRateCalculator_needs_no_tax_or_audit_collaborator`
constructs it alone and gets a correct quote straight back, which is the
test-level proof that pricing's only dependency is a rate. `PercentageTaxPolicy`
implements `ITaxPolicy` by adding a configured percentage on top of whatever
subtotal it is handed. It never sees a `Consignment`, only a `Money` value,
so a tax-rate change from finance touches exactly one file and reruns exactly
one focused test class, `PercentageTaxPolicyTests`, with no pricing
collaborator anywhere in the picture. `IAuditSink` is implemented against a
real destination outside this example, but inside the test suite it is
substituted with NSubstitute: `RateCalculatorOrchestrationTests.Calls_the_audit_sink_exactly_once_with_the_final_quote`
proves the final total reaches the audit sink exactly once, and
`Never_calls_an_audit_sink_that_was_not_wired_in` proves the reverse, that
omitting an audit sink at construction time never silently crashes or skips
pricing. Each of the three collaborators in this split has one constructor
dependency it needs, one job, and one test class that exercises it
without standing up either of the other two. A reviewer approving a change to
`PercentageTaxPolicy` is reviewing a tax change, not a tax change bundled with
whatever pricing or auditing code happened to share its method body.

SRP doesn't say a class should be small. It says a class should have one
reason to change, and the size follows from that, not the other way around.
`WeightBasedRateCalculator` is nine lines including its guard clause, but the
nine lines aren't the point; the point is that every one of those nine lines
changes only when the pricing formula changes, for no other reason. A class
twice that size with the same property, one axis of change and nothing else,
would satisfy SRP just as well. The overloaded method this chapter opened
with wasn't too long because it had too many lines; it was too long because
it had three unrelated reasons to be edited, and splitting it along those
reasons, not along an arbitrary line count, is what SRP asks for.

## Open/Closed: a surcharge pipeline that grows without edits

Pricing, tax, and audit cover the quote's core shape, but a real rate quote
usually carries additional charges on top: a fuel surcharge tracking current
fuel costs, an insurance surcharge proportional to declared value, and
whatever charge a new regulation or a new service tier introduces next. The
Open/Closed Principle asks whether adding the next charge requires editing
any of the proven types above, or only adding a new one next to them. This
example answers that with a fourth interface and a decorator:

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

`SurchargedRateQuote` itself implements `IRateQuote`, the same interface its
wrapped `basePricing` implements, so a caller that only knows about
`IRateQuote` can't tell whether it is quoting a bare calculator or one
wrapped in an arbitrary stack of surcharges. `FuelSurcharge` scales the
running subtotal by a configured rate; `InsuranceSurcharge` scales the
consignment's declared value instead, proportional to what is being insured
rather than what it costs to ship. Neither surcharge type knows the other
exists, and `SurchargedRateQuote` folds them over the subtotal one at a time
without caring how many there are or what any individual one does internally.

The proof that this pipeline is open for extension lives in
`SurchargeOcpTests`, and it is a stronger proof than reading the production
code and nodding along. The test file defines a brand-new charge,
`HazmatHandlingSurcharge`, a flat fee that implements `ISurcharge` and exists
nowhere in the shipped library, only in the test. `A_newly_added_surcharge_type_composes_without_editing_any_pricing_type`
composes it alongside the shipped `FuelSurcharge` and asserts on the combined
total, 25.00 in base pricing plus a 10 percent fuel surcharge plus a flat
15.00 hazmat fee, landing at 42.50. Getting that number right requires zero
changes to `WeightBasedRateCalculator`, `TieredRateCalculator`,
`SurchargedRateQuote`, or `FuelSurcharge`, the four types that existed before
the test was written. That's what "closed for modification, open for
extension" means in a way a reader can check rather than take on faith: the
extension is a type a test introduced after the fact, and the pipeline
absorbed it with no edit to anything already shipped.

OCP isn't free, and this chapter's example doesn't pretend otherwise. An
`ISurcharge` abstraction costs a constructor parameter, an interface, and a
small amount of indirection that a single fixed fuel surcharge wouldn't
need. That cost buys something specific: a new surcharge type ships as a new
file with its own focused test, reviewed on its own, without reopening
`SurchargedRateQuote` or re-running every existing surcharge's tests to make
sure the new one didn't change their behavior. Where a codebase has one
surcharge and no second one coming, paying that cost ahead of the need is
speculation, not Open/Closed design; the principle describes a response to
real variation, the kind this chapter's second calculator supplies next, not
a license to wrap every value in an interface on the chance that it might
vary someday.

## Liskov Substitution: a contract both calculators honor

The example ships two pricing strategies, `WeightBasedRateCalculator` and
`TieredRateCalculator`, and OCP's story about extension only works if a
caller holding an `IRateQuote` can swap one for the other without checking
which concrete type it has. `TieredRateCalculator` prices a bulk-discount
tier, billing a first stretch of kilograms at one rate and the remainder at a
lower one, a completely different formula from `WeightBasedRateCalculator`'s
flat per-kilogram multiplication. Liskov Substitution asks whether that
difference in formula is the only difference, or whether it comes bundled
with a difference in behavior a caller can't see from the method signature:
does one implementation return a negative number under some input where the
other never would, silently convert currency, mutate the consignment it was
handed, or charge less for a heavier shipment than a lighter one. Any of
those would make the two types compile-time compatible and runtime
incompatible, which is the failure LSP exists to catch.

`LspInvariantTests` checks for exactly that failure, and it does so by
calling each implementation and asserting on its actual output rather than
inspecting its type. A `TheoryData` table lists both `IRateQuote`
implementations as factory functions, and four `[Theory]` methods run over
that table: every quote returns a non-negative amount, every quote preserves
the consignment's currency rather than silently converting it, every quote
leaves the consignment unmutated, and every quote charges at least as much
for a fifty-kilogram shipment as it does for a ten-kilogram one. The same
four checks run again over a second table listing both `ISurcharge`
implementations, `FuelSurcharge` and `InsuranceSurcharge`, because a
surcharge is exactly as substitutable through `ISurcharge` as a pricing
strategy is through `IRateQuote`, and deserves the same proof. None of these
eight test methods type-checks a result or confirms that a method exists;
each one calls the implementation with a real `Consignment` and a real
`Money` value and asserts on the number that comes back. That's what
distinguishes a Liskov substitution test from a compiler's type check: the
compiler confirms `TieredRateCalculator` has a `Quote` method shaped like
`IRateQuote` requires; the test confirms calling that method produces the
behavior every caller of `IRateQuote` is entitled to assume.

Writing those four invariants down as doc comments on `IRateQuote` itself,
not just as test assertions, matters as much as the test suite does. An
interface that specifies only a method signature tells an implementer what
to compile against; an interface whose doc comment states that a quote
"must return a non-negative amount," "leave the consignment untouched," and
"never charge less for a heavier consignment" tells an implementer what to
honor. A third pricing strategy added later, without ever looking at
`LspInvariantTests`, still has those three sentences to satisfy, and the
`[Theory]` cases will catch a strategy that quietly violates one of them the
same way they already cover the two that ship today.

## Wiring it back together without losing any of the three principles

Splitting pricing, tax, audit, and surcharges into separate types only pays
off if composing them back into a single quote stays simple, and
`RateCalculator` is the orchestrator that does that:

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

`pricing` is required, because a rate quote with no price is not a rate
quote; `taxPolicy` and `auditSink` are optional, because `RateCalculatorOrchestrationTests.Quotes_with_pricing_only_when_no_tax_or_audit_is_wired_in`
proves pricing alone is a complete, usable configuration with neither of the
other two collaborators present. That optionality is SRP's payoff at the
orchestration level: because tax and audit were never baked into pricing in
the first place, `RateCalculator` can treat them as independently addable
steps rather than as a package deal. The constructor accepts any `IRateQuote`,
whether that is a bare `WeightBasedRateCalculator`, a bare
`TieredRateCalculator`, or either one wrapped in a `SurchargedRateQuote`
carrying any number of surcharges, because LSP guarantees all of them honor
the same contract, and OCP guarantees a new surcharge or a new pricing
strategy slots in without `RateCalculator` itself ever changing. The three
principles don't operate independently of each other here; SRP draws the
boundaries, OCP describes how each boundary grows, and LSP is what makes it
safe for `RateCalculator` to depend on the boundary rather than on a specific
implementation behind it.

<blockquote class="ai-sidebar" markdown="1">
Ask an AI pairing partner to "apply SOLID" to a working rate calculator and
a common result is an interface for every class regardless of whether a
second implementation exists, because that shape appears constantly in
material that treats SOLID as a checklist rather than a response to a
specific force. Check the result against what each principle asks: SRP asks
whether a proposed split follows a real difference in who changes the code
and why, not just whether the result has more files than before. OCP asks
whether a second pricing strategy or surcharge is already in hand or
clearly coming; an `ISurcharge` with exactly one implementation and nothing
else planned is OCP's cost with none of its benefit. LSP asks whether a
generated implementation honors the contract a shared interface promises,
not merely its method signature; a model asked to add a new pricing
strategy will cheerfully write one that returns a negative adjustment for a
promotional rate, something no other `IRateQuote` implementation does,
unless a test like `LspInvariantTests` catches it. None of the three
principles is verified by reading generated code and nodding; each one is
verified by the kind of test this chapter's example already carries.
</blockquote>

## Build, run, observe

Every command below runs with the local `dotnet` CLI against the .NET 10
SDK; there's no container anywhere in this loop. The example is
standalone-testable from its own directory:

```sh
cd examples/05-srp-ocp-lsp
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`. Observed
locally on Fedora with .NET SDK 10.0.113:

```
Running tests from examples/05-srp-ocp-lsp/RateCalculation.Tests/bin/Debug/net10.0/RateCalculation.Tests.dll (net10.0|x64)
examples/05-srp-ocp-lsp/RateCalculation.Tests/bin/Debug/net10.0/RateCalculation.Tests.dll (net10.0|x64) passed (1s 038ms)

Test run summary: Passed!
  total: 29
  failed: 0
  succeeded: 29
  skipped: 0
  duration: 1s 357ms
```

The 29 cases split across five files by exactly the three principles this
chapter covers. `RateQuoteTests` and `PercentageTaxPolicyTests` prove SRP by
constructing `WeightBasedRateCalculator`, `TieredRateCalculator`, and
`PercentageTaxPolicy` each on their own, with no other collaborator in
sight. `RateCalculatorOrchestrationTests` proves SRP at the orchestration
level, substituting `IAuditSink` with NSubstitute to confirm `RateCalculator`
calls it exactly once with the final total, and never calls it at all when
no audit sink is wired in. `SurchargeOcpTests` proves OCP by composing a
brand-new `HazmatHandlingSurcharge`, defined only in the test file, alongside
the shipped `FuelSurcharge`, with no edit to any pricing or surcharge type
already in the library. `LspInvariantTests` proves LSP by running four
behavioral checks, non-negative result, preserved currency, an untouched
consignment, and a non-decreasing price as weight increases, as `[Theory]`
cases over both `IRateQuote` implementations and both `ISurcharge`
implementations, so a substitution failure in either direction fails the
build rather than passing silently.

## For deeper coverage

- Martin's *Agile Software Development: Principles, Patterns, and Practices*
  is the source of the SOLID principles this chapter builds its examples on,
  SRP, OCP, and LSP among them.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
