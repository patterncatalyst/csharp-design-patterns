---
title: "Seams in C#: non-virtual by default"
order: 41
layout: tutorial
sectionid: docs
part: "changing-inherited-code"
description: "The finding that inverts expectations: you cannot override to make a seam, so Feathers matters all the more in C#."
---

<!-- EXAMPLE (shipping domain): A legacy RateEngine: sealed, non-virtual, untestable. -->

## Learning objectives

By the end of this chapter you can:

- Explain why the sealed `RateEngineSeams.Legacy.RateEngine` offers no way to substitute either of its two collaborators, and name the two specific lines in `Quote`, the `new LegacyTariffTable()` call and the `DateTime.Now.Hour` check, where that closing-off happens.
- Read `LegacyContrastTests.Legacy_engine_offers_no_seam_for_the_clock_or_the_tariff_source` and explain what its reflection assertions, `IsSealed`, a single parameterless constructor, a non-virtual `Quote`, prove that an assertion against the wall clock could not prove reliably.
- Open a constructor seam over a collaborator by extracting an interface, `ITariffSource`, and accepting it through a primary constructor, the change `RateEngineSeams.RateEngine` makes in place of `new LegacyTariffTable()`.
- Wrap an ambient value behind an interface seam, `IClock`, name the .NET base class library's own equivalent, `TimeProvider`, and explain why this example keeps its own minimal interface instead of taking that dependency.
- Tell apart three further seam kinds in the same class: a constructor seam, an extract-and-override seam (`protected virtual PeakSurcharge`, overridden by `TestableRateEngine`), and an internal seam (`PeakWindow.IsPeak`, reached through `InternalsVisibleTo`), and say which collaborator or rule each one targets.

## Why this matters

Michael Feathers defines legacy code as code with no tests, and the obstacle to adding one is rarely the test itself. It is finding a point in the code where a collaborator can be swapped for a test double before the real logic runs. Feathers calls that point a seam. In many object systems, overriding a method nobody wrote for testing hands a caller that point automatically, because a method stays open to override until its class closes it off. C# runs the opposite default: a method stays non-virtual until its author writes `virtual`, and a class can close inheritance entirely with `sealed`. That default carries a real benefit. A sealed class with non-virtual methods gives a reader and the compiler a guarantee that nothing outside the class can change its behavior through inheritance its author never anticipated. The cost of that guarantee shows up exactly where this chapter starts: a class built without a seam in mind gives a later test nothing to substitute, and no amount of inheritance can manufacture a seam its author never left behind.

Closing that gap takes a small set of named moves rather than guesswork. A constructor parameter in place of a hard-coded `new`. An interface in place of an ambient static call. A `protected virtual` method in place of a private one. `InternalsVisibleTo` in place of a wider public surface. Each move answers a different kind of closed dependency, and picking the move that matches the dependency in front of a reader, rather than marking every member `virtual` and every field `public` out of habit, is what keeps a legacy refactor narrow.

{% include excalidraw.html name="41-seams-non-virtual-by-default" caption="Figure 41.1: A sealed RateEngine with its dependencies built inside offers no way in. Adding a constructor and a protected override opens a seam where tests and other behavior can substitute collaborators." %}

## The legacy engine: both collaborators wired inside

`RateEngineSeams.Legacy.RateEngine` is the shape a reader runs into most often in an aging codebase: a single `sealed class` with one public method, `Quote`, that does everything the rate calculation needs without asking for help from outside the method:

```csharp
namespace RateEngineSeams.Legacy;

public sealed class RateEngine
{
    public decimal Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tariff = new LegacyTariffTable();
        var subtotal = request.DeclaredValue * 0.015m;
        subtotal += request.WeightKilograms * tariff.RatePerKilogram("STD");

        if (DateTime.Now.Hour is >= 8 and < 18)
        {
            subtotal *= 1.1m;
        }

        return subtotal;
    }
}
```

Two lines close off this method to a test. `var tariff = new LegacyTariffTable();` builds the tariff collaborator inside the method that uses it, so a test has no parameter, no property, and no factory call to redirect toward a tariff table carrying a known rate. `LegacyTariffTable` compounds the problem on its own: its constructor seeds the one rate it knows, `STD` at `2.40m` per kilogram, so even reaching the real collaborator from outside the engine would not let a caller substitute a different rate. The `DateTime.Now.Hour` check carries the same shape for a different kind of dependency. `Quote` reads the ambient wall clock directly rather than asking anything for the current time, so a test asserting the peak-hour surcharge applies has exactly one option, running during the real peak window on the real machine, and a test asserting the surcharge does not apply has exactly the complementary, equally unworkable, option.

