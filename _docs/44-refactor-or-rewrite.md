---
title: "Refactor or rewrite"
order: 44
layout: tutorial
sectionid: docs
part: "changing-inherited-code"
description: "The judgement call, with criteria rather than a reflex."
---

<!-- EXAMPLE (shipping domain): The rate engine: refactor or rewrite. -->

## Learning objectives

By the end of this chapter you can:

- State the four criteria that weigh a refactor-or-rewrite decision (test
  coverage, change frequency, blast radius, domain stability) and argue which
  way each one leans for a concrete piece of code.
- Explain why `LegacyRateEngine` offers no stable seam to refactor or rewrite
  behind, and point to the specific shape (`sealed`, non-virtual `Quote`, a
  hard-coded `new LegacyTariffTable()`, no `IRateEngine` implementation) that
  closes off both paths.
- Define `IRateEngine` as the stable seam that makes the choice reversible,
  and describe how `RefactoredRateEngine` and `RewrittenRateEngine` both
  satisfy it from different starting points.
- Read `EquivalenceTests.Both_implementations_satisfy_the_same_contract` and
  explain what a record-equality assertion across six scenarios proves that a
  single passing test on one engine alone could not.
- Show that `QuoteService` depends only on `IRateEngine`, so swapping the
  refactored engine for the rewritten one is a one-line change at a
  construction site rather than a change to the consumer.

## Why this matters

"Refactor or rewrite" gets asked about a piece of code the moment someone
notices it is hard to change, and the question usually arrives already
dressed as an opinion. One camp treats a rewrite as an admission of failure,
a sign the team never understood the problem well enough to grow the
original code into something better. The other treats a rewrite as the only
sound response to code nobody trusts, on the theory that patching
something broken enough just teaches the next patch where to go wrong too.
Both camps are answering a question that has no single answer, because the
right call depends on facts specific to the code in front of you, not on
which camp you started in.

Four of those facts do most of the work. How much of the current behavior is
already pinned down by tests, so a rewrite has something to check itself
against. How often this code actually changes, since a seam paid for once
only earns its cost back over repeated use. How much of the system would
feel a mistake here, which decides how much safety net the change needs
before it ships. And how settled the domain rules are, because a domain
still being discovered punishes a rewrite's assumption that today's
understanding is the final one. None of the four hands back a verdict by
itself. A rate engine with thin coverage, low change frequency, a narrow
blast radius, and a domain that has not moved in years is a very different
decision than the same code with daily changes feeding a dozen downstream
services.

What makes either answer safe to act on is a detail easy to skip past: the
decision only stays reversible if both outcomes, refactor and rewrite, land
behind the same stable contract. Without that contract, choosing a rewrite
commits you to it before you know whether the new engine actually agrees
with the old one on every case that matters. With it, a team can build the
rewrite alongside the refactor, in the same codebase, resolved against the
identical interface, and prove the two produce identical results before
either one has to win. This chapter's rate engine does exactly that: one
interface, two replacements built from first principles in different
directions, and a test that checks them against each other rather than
asking either one to be trusted on faith.

{% include excalidraw.html name="44-refactor-or-rewrite" caption="Figure 44.1: The legacy engine has no stable seam to build on, but IRateEngine gives both a refactored and a rewritten replacement a shared contract, so QuoteService can swap one for the other with no change of its own." %}

## A rate engine with nothing to build on

`LegacyRateEngine.Quote` is the starting point, and it is worth reading
before asking whether to refactor or rewrite it, because the method's shape
rules out a third option: leaving it alone and building new work around it.

```csharp
public decimal Quote(RateRequest request)
{
    ArgumentNullException.ThrowIfNull(request);

    var tariff = new LegacyTariffTable();
    var subtotal = request.DeclaredValue * 0.015m;
    subtotal += request.WeightKilograms * tariff.RatePerKilogram(request.LaneCode);

    if (DateTime.Now.Hour is >= 8 and < 18)
    {
        subtotal *= 1.1m;
    }

    return subtotal;
}
```

