---
title: "Mediator and Chain of Responsibility"
order: 22
layout: tutorial
sectionid: docs
part: "coordinating-traversing"
description: "The pipeline shape, with Kestrel middleware mentioned once as an illustration rather than a hosting subject."
---

<!-- EXAMPLE (shipping domain): Customs clearance: document check, duty calculation, inspection, release. -->

## Learning objectives

By the end of this chapter you can:

- Build a `ClearanceStage` chain where each link advances a `CustomsDeclaration` or halts it by returning a non-cleared `ClearanceResult`, never by throwing.
- Record each stage's type name into a shared `ClearanceContext` from the base class, so a caller reads which stages ran without instrumenting any stage.
- Assemble a fixed pipeline with `SetNext`, as `CustomsClearancePipeline.Standard` does, and hold it by its first link.
- Build a Mediator, `CustomsMediator`, that routes messages between two colleagues that never reference each other, registered after construction to break a circular dependency.
- Tell a stage that halts a chain from a mediator that routes between peers, and recognize when either is more structure than a few `if` statements or a direct call.

## Why this matters

A consignment entering customs passes an ordered sequence of checks, each able to stop it, none aware of what the others do. Missing paperwork never reaches an inspector. A failed inspection never reaches release. A declaration that clears needs a duty figure computed along the way and carried to the end. Written as one method of `if` statements, the order of checks is the order of lines in a function body; reordering, adding a fifth check, or running three of four for a lighter declaration all mean editing that function.

A customs house also runs desks that coordinate without depending on each other. The declaration desk and the duty desk each need to hear from the other, but wiring every desk to every other desk becomes a web of references that grows with each new desk.

Chain of Responsibility gives each check its own link, wired to a successor it otherwise knows nothing about. Mediator gives the desks one thing to send through, so a desk sends and receives while the mediator decides who hears what.

{% include excalidraw.html name="22-mediator-and-chain-of-responsibility" caption="Figure 22.1: CustomsClearancePipeline chains four stages that each pass a declaration forward or halt it, while CustomsMediator routes messages between two desks that never reference each other directly." %}

## Pattern: Chain of Responsibility

### The problem

Four checks run in a fixed order: documents, duty, inspection, release. Any of the first three may need to stop the declaration, later stages must not run once one does, and the order or membership of the sequence should change without editing every check.

### The solution

A **Chain of Responsibility** passes a request along a chain of handlers, each of which handles its own part and either passes the request on or stops it.

### How C# models it

The declaration is an immutable record of what the officer knows before any check runs:

```csharp
public sealed record CustomsDeclaration(
    string ConsignmentId,
    decimal DeclaredValue,
    bool DocumentsComplete,
    bool RequiresInspection,
    bool InspectionPassed);
```

What the chain accumulates while running lives in a separate type:

```csharp
public sealed class ClearanceContext
{
    private readonly List<string> _stagesRun = [];

    public ClearanceContext(CustomsDeclaration declaration)
    {
        Declaration = declaration;
    }

    public CustomsDeclaration Declaration { get; }

    public IReadOnlyList<string> StagesRun => _stagesRun;

    public decimal Duty { get; set; }

    internal void RecordStage(string stageName) => _stagesRun.Add(stageName);
}
```

`StagesRun` is an `IReadOnlyList<string>` over a private list, so a caller can read which stages ran but only the chain, through the internal `RecordStage`, can write to it. `Duty` is a plain settable property because only one stage sets it.

The base class holds the chain's two rules: record the stage, and continue only while the declaration is cleared.

```csharp
public abstract class ClearanceStage
{
    private ClearanceStage? _next;

    public ClearanceStage SetNext(ClearanceStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _next = stage;
        return stage;
    }

    public ClearanceResult Handle(ClearanceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.RecordStage(GetType().Name);
        var result = Process(context);
        if (!result.Cleared)
        {
            return result;
        }

        return _next?.Handle(context) ?? result;
    }

    protected abstract ClearanceResult Process(ClearanceContext context);
}
```