Both problems trace back to the same root. `Quote` has no point where an outside caller can insert a substitute for either value, `LegacyTariffTable` or `DateTime.Now`, because both values arrive through code written directly inside the method rather than through a parameter. `RateEngine` being `sealed`, and `Quote` carrying no `virtual` modifier, removes the one path that might have papered over the gap: subclassing the engine and overriding `Quote` to compute the subtotal a different way. There is no subclass to write. The type closes that door along with the constructor-injection door the `new` already closed.

## Proving there is no seam without racing the clock

A test that wants to demonstrate this engine's untestability without itself becoming flaky cannot assert on `DateTime.Now`, because an assertion timed against the peak window would pass or fail depending on when the suite happens to run. `LegacyContrastTests.Legacy_engine_offers_no_seam_for_the_clock_or_the_tariff_source` sidesteps the clock entirely and asserts on the type's shape instead, through reflection:

```csharp
var legacyEngineType = typeof(Legacy.RateEngine);

Assert.True(legacyEngineType.IsSealed);

var constructors = legacyEngineType.GetConstructors();
var constructor = Assert.Single(constructors);
Assert.Empty(constructor.GetParameters());

var quoteMethod = legacyEngineType.GetMethod("Quote", BindingFlags.Public | BindingFlags.Instance);
Assert.NotNull(quoteMethod);
Assert.False(quoteMethod.IsVirtual);
```

Four assertions, each pinned to a structural fact rather than a runtime outcome. `IsSealed` confirms no subclass can exist to override anything. `Assert.Single` on the constructor list, followed by `Assert.Empty` on that one constructor's parameters, confirms there is exactly one way to build a `RateEngine` and it accepts nothing, so no caller can hand it a substitute `LegacyTariffTable` or clock at construction time. `IsVirtual` on the `Quote` method handle returns `false`, confirming that even without the `sealed` modifier on the class, this one method would still refuse an override. Any single fact among those four would close off a different repair; together they rule out a constructor seam, an interface seam, and an override seam in one assertion block, which is the reflection-based proof that this engine's untestability is a property of its structure, not an artifact of whatever numbers `Quote` happens to compute today.

## Opening a constructor seam: an interface in place of a hard-coded new

The first collaborator the refactored engine frees is the tariff lookup. `ITariffSource` names the one operation `LegacyTariffTable` buries inside a `new`:

```csharp
public interface ITariffSource
{
    decimal RatePerKilogram(string laneCode);
}
```

`StandardTariffSource` implements it over the same lane data `LegacyTariffTable` seeds, `STD` at `2.40m` per kilogram, so the production behavior does not change:

```csharp
public sealed class StandardTariffSource : ITariffSource
{
    private readonly Dictionary<string, decimal> _ratesByLaneCode = new(StringComparer.Ordinal)
    {
        ["STD"] = 2.40m,
    };

    public decimal RatePerKilogram(string laneCode)
    {
        ArgumentNullException.ThrowIfNull(laneCode);
        return _ratesByLaneCode[laneCode];
    }
}
```

The refactored `RateEngine` accepts an `ITariffSource` through a primary constructor instead of building one itself:

```csharp
public class RateEngine(ITariffSource tariffs, IClock clock)
{
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = request.DeclaredValue * 0.015m;
        subtotal += request.WeightKilograms * tariffs.RatePerKilogram("STD");
        // the clock and surcharge seams follow in the next two sections
    }
}
```

That single change, a parameter where there was a `new`, is the entire repair for this collaborator. A caller wiring the engine for production passes a `StandardTariffSource` and gets the same numbers `LegacyTariffTable` always produced. A test wiring the engine for a specific scenario passes something else: `SeamTests.Injected_tariff_source_can_be_substituted` hands it an NSubstitute substitute configured to return `5.00m` for the `STD` lane, quotes a `RateRequest` carrying a `DeclaredValue` of `1000.00m` and a `WeightKilograms` of `200.00m`, and asserts the quoted amount reflects that substituted rate rather than the real one, `1015.00m` instead of the `495.00m` the real `StandardTariffSource` would produce for the same request. `tariffs.Received(1).RatePerKilogram("STD")` on the same substitute confirms the engine called the collaborator exactly once, rather than only confirming the arithmetic happened to come out right. `SeamTests.Standard_tariff_source_returns_the_expected_rate` checks the production implementation on its own, calling `StandardTariffSource.RatePerKilogram("STD")` directly and asserting `2.40m`, the same rate `LegacyTariffTable` seeded, so the suite confirms separately that moving the rate behind an interface changed nothing about the number a real consignment is quoted. Nothing about `Quote`'s logic changed to make either test possible. The only change is where the `ITariffSource` instance comes from, inside the method or through the constructor, and that single relocation is what converts a closed dependency into an open one.