`new LegacyTariffTable()` means the per-kilogram rate comes from whatever
that one constructor seeds, and nothing a caller supplies can change it.
`0.015m` is the ad-valorem rate, written inline rather than named, so a test
asserting on a quote amount is also asserting on a number nobody can find a
name for elsewhere in the type. `DateTime.Now.Hour` reads the ambient wall
clock directly, so the one branch that applies a peak-hour surcharge only
runs one way during whichever ten hours a test happens to execute in, and
the other way the rest of the day. `LegacyRateEngine` itself is `sealed`,
`Quote` is not `virtual`, and `LegacyRateEngine` does not implement
`IRateEngine` or any other interface. `LegacyContrastTests.Legacy_engine_has_no_stable_seam_to_refactor_or_rewrite_behind`
checks all of that through reflection rather than by asserting on a quote
amount that would depend on the hour the test ran: `legacyEngineType.IsSealed`
is true, the type's single constructor takes no parameters, the `Quote`
method's `IsVirtual` is false, and `typeof(IRateEngine).IsAssignableFrom(legacyEngineType)`
is false. Every one of those four facts is a door closed before the
refactor-or-rewrite question is even asked. A sealed type with a
non-virtual method and no interface cannot be subclassed, cannot be
overridden, and cannot be substituted for anything else through a shared
contract. There is no seam here to refactor behind and no seam here to
build a rewrite's replacement contract on top of. The first move, whichever
way the decision eventually goes, is the same move: name the contract this
type should have had from the start.

## IRateEngine: the seam that makes the choice reversible

```csharp
public interface IRateEngine
{
    RateQuote Quote(RateRequest request);
}
```

`IRateEngine` names one method, matching the one behavior `LegacyRateEngine`
already exposes, so naming the interface changes nothing about what a rate
engine computes. What it changes is what a caller can depend on. A type
bound to `IRateEngine` never knows, and never needs to know, whether the
instance behind it got there by growing out of the legacy calculation one
step at a time or by starting over with a different internal shape
entirely. That is the property a refactor-or-rewrite decision needs before
it can be made safely: once both outcomes are required to satisfy the same
named contract, choosing one over the other stops being a one-way door.
Either implementation can replace the other later, because both were built
against the same seam from the beginning rather than against each other's
internals.

{% include excalidraw.html name="44-decision-criteria" caption="Figure 44.2: Test coverage, change frequency, blast radius, and domain stability each weigh toward refactoring in place or rewriting clean. The matrix is a judgement aid, not a formula that hands back a verdict." %}

Each of the four criteria reads differently once a stable seam like
`IRateEngine` exists to build either outcome behind.

**Test coverage.** A refactor-in-place leans on coverage that already
exists, moving a calculation's shape in small steps while a passing suite
confirms nothing shifted along the way. A rewrite needs coverage of a
different kind: not tests that watch the old implementation for
regressions, but tests that pin down the *contract* both old and new forms
must satisfy, so a clean-sheet rewrite has something external to check
itself against the moment it compiles. Thin coverage of either kind argues
for writing the tests first, against the seam, before touching the
implementation on either side of the decision.

**Change frequency.** Code that changes weekly earns back the cost of a
rewrite's seams and structure many times over; code that has not changed in
two years may never recoup that investment no matter how clean the result
would be. A rate engine quoting one rule set for a stable set of lanes, with
no pending change in sight, is a weak candidate for a ground-up rewrite
regardless of how dated its internals read, because nothing is coming that
would exploit a cleaner shape.

**Blast radius.** How much of the system depends on this rate engine's exact
output decides how much proof a change needs before it ships. A rate engine
feeding one internal report tolerates more risk than one feeding customer
invoices. A wide blast radius argues for the smallest, most verifiable
change available, which is often a refactor taken in individually testable
steps rather than a rewrite that replaces everything at once and asks a
single equivalence check to catch every discrepancy in one pass.

