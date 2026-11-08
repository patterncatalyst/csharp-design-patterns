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

- Name the two lines in the sealed `RateEngineSeams.Legacy.RateEngine` that close it to substitution: `new LegacyTariffTable()` and the `DateTime.Now.Hour` check.
- Prove a class has no seam from its shape, through reflection, without racing the wall clock.
- Open a constructor seam by extracting `ITariffSource` and accepting it through a primary constructor.
- Wrap an ambient value behind `IClock`, and say when the base class library's `TimeProvider` is the better choice.
- Tell a constructor seam, an extract-and-override seam (`protected virtual PeakSurcharge`) and an internal seam (`PeakWindow.IsPeak` through `InternalsVisibleTo`) apart, and match each to the dependency it opens.

## Why this matters

Michael Feathers defines legacy code as code without tests. The obstacle to adding one is finding a point where a collaborator can be swapped for a test double before the real logic runs. Feathers calls that point a **seam**.

In C#, a method is non-virtual until its author writes `virtual`, and `sealed` closes a class to inheritance entirely. That default has a benefit: nothing outside a sealed class can change its behavior through inheritance its author didn't plan for. The cost appears when a test arrives. A class built without a seam gives a test nothing to substitute, and inheritance can't create a seam the author never left.

Each kind of closed dependency has a named move:

| Closed dependency | Move |
| --- | --- |
| A hard-coded `new` | A constructor parameter |
| An ambient static call | An interface wrapper |
| A rule inside a method | A `protected virtual` method |
| A rule the public surface shouldn't expose | `InternalsVisibleTo` for the tests' assembly |

Picking the move that matches the dependency, instead of marking every member `virtual` and every field `public`, keeps a legacy change narrow.

{% include excalidraw.html name="41-seams-non-virtual-by-default" caption="Figure 41.1: A sealed RateEngine with its dependencies built inside offers no way in. Adding a constructor and a protected override opens a seam where tests and other behavior can substitute collaborators." %}

## The legacy engine: both collaborators wired inside

The legacy engine is a common shape in an aging codebase: one `sealed class`, one public method, and everything the calculation needs built inside it.

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

Two lines close it to a test:

- `new LegacyTariffTable()` builds the tariff inside the method, so there's no parameter, property or factory call to redirect. `LegacyTariffTable`'s constructor seeds its one rate, `STD` at `2.40m` per kilogram, so even reaching it wouldn't let a caller change the rate.
- `DateTime.Now.Hour` reads the wall clock directly. A check that the peak surcharge applies can only pass during the real peak window, and a check that it doesn't can only pass outside it.

Both values arrive through code inside the method rather than through a parameter. Because `RateEngine` is `sealed` and `Quote` isn't `virtual`, subclassing and overriding `Quote` isn't available either.

## Proving there is no seam without racing the clock

An assertion against `DateTime.Now` would pass or fail depending on when it runs. Asserting on the type's shape avoids the clock:

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

Each fact rules out a different repair. `IsSealed` rules out a subclass. One constructor with no parameters rules out handing in a substitute tariff table or clock. A non-virtual `Quote` would refuse an override even without `sealed`. Together they show the engine's untestability is a property of its structure, not of the numbers it computes.

## Opening a constructor seam: an interface in place of a hard-coded new

`ITariffSource` names the one operation `LegacyTariffTable` buries inside a `new`:

```csharp
public interface ITariffSource
{
    decimal RatePerKilogram(string laneCode);
}
```

`StandardTariffSource` implements it over the same lane data, `STD` at `2.40m`, so production numbers don't change:

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

The refactored `RateEngine` takes an `ITariffSource` through its primary constructor instead of building one (the full class appears with the surcharge seam below). A parameter where there was a `new` is the whole repair. Production passes a `StandardTariffSource` and gets the legacy numbers. A test passes an NSubstitute substitute returning `5.00m` for `STD`: a request with `1000.00m` declared and `200.00m` kg then quotes `1015.00m` instead of the real `495.00m`, and the substitute receives exactly one `RatePerKilogram("STD")` call. `Quote`'s logic didn't change; only where the `ITariffSource` comes from did.

## Wrapping ambient time: IClock, SystemClock, and TimeProvider

`DateTime.Now` is a static property, so no constructor parameter can replace it directly. Wrap it behind an interface instead:

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

`SystemClock` is the only place in the refactored code that reads the real clock. Everywhere else, `RateEngine` asks its injected `IClock`. A `FakeClock` returning a fixed `DateTimeOffset` makes the quote deterministic: fixed at eleven at night, outside the peak window, the quote is `495.00m` on every run, and `RateQuote.QuotedAt` equals the fixed instant. The engine reads time through the seam for both the surcharge and the timestamp.

The base class library ships the same wrapper: `TimeProvider`, an abstract class with `GetUtcNow()` and a `System` instance for the real clock, with `FakeTimeProvider` in a separate testing package.

| Choice | Fits when |
| --- | --- |
| A minimal `IClock` | The code needs only the current instant, as one rate quote does |
| `TimeProvider` | The code also needs timers, delays or other scheduling |

Either opens the same seam: a static clock call becomes something a test controls.

## The extract-and-override seam: a protected virtual hook, and why this class isn't sealed

The peak surcharge is a rule, not a collaborator: peak hours add ten percent. The refactored engine extracts it into a `protected virtual` method:

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