## Wrapping ambient time: IClock, SystemClock, and TimeProvider

The second collaborator the legacy engine hard-wires is not an object at all; it is a moment in time, read through the static `DateTime.Now` property. A static property call cannot be replaced by a constructor parameter directly, because nothing about calling `DateTime.Now` passes through an object a caller controls. The refactored engine wraps that ambient call behind an interface instead, the same move applied to a value rather than a collaborator object:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

`SystemClock` is the only place in the refactored code that still reads the real wall clock; everywhere else, `RateEngine` asks its injected `IClock` for `UtcNow` and uses whatever comes back. A test substitutes `FakeClock`, a small test double that returns a fixed `DateTimeOffset` given to its constructor, and gets a deterministic quote regardless of what time the test suite happens to run: `SeamTests.Refactored_engine_quotes_a_deterministic_amount_with_a_fixed_clock` fixes the clock at eleven at night, well outside the peak window, and asserts the exact amount, `495.00m`, every time the suite runs. `SeamTests.Refactored_engine_stamps_the_quote_with_the_injected_clock` goes one step further and asserts `RateQuote.QuotedAt` equals the fixed instant exactly, proving the engine reads time through the seam for both the surcharge decision and the quote's own timestamp, not only one of the two.

The .NET base class library ships a built-in version of exactly this wrapper: `TimeProvider`, an abstract class with a `GetUtcNow()` method and a `System` static instance backing the real clock, meant to be injected the same way `IClock` is injected here, with a companion `FakeTimeProvider` available for tests from a separate testing package. `RateEngineSeams` keeps its own minimal `IClock` interface rather than taking that dependency, because the one operation this engine needs, the current instant, is narrower than `TimeProvider`'s full surface of timers and delays, and a project with broader scheduling needs than a single rate quote is exactly where reaching for `TimeProvider` over a hand-rolled interface pays off. The seam the two choices open is identical: wherever production code calls a static clock-like API directly, nothing inside that method can be tested without waiting for the real clock, and wrapping the call behind an interface, whichever interface, restores the constructor seam a hard-coded `new` would also restore for an object.

## The extract-and-override seam: a protected virtual hook, and why this class isn't sealed

Not every dependency this engine has to open relates to a collaborator at all; one of them is a rule, peak hours draw a ten percent surcharge, embedded directly inside the original `Quote` method as an `if` statement. Rather than leaving that rule as a hard-coded conditional, the refactored `RateEngine` extracts it into its own method and marks that method `protected virtual`:

```csharp
public class RateEngine(ITariffSource tariffs, IClock clock)
{
    public RateQuote Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = request.DeclaredValue * 0.015m;
        subtotal += request.WeightKilograms * tariffs.RatePerKilogram("STD");

        var at = clock.UtcNow;
        var total = subtotal + PeakSurcharge(subtotal, at);

        return new RateQuote(request.ConsignmentId, total, at);
    }

    protected virtual decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) =>
        PeakWindow.IsPeak(at) ? subtotal * 0.1m : 0m;
}
```

`protected virtual` on `PeakSurcharge` is the reason this `RateEngine` cannot carry the `sealed` modifier the legacy type carries. Sealing a class and marking a member `virtual` work against each other: a `virtual` member exists to be overridden by a subclass, and `sealed` forbids subclasses outright. Leaving `RateEngine` open is a choice weighed against the exact benefit `sealed` offers the legacy type, a guarantee that no subclass can alter behavior, traded here for the ability to vary one specific rule, the peak surcharge, without touching `Quote` at all.

`RateEngineSeams.Tests.TestableRateEngine` is the subclass that exercises this seam:

```csharp
public sealed class TestableRateEngine(ITariffSource tariffs, IClock clock) : RateEngine(tariffs, clock)
{
    public const decimal FixedSurcharge = 7.00m;

    protected override decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) => FixedSurcharge;
}
```