**Domain stability.** A rewrite encodes today's understanding of the rules
as the new, cleaner shape. If the rules themselves are still being
discovered, a rewrite risks baking in an understanding that is about to be
wrong again, while a refactor that keeps the existing rule evaluation intact
has less surface for that kind of drift. A domain that has settled, where
the rate rules have not meaningfully changed in years, removes that risk
and makes a rewrite's clean-slate assumptions safer to make.

None of the four criteria is a threshold to clear on its own, and none
overrides the others by default. A rate engine with excellent coverage, low
change frequency, a narrow blast radius, and a settled domain still might
be refactored rather than rewritten, if the team simply has no appetite for
a rewrite's upfront cost against a payoff that will not be collected for
years. The criteria structure the conversation; they do not replace the
judgement the conversation is for.

## Two replacements, the same contract

`RefactoredRateEngine` takes the refactor-in-place path: the same
calculation `LegacyRateEngine` performed, moved behind `IRateEngine` and
given its collaborators through a constructor instead of constructing them
itself, but left as the same running subtotal built up statement by
statement.

```csharp
public sealed class RefactoredRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine
{
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = request.DeclaredValue * RateRules.DeclaredValueRate;
        subtotal += request.WeightKilograms * tariffs.RatePerKilogram(request.LaneCode);

        var at = clock.UtcNow;
        if (PeakWindow.IsPeak(at))
        {
            subtotal *= 1m + RateRules.PeakMultiplier;
        }

        return new RateQuote(request.ConsignmentId, subtotal, at);
    }
}
```

`ITariffSource` replaces the hard-coded `new LegacyTariffTable()`, and
`IClock` replaces `DateTime.Now`, so both the rate lookup and the
peak-window check are now substitutable from outside the method instead of
fixed inside it. `RateRules.DeclaredValueRate` and `RateRules.PeakMultiplier`
replace the two inline literals the legacy method never named. Nothing
about the order of operations changed: declared value times rate, plus
weight times per-kilogram rate, then a conditional multiplier if the clock
falls inside the peak window. A refactor-in-place keeps exactly that
promise. It moves a calculation onto a seam without asking whether the
calculation's own shape, a mutable running subtotal built up across several
statements, is the shape anyone would choose starting fresh.

`RewrittenRateEngine` answers that question differently, computing the same
contract as one consolidated expression instead of a sequence of mutations:

```csharp
public sealed class RewrittenRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine
{
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var at = clock.UtcNow;
        var baseAmount = (request.DeclaredValue * RateRules.DeclaredValueRate)
            + (request.WeightKilograms * tariffs.RatePerKilogram(request.LaneCode));
        var total = PeakWindow.IsPeak(at) ? baseAmount * (1m + RateRules.PeakMultiplier) : baseAmount;

        return new RateQuote(request.ConsignmentId, total, at);
    }
}
```

The ad-valorem and per-kilogram components combine into one `baseAmount`
before the peak surcharge is applied as a single conditional expression
rather than a mutating `*=` inside an `if` block. Both engines take the
same two constructor parameters, both implement the same `IRateEngine`
method, and both read `RateRules` and `PeakWindow` rather than
re-deriving either rule on their own. Neither engine knows the other
exists. What makes them comparable is not that one was written by looking
at the other; it is that both were written against the same named contract,
so a test outside either one can ask whether they agree.

## Proving equivalence instead of assuming it

A rewrite is only a safe choice once something checks that the new
implementation actually agrees with the old one, or in this case, with the
refactored one, on every input that matters. `EquivalenceTests` does that
checking directly, by constructing both engines from the identical
collaborators and asserting their results are equal rather than merely
similar:

```csharp
[Theory]
[MemberData(nameof(Scenarios))]
public void Both_implementations_satisfy_the_same_contract(RateRequest request, DateTimeOffset at)
{
    var tariffs = new StandardTariffSource();
    var clock = new FakeClock(at);

    var refactored = new RefactoredRateEngine(tariffs, clock).Quote(request);
    var rewritten = new RewrittenRateEngine(tariffs, clock).Quote(request);

    Assert.Equal(refactored, rewritten);
}
```