`Handle` isn't virtual, so no stage can skip the bookkeeping. `GetType().Name` reads the concrete class name at run time; no stage names itself as a string. `Process` is the one decision each stage makes, returned as:

```csharp
public sealed record ClearanceResult(bool Cleared, string? HaltedStage, string? Reason, decimal Duty);
```

A stage halts by returning `Cleared: false`. `Handle` then returns that result and never touches `_next`. Otherwise it passes the same context to `_next?.Handle(context)`, and at the last link, where `_next` is null, returns the current result.

### The stages

The first link halts as early as possible:

```csharp
public sealed class DocumentCheckStage : ClearanceStage
{
    protected override ClearanceResult Process(ClearanceContext context)
    {
        if (!context.Declaration.DocumentsComplete)
        {
            return new ClearanceResult(
                Cleared: false,
                HaltedStage: nameof(DocumentCheckStage),
                Reason: "Required clearance documents are missing.",
                Duty: context.Duty);
        }

        return new ClearanceResult(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
    }
}
```

The second never halts; it computes a figure and lets the chain continue:

```csharp
public sealed class DutyCalculationStage : ClearanceStage
{
    private const decimal DutyRate = 0.08m;

    protected override ClearanceResult Process(ClearanceContext context)
    {
        context.Duty = context.Declaration.DeclaredValue * DutyRate;
        return new ClearanceResult(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
    }
}
```

`InspectionStage` halts only on `RequiresInspection && !InspectionPassed`, so a declaration never flagged for inspection passes whatever `InspectionPassed` says. The last link always clears, because reaching it means every earlier stage did:

```csharp
public sealed class ReleaseStage : ClearanceStage
{
    protected override ClearanceResult Process(ClearanceContext context) =>
        new(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
}
```

### Wiring the pipeline

`Standard` is the one place that builds the chain:

```csharp
public static class CustomsClearancePipeline
{
    public static ClearanceStage Standard()
    {
        var documentCheck = new DocumentCheckStage();
        var dutyCalculation = new DutyCalculationStage();
        var inspection = new InspectionStage();
        var release = new ReleaseStage();

        documentCheck.SetNext(dutyCalculation).SetNext(inspection).SetNext(release);

        return documentCheck;
    }
}
```

`SetNext` returns the stage it was given, not the one it was called on, so the chained calls read left to right in run order. `Standard` returns only `documentCheck`; that first link is all a caller needs.

| Declaration | `StagesRun` | Result |
| --- | --- | --- |
| Documents complete, no inspection, value 1000 | All four, in order | Cleared, duty 80 |
| `DocumentsComplete: false` | `DocumentCheckStage` only | Halted at `DocumentCheckStage` |
| Documents complete, inspection failed | Document check, duty, inspection | Halted at `InspectionStage`; duty already recorded |
| Value 2500, passing | All four | Cleared, duty 200 in both the result and the context |

The ASP.NET Core middleware pipeline that Kestrel runs each HTTP request through has the same shape: each component calls the next or short-circuits the response, as [Microsoft Learn documents](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/).

## Pattern: Mediator

### The problem

The declaration desk and the duty desk tell each other what they did. If each holds a reference to the other, adding a third desk means changing both, and the references multiply with each desk.

### The solution

A **Mediator** encapsulates how a set of objects interact, so the objects refer to the mediator instead of to each other.

### How C# models it

A desk depends only on this:

```csharp
public interface IClearanceMediator
{
    void Send(string message, ClearanceColleague sender);
}
```

The shared base holds the mediator and an inbox:

```csharp
public abstract class ClearanceColleague
{
    private readonly List<string> _inbox = [];

    protected ClearanceColleague(IClearanceMediator mediator)
    {
        Mediator = mediator;
    }

    protected IClearanceMediator Mediator { get; }

    public IReadOnlyList<string> Inbox => _inbox;

    public void Receive(string message) => _inbox.Add(message);

    public void Send(string message) => Mediator.Send(message, this);
}
```

`Send` never names another colleague; it passes the message and `this` to the mediator, which works out the recipient. `Inbox` is readable but not writable from outside. The desks add only a constructor:

```csharp
public sealed class DeclarationDesk : ClearanceColleague
{
    public DeclarationDesk(IClearanceMediator mediator)
        : base(mediator)
    {
    }
}
```

`DutyDesk` is identical. Neither has a field, property or constructor parameter referring to the other; `DeclarationDesk`'s source gives no hint that `DutyDesk` exists. The relationship lives in the mediator:

```csharp
public sealed class CustomsMediator : IClearanceMediator
{
    private DeclarationDesk? _declarationDesk;
    private DutyDesk? _dutyDesk;

    public void Register(DeclarationDesk declarationDesk, DutyDesk dutyDesk)
    {
        _declarationDesk = declarationDesk;
        _dutyDesk = dutyDesk;
    }

    public void Send(string message, ClearanceColleague sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        if (ReferenceEquals(sender, _declarationDesk))
        {
            _dutyDesk?.Receive(message);
        }
        else if (ReferenceEquals(sender, _dutyDesk))
        {
            _declarationDesk?.Receive(message);
        }
    }
}
```

### Registering after construction

Each desk needs the mediator in its constructor, and the mediator needs both desks before it can route, so neither can be built first through constructors alone. Build the mediator, build both desks against it, then call `Register(declarationDesk, dutyDesk)`. Wiring after every object exists breaks the cycle.

### Routing by identity

`Send` compares `ReferenceEquals(sender, _declarationDesk)`, not message content or a type check, because identity is all it needs to know. Each branch delivers to the other party only, so a sender never finds its own message in its inbox.

| Send | `DutyDesk.Inbox` | `DeclarationDesk.Inbox` |
| --- | --- | --- |
| `declarationDesk.Send("Consignment CN-2001 declared.")` | The message | Empty |
| A reply from `dutyDesk` | The original message only | The reply |

Checking the sender's inbox matters: a mediator that delivered to both desks would still put the message in the recipient's inbox.

{% include excalidraw.html name="22-mediator-colleagues" caption="Figure 22.2: CustomsMediator routes a message between DeclarationDesk and DutyDesk and a reply back the other way; each desk calls Send only on the mediator, never on each other, and a sender never sees its own message echoed back." %}

## When not to use it

- **Chain of Responsibility.** Use it when the sequence changes shape in practice: a check dropped for a lighter declaration, one inserted between two others, or links reused in a second pipeline. A fixed pair of conditions that never grows needs two `if` statements, not four classes. The tell is one caller that always builds the chain the same way.
- **Mediator, too early.** Two desks that will stay two, exchanging one kind of message, get little that a direct call on an injected `DutyDesk` wouldn't say in fewer lines.
- **Mediator, too much.** A `Send` that branches on message content to compute duty or validate a declaration has stopped routing. It centralizes every rule in one class, the concentration the pattern exists to prevent.

<blockquote class="ai-sidebar" markdown="1">
Asked to make two conditions "more flexible" or "ready for growth," an assistant will sometimes produce `ClearanceStage` subclasses, `Process` overrides and `SetNext` wiring for a pair of checks that will always be that pair. A related suggestion routes a single direct call between two classes through an `IClearanceMediator` and a `Send` dispatch. A generated mediator can also drift the other way, with `Send` branching on message content to make decisions the colleagues should own. Check a generated answer against two tests: can the sequence plausibly change shape or be reused (a chain), and are there enough participants, or volatile enough relationships, that direct references would be harder to read than one router (a mediator)? A fixed pair of conditions or collaborators usually reads better as a plain `if` or a plain method call.
</blockquote>

## Build, run, observe

```sh
cd examples/22-mediator-and-chain-of-responsibility
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. Every stage and colleague is synchronous and in-memory. All 7 tests pass: four for the chain (full run, document halt, inspection halt, duty in the result) and three for the mediator (delivery, no echo, reply).

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Mediator and Chain of Responsibility.
- Microsoft Learn, [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/): the Kestrel request pipeline, an ordered chain in which each link decides whether to continue.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