`SeamTests.Extract_and_override_seam_lets_a_subclass_replace_the_surcharge` builds a `TestableRateEngine` with a substituted `ITariffSource` returning `5.00m` per kilogram and a clock fixed inside the peak window, quotes the standard request, and asserts the total equals the subtotal at that rate, `1015.00m`, plus the fixed `7.00m` surcharge `TestableRateEngine` always returns, regardless of `PeakWindow.IsPeak`'s own answer for that instant. The test subclass never touches the tariff lookup or the clock seam at all; it replaces exactly one rule, leaving the other two seams free for a different test to exercise on their own. That separation, one seam per concern, each one substitutable independently, is what an extract-and-override seam buys over simply inlining a second branch for the test scenario: the production rule and the test rule live in two methods connected by inheritance, rather than one method branching on whether it currently happens to be under test.

## The internal seam: reaching PeakWindow through InternalsVisibleTo

`PeakSurcharge`'s default implementation delegates the peak-hour boundary itself to a fourth type, `PeakWindow`, marked `internal`:

```csharp
internal static class PeakWindow
{
    internal static bool IsPeak(DateTimeOffset at) => at.Hour is >= 8 and < 18;
}
```

Marking `IsPeak` `internal` rather than `public` keeps the rule out of the engine's public surface; nothing outside this library needs to call `PeakWindow.IsPeak` directly in production, since `PeakSurcharge` is the one caller that rule exists for. A test project, though, is a different kind of outside caller than a production consumer, and C# gives that relationship its own seam: an `InternalsVisibleTo` assembly attribute, declared once in `RateEngineSeams.csproj`:

```xml
<ItemGroup>
    <InternalsVisibleTo Include="RateEngineSeams.Tests" />
</ItemGroup>
```

opens every `internal` member in `RateEngineSeams` to the named test assembly alone, without widening `PeakWindow` or `IsPeak` to `public` for every other consumer of the library. `SeamTests.Internal_peak_window_is_reachable_through_internals_visible_to` is a `[Theory]` that calls `PeakWindow.IsPeak` directly across five hours, `7`, `8`, `17`, `18`, and `23`, asserting `false`, `true`, `true`, `false`, and `false` in turn, pinning the exact boundary, peak starts at eight and ends before eighteen, independent of the surcharge arithmetic layered on top of it by `PeakSurcharge`. That independence matters for the same reason the extract-and-override seam's independence mattered: a change to the surcharge percentage never needs to touch this theory, and a change to the peak-hour boundary never needs to touch `SeamTests.Refactored_engine_applies_the_peak_surcharge_during_the_peak_window`, because each test reaches the one rule it checks through its own seam rather than through the whole `Quote` method.

{% include excalidraw.html name="41-c-sharp-seam-kinds" caption="Figure 41.2: Constructor injection, wrapping an ambient clock, a protected override hook, and an internal-only entry point open four different seams in the same RateEngine, each suited to a different kind of dependency." %}

## Four seams, one engine, one choice each time

Four seams sit inside one refactored class, and each answers a different question about where a dependency comes from. The constructor seam over `ITariffSource` answers which object computes this value. The interface seam over `IClock` answers which instant counts as now, the same move applied to a value read from a static call rather than an object built with `new`. The extract-and-override seam over `PeakSurcharge` answers which rule decides this one outcome, without reopening the rest of `Quote` to a subclass. The internal seam over `PeakWindow.IsPeak` answers who else besides this engine needs to see this rule, keeping it out of the public surface while still letting a test reach it directly.

None of the four exists in the legacy engine, and the legacy engine's own structure, sealed, one parameterless constructor, a non-virtual `Quote`, is not an oversight a later maintainer forgot to fix. It is what a C# class looks like when nobody chose any of the four moves this chapter walks through, because declaring a class or a method in C# opens none of these points by default. A test author arriving at a legacy class built this way faces the same four questions the refactored `RateEngine` answers, which collaborator, which value, which rule, and which audience, and the answer is never to mark every member `virtual` and every field `public` on the assumption that broader access is safer. It is to pick the narrowest move that opens exactly the one point a test actually needs: `ITariffSource` opens one collaborator, `IClock` wraps one static call, `protected virtual` opens one rule, and `InternalsVisibleTo` opens one assembly's view, leaving everything else about the class exactly as closed as it was before.

None of the four moves needed anything beyond current C# 14 to express. The primary constructor on `RateEngine(ITariffSource tariffs, IClock clock)` is the same primary-constructor syntax used everywhere else in this book, not a construct added specifically to make this engine testable. The pattern-matching range check in `PeakWindow.IsPeak`, `at.Hour is >= 8 and < 18`, reads the exact boundary the legacy `if (DateTime.Now.Hour is >= 8 and < 18)` already expressed, carried over unchanged because the rule itself never needed to change, only where it lived and how it could be reached. What changed between the legacy engine and the refactored one is never the language version underneath either file. It is four separate decisions about where a dependency enters a class, made once each, on a class that otherwise does exactly what the legacy one did.