`RateQuote` is a record, so `Assert.Equal` compares all three of its
members, `ConsignmentId`, `Amount`, and `QuotedAt`, by value rather than by
reference. The six scenarios the `[Theory]` runs against span both sides of
the peak-hour boundary and three different lanes, including a zero-value
request and a request large enough to exercise rounding at scale. Passing
on all six means the refactored and rewritten engines do not merely compute
similar numbers; given the same request and the same instant, they produce
record-equal quotes, every time, across the inputs most likely to expose a
disagreement if one existed. That theory is what turns "a clean rewrite
feels right" into a checked fact rather than an impression. Without it, a
team adopting the rewrite would be trusting that a ground-up rewrite
happened to preserve every edge case the original handled, on the strength
of having read the new code carefully. With it, the trust has something to
point at.

{% include excalidraw.html name="44-same-contract-two-paths" caption="Figure 44.3: Two engines sit behind one IRateEngine contract and, fed the same request and clock, return record-equal quotes. QuoteService swaps between them with a one-line change at the construction site." %}

## QuoteService: the consumer that never has to choose

The equivalence proof only matters to a caller if that caller is actually
bound to the shared contract rather than to one engine's concrete type.
`QuoteService` is that caller:

```csharp
public sealed class QuoteService(IRateEngine engine)
{
    public RateQuote QuoteFor(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return engine.Quote(request);
    }
}
```

`QuoteService` takes an `IRateEngine`, not a `RefactoredRateEngine` or a
`RewrittenRateEngine`, and its one method does nothing but forward to that
interface. `RefactorOrRewriteTests.Quote_service_works_with_the_refactored_engine_behind_the_seam`
and `Quote_service_works_with_the_rewritten_engine_behind_the_seam` construct
a `QuoteService` around each engine in turn, over the identical request and
clock, and assert the same amount both times. The test method bodies differ
by exactly one line, the constructor call that builds the engine passed
into `QuoteService`; `QuoteFor` itself is never touched. That one-line
difference is the entire cost of switching a running system from the
refactored engine to the rewritten one, or back, once both sit behind
`IRateEngine`. A refactor-or-rewrite decision made without a seam like this
one commits a codebase the moment the new implementation starts replacing
the old call sites one by one. A decision made behind a stable interface
stays reversible for as long as the interface holds, because the thing
every caller depends on never had to change to make either choice.

The remaining tests round out the proof that the seam does real work
beyond the equivalence theory. `Injected_tariff_source_can_be_substituted_in_both_engines`
drives both engine factories through an NSubstitute double for
`ITariffSource`, confirming a test can control the rate lookup for either
implementation without touching either engine's source. `Internal_peak_window_is_reachable_through_internals_visible_to`
exercises `PeakWindow.IsPeak` directly across the peak boundary's edges,
reachable from the test project through the project file's
`InternalsVisibleTo` entry even though `PeakWindow` is `internal` and
exposes nothing on `IRateEngine`'s own public surface. Neither test needed
a new interface to reach the thing it checks; `InternalsVisibleTo` already
opened that seam for a fixed rule that has exactly one production shape,
the same restraint that applies to internal seams generally.

## Build, run, observe

The example behind this chapter is `examples/44-refactor-or-rewrite`: the
legacy `LegacyRateEngine` with no stable seam, the `IRateEngine` contract,
the refactor-in-place and clean-rewrite replacements built against it, the
equivalence theory, and `QuoteService` as the consumer bound only to the
interface. It is standalone-testable from its own directory:

```sh
cd examples/44-refactor-or-rewrite
dotnet test
```

Observed locally with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 21
  failed: 0
  succeeded: 21
  skipped: 0