`sealed` and `virtual` work against each other: a `virtual` member exists to be overridden, and `sealed` forbids subclasses. Leaving `RateEngine` open trades the guarantee `sealed` gives the legacy type for the ability to vary one rule without touching `Quote`.

A test subclass replaces exactly that rule:

```csharp
public sealed class TestableRateEngine(ITariffSource tariffs, IClock clock) : RateEngine(tariffs, clock)
{
    public const decimal FixedSurcharge = 7.00m;

    protected override decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) => FixedSurcharge;
}
```

With a substitute tariff at `5.00m` and a clock inside the peak window, `TestableRateEngine` quotes `1015.00m` plus its fixed `7.00m`, whatever `PeakWindow.IsPeak` says. The subclass leaves the tariff and clock seams alone. The production rule and the override live in two methods connected by inheritance, instead of one method branching on whether it's under test.

## The internal seam: reaching PeakWindow through InternalsVisibleTo

The default `PeakSurcharge` delegates the boundary to an `internal` type:

```csharp
internal static class PeakWindow
{
    internal static bool IsPeak(DateTimeOffset at) => at.Hour is >= 8 and < 18;
}
```

`internal` keeps the rule out of the public surface, since `PeakSurcharge` is its only production caller. A test project is a different kind of caller, and one attribute in `RateEngineSeams.csproj` opens every `internal` member to the named test assembly alone:

```xml
<ItemGroup>
    <InternalsVisibleTo Include="RateEngineSeams.Tests" />
</ItemGroup>
```

That pins the boundary directly: hours `7`, `8`, `17`, `18` and `23` give `false`, `true`, `true`, `false` and `false`. A change to the surcharge percentage never touches the boundary checks, and a change to the boundary never touches the surcharge checks, because each reaches its rule through its own seam.

{% include excalidraw.html name="41-c-sharp-seam-kinds" caption="Figure 41.2: Constructor injection, wrapping an ambient clock, a protected override hook, and an internal-only entry point open four different seams in the same RateEngine, each suited to a different kind of dependency." %}

## Four seams, one engine, one choice each time

Each seam answers a different question:

| Seam | Over | Question it answers |
| --- | --- | --- |
| Constructor | `ITariffSource` | Which object computes this value? |
| Interface wrapper | `IClock` | Which instant counts as now? |
| Extract and override | `PeakSurcharge` | Which rule decides this one outcome? |
| Internal | `PeakWindow.IsPeak` | Who besides the engine may see this rule? |

The legacy engine has none of them, and that isn't an oversight. It's what a C# class looks like when nobody chose any of the four moves, because declaring a class or method opens none of them by default. The fix is never to mark everything `virtual` and `public`. Pick the narrowest move that opens the one point a test needs, and leave the rest of the class as closed as it was.

Nothing here needed a new language feature. The primary constructor is ordinary syntax, and `at.Hour is >= 8 and < 18` is the legacy check carried over unchanged. What changed is four decisions about where a dependency enters the class.

## Build, run, observe

```sh
cd examples/41-seams-non-virtual-by-default
dotnet test
```

All 13 tests pass: one reflection check on the legacy engine's shape, and twelve on the refactored engine covering the fixed clock, the surcharge inside and outside the peak window, the timestamp, the substituted tariff, the overridden surcharge, the standard tariff rate and five boundary hours. `scripts/verify.sh` runs the same project with every other example.

## When not to use it

A seam has a cost. The case for opening seams in a closed legacy class isn't a case for opening them on a new class before anything needs them. Ten `protected virtual` methods nobody overrides, or ten constructor parameters for collaborators nobody varies, cost a reader indirection for variation that doesn't exist. The four seams in the refactored `RateEngine` each have a caller that varies them. A new class gets a seam under the same condition as any design choice: a collaborator a test or a real caller has to vary now, not a guess about someday. The legacy engine is the opposite extreme, and its fix isn't to open every point on every new class from the first line.

> **🤖 Explore with your agent**
>
> "Starting from `Legacy.RateEngine`, write `RateEngineSeams.RateEngine`. Replace `new LegacyTariffTable()` and the `DateTime.Now` read with constructor parameters `ITariffSource tariffs` and `IClock clock`, and read the time once from `clock.UtcNow`. Make only `PeakSurcharge` `protected virtual`; leave `Quote` non-virtual, and add no other virtual members, `protected` fields or constructor parameters. Keep `PeakWindow` `internal` and reach it from tests through the `InternalsVisibleTo` item in `RateEngineSeams.csproj`."
>
> Run `dotnet test --project RateEngineSeams.Tests --filter "SeamTests|LegacyContrastTests"`; all 13 must pass.

## For deeper coverage

- Michael Feathers, [*Working Effectively with Legacy Code*](https://www.oreilly.com/library/view/working-effectively-with/0131177052/) (Prentice Hall, 2004): the source of the term seam, and the taxonomy of seam types the four seams here draw from.
- Martin Fowler, [*Refactoring: Improving the Design of Existing Code*](https://martinfowler.com/books/refactoring.html), 2nd ed. (Addison-Wesley, 2018): the Extract Method and Extract Interface refactorings behind the move from an inline `if` to `protected virtual PeakSurcharge`, and from a hard-coded `new` to `ITariffSource`.
- Microsoft Learn, [TimeProvider Class](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider): the base class library's own abstraction over the current time, the authority behind the comparison between `IClock` and `TimeProvider`; the language reference's own pages on `virtual`, `sealed`, and `InternalsVisibleToAttribute` cover the other three seam kinds.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