## Build, run, observe

The example behind this chapter lives in `examples/41-seams-non-virtual-by-default`, the legacy and refactored rate engines over the same `RateRequest`, covered by one test project. It is standalone-testable from its own directory:

```sh
cd examples/41-seams-non-virtual-by-default
dotnet test
```

It is also picked up, alongside every other example, by the repo-level `scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 13
  failed: 0
  succeeded: 13
  skipped: 0
  duration: 892ms
```

The thirteen cases split across two test classes. `LegacyContrastTests` contributes one, the reflection-based proof that the legacy engine is sealed, has a single parameterless constructor, and exposes a non-virtual `Quote`. `SeamTests` contributes the remaining twelve: seven `[Fact]` methods covering a deterministic quote under a fixed clock, the surcharge applied inside the peak window and omitted outside it, the quote's timestamp matching the injected clock, an NSubstitute substitute standing in for `ITariffSource`, the extract-and-override seam replacing the surcharge rule through `TestableRateEngine`, and `StandardTariffSource` checked directly against the same rate the legacy table seeds, plus one `[Theory]` with five inline cases pinning the `PeakWindow.IsPeak` boundary at seven, eight, seventeen, eighteen, and twenty-three hours.

## When not to use it

Opening a seam has a cost of its own. The same argument for constructor injection, interface wrapping, `protected virtual`, and `InternalsVisibleTo` over a closed legacy class is not an argument for marking every member of a new class the same way before any test asks for it. A class with ten `protected virtual` methods nobody has ever overridden, or ten constructor parameters standing in for collaborators no test or production caller has ever varied, costs a reader an indirection to trace for a variation that does not exist. The four seams in this chapter's refactored `RateEngine` exist because the tests behind this chapter exercise all four: substituting the tariff source, fixing the clock, overriding the surcharge rule, and reaching the peak-hour boundary directly. A freshly written class, with no legacy weight behind it and no test yet written against it, calls for a seam under the same condition as any other design choice: a concrete need, a collaborator a test or a real caller has to vary right now, not a guess about what might need varying someday. The legacy engine in this chapter sits at the other extreme, a class that closed off all four of those points before a single test existed to need them, and the fix for that extreme is not to open every point on every new class from the first line.

<blockquote class="ai-sidebar" markdown="1">
An AI assistant reading a legacy class like the one in this chapter can be useful for spotting where a seam is missing: pointed at `RateEngineSeams.Legacy.RateEngine`, a capable assistant will reliably flag the hard-coded `new LegacyTariffTable()` and the direct `DateTime.Now` read as the two places a test cannot reach, and can draft the interface extraction or the clock wrapper that opens each one. That pattern match is narrow and mechanical: find a `new` inside a method body or a static call to an ambient API, and suggest a parameter or a wrapper in its place. It makes a reasonable first pass over an unfamiliar class.

Deciding where a seam actually belongs is a different question, and it is not one to hand to the assistant along with the first. An assistant asked to make a class testable tends to open every closed point it can find, marking methods `virtual`, widening fields to `protected`, and adding constructor parameters for collaborators no test or caller has ever needed to vary, because a suggestion that opens a point costs the assistant nothing and a suggestion that leaves one closed risks looking incomplete. A person reviewing that output still has to ask which of the four kinds of seam, a constructor parameter, an interface wrapper, a `protected virtual` method, or an `InternalsVisibleTo` grant, matches a real need a test or a production caller already has, and decline the rest. The legacy-versus-refactored contrast in this chapter is small enough to check by hand; a larger class handed to an assistant for the same treatment needs that same question asked just as carefully, once per suggested seam.
</blockquote>

## For deeper coverage

- Michael Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source of the term seam, and the taxonomy of seam types this chapter's four examples draw from.
- Martin Fowler, [*Refactoring: Improving the Design of Existing Code*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the Extract Method and Extract Interface refactorings behind the move from an inline `if` to `protected virtual PeakSurcharge`, and from a hard-coded `new` to `ITariffSource`.
- Microsoft Learn, [TimeProvider Class](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider): the base class library's own abstraction over the current time, the authority behind this chapter's comparison between `IClock` and `TimeProvider`; the language reference's own pages on `virtual`, `sealed`, and `InternalsVisibleToAttribute` cover the other three seam kinds this chapter opens.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