```

The twenty-one cases split across three test classes. `LegacyContrastTests`
contributes one, the reflection-based proof that `LegacyRateEngine` is
sealed, non-virtual, and does not implement `IRateEngine`.
`RefactorOrRewriteTests` contributes sixteen: four facts checking each
engine's amount off-peak and during the peak window, two facts proving
`QuoteService` works unchanged behind either engine, a two-case theory
substituting a fake tariff source into both engine factories, a five-case
theory walking the peak window's hour boundaries through
`InternalsVisibleTo`, and a fact confirming `StandardTariffSource` returns
the expected rate. `EquivalenceTests` contributes the remaining theory, six
scenarios asserting record equality between the refactored and rewritten
engines' quotes across both sides of the peak window and three lanes.

## When not to use it

Neither refactor nor rewrite is the right default for every piece of
inherited code, and the same four criteria that argue for one over the
other sometimes argue against touching the code at all right now. Code with
thin coverage, low change frequency, a narrow blast radius, and a domain
that is still shifting is a weak candidate for a ground-up rewrite, because
a rewrite's main payoff, a cleaner shape to build future changes on, never
gets collected if no future change arrives, and its main risk, silently
dropping a rule the old code handled, is hardest to catch with coverage
that thin. The same facts argue just as strongly against an incremental
refactor if the team's real goal is replacing the code's behavior rather
than preserving it; refactoring is for code whose current behavior is worth
keeping, not a slower way to arrive at a rewrite while telling a passing
suite that nothing changed.

A stable seam like `IRateEngine` is also not free, and not every piece of
code earns one. Introducing an interface, a second implementation, and an
equivalence theory for a calculation that has one caller, changes twice a
decade, and backs nothing larger than an internal report costs more than it
returns. The seam pays for itself when more than one implementation is
plausible on its own terms, when the decision needs to stay reversible
while evidence accumulates, or when a caller needs to depend on a contract
rather than on a single concrete type for reasons beyond this one decision.
Reach for the criteria and the seam when a refactor-or-rewrite question is
still open; skip both when the clearer answer is that the code should be
left alone.

<blockquote class="ai-sidebar" markdown="1">
Asked whether a piece of code should be refactored or rewritten, an AI
assistant can score it against test coverage, change frequency, blast
radius, and domain stability about as fast as it can read the code once,
and it can draft a rewrite behind a named interface, matching an existing
contract statement for statement, faster than most engineers can type the
method signature. Both save real time: weighing four criteria consistently
across a backlog of candidate files is tedious work an assistant does
without getting tired of it, and a drafted rewrite against a known contract
gives a team something concrete to react to instead of a blank page. What
an assistant's draft cannot settle is whether the rewrite it produced
actually computes what the original did. A model that writes confidently
does not write infallibly, and a rewrite that reads cleanly can still drop
a rounding rule, an edge case, or a boundary condition the original handled
correctly. That gap is exactly what an equivalence theory closes: running
the drafted rewrite and the existing implementation against the same
scenarios and asserting their results are equal, the way `Both_implementations_satisfy_the_same_contract`
does here, rather than trusting that a clean reading implies a correct one.
The weighing of criteria and the decision to proceed stay a human call;
confirming the result is correct is a check anyone can run, and should,
before either answer ships.
</blockquote>

## For deeper coverage

- Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source framing for finding and using a seam, the property that makes a refactor-in-place and a clean rewrite equally safe to attempt behind the same contract.
- Fowler, [*Refactoring*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the discipline behind the refactor-in-place path, small behavior-preserving steps rather than a wholesale replacement.
- Microsoft Learn, [Interfaces](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces): the language reference for the `IRateEngine` contract both replacement engines implement.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection): constructor injection as the idiom behind `RefactoredRateEngine`, `RewrittenRateEngine`, and `QuoteService` each receiving their collaborators rather than constructing them.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records): the value-equality semantics `RateQuote` relies on for the equivalence theory's `Assert.Equal` comparison.
- Microsoft Learn, [InternalsVisibleToAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute): the attribute behind the seam onto `PeakWindow.IsPeak`.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
