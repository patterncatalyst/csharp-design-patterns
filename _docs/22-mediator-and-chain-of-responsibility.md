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

- Build a `ClearanceStage` chain where each link either advances a
  `CustomsDeclaration` to the next stage or halts it by returning a
  non-cleared `ClearanceResult`, never by throwing.
- Explain why `ClearanceStage.Handle` records its own type name into a shared
  `ClearanceContext` before delegating, so a caller can read exactly which
  stages ran without instrumenting any one stage by hand.
- Assemble a fixed pipeline with `SetNext`, the way
  `CustomsClearancePipeline.Standard` wires document check, duty calculation,
  inspection, and release into one chain a caller holds by its first link.
- Build a Mediator, `CustomsMediator`, that routes a message between two
  colleagues neither of which holds a reference to the other, and register
  those colleagues after construction to avoid a circular dependency.
- Distinguish a stage that halts a chain from a mediator that routes between
  peers, and recognize when either pattern adds structure a few sequential
  checks or a direct method call would say in fewer lines.

## Why this matters

A consignment entering customs does not clear in one decision. It passes
through an ordered sequence of checks, each one capable of stopping the
consignment outright, and none of them aware of what the ones before or after
it actually do. A declaration with missing paperwork never reaches an
inspector. A declaration that reaches inspection but fails it never reaches
release. A declaration that clears every check needs a duty figure computed
somewhere along the way, carried forward to whatever accepts it at the end.
Writing this as one method, a block of `if` statements checking documents,
then inspection, then computing duty, then returning a confirmation, ties the
order of those checks to the order statements happen to appear in a function
body. Reordering the checks, adding a fifth one, or running only three of the
four for a lighter-weight declaration all mean editing that one function and
re-reading logic that has nothing to do with the change at hand.

A customs house also runs two desks that have to coordinate without
depending on each other directly. A desk that logs an incoming declaration
and a desk that assesses duty both need to know what the other one did, but
neither should hold a hard reference to the other's class. Wiring every desk
directly to every other desk it needs to talk to turns a two-desk workflow
into a web of direct references that only grows as more desks join, and a
desk added later has to know about every desk already in place just to send
one message.

Chain of Responsibility solves the first problem by giving each check its own
link, responsible for exactly one decision, wired to a successor it does not
otherwise know about. A link either lets a declaration continue past it or
halts the chain there, and the chain itself holds no central switch statement
deciding which link runs when; the links decide that one at a time, each by
handling its own responsibility and then stepping aside. Mediator solves the
second problem by giving the desks a single thing to send through, an
`IClearanceMediator`, so a desk's only job is to send a message and react to
one it receives, leaving the question of who gets what to a mediator sitting
between them. Both patterns move coordination logic that would otherwise live
inside the participants themselves out to a thin shape purpose-built to
express it: a chain of links reads cleanly as an ordered sequence of
independent checks, and a mediator reads cleanly as the one place that knows
who talks to whom, before either reaches the mechanics that make it work.

{% include excalidraw.html name="22-mediator-and-chain-of-responsibility" caption="Figure 22.1: CustomsClearancePipeline.Standard wires DocumentCheckStage, DutyCalculationStage, InspectionStage, and ReleaseStage into one chain through SetNext, with ClearanceStage.Handle recording each visited stage into the shared ClearanceContext and returning early on a halt; CustomsMediator routes a message from DeclarationDesk to DutyDesk and back, with neither desk holding a direct reference to the other." %}

## Chain of Responsibility: an ordered sequence of independent checks

The example behind this chapter lives in
`examples/22-mediator-and-chain-of-responsibility`. A declaration enters the
chain as an immutable fact, the information a customs officer has on hand
before any check runs:

```csharp
public sealed record CustomsDeclaration(
    string ConsignmentId,
    decimal DeclaredValue,
    bool DocumentsComplete,
    bool RequiresInspection,
    bool InspectionPassed);
```

`CustomsDeclaration` never changes once constructed; it carries the facts a
chain inspects, not the facts a chain accumulates while running. The
accumulating half lives in a separate type, `ClearanceContext`, built around
one declaration and mutated as the chain walks through it:

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

`StagesRun` exposes an `IReadOnlyList<string>` backed by a private mutable
list, so a caller holding a `ClearanceContext` can read exactly which stages
ran, in order, but cannot append a stage name itself; only the chain
machinery, through the internal `RecordStage`, is allowed to write to that
list. `Duty` is a plain settable property rather than something recorded
through a method, because unlike the stage log, only one stage ever needs to
set it, and nothing about that single write benefits from the same
append-only discipline `StagesRun` enforces.

Every link in the chain shares one base type, `ClearanceStage`, and that base
type is where the chain's two running rules live: record the stage that just
ran, and keep going only if the declaration is still cleared.

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

`Handle` is the one method every caller invokes, and it is not virtual, so no
concrete stage can skip the bookkeeping it performs. `GetType().Name` reads
the concrete stage's own class name at the moment it runs, which is why
`context.RecordStage` needs no argument passed in from outside; a
`DocumentCheckStage` records `"DocumentCheckStage"` into the context without
either type naming itself as a string anywhere. `Process` is the one step
`Handle` leaves to each concrete stage, the actual decision that stage exists
to make, returned as a `ClearanceResult`:

```csharp
public sealed record ClearanceResult(bool Cleared, string? HaltedStage, string? Reason, decimal Duty);
```

A stage signals a halt by returning a `ClearanceResult` with `Cleared` set to
`false`, never by throwing an exception. `Handle` checks that single boolean
after `Process` returns: if the declaration is not cleared, `Handle` returns
immediately with that same result, and `_next` is never touched, so a halted
declaration never reaches a later stage at all. If the declaration is still
cleared, `Handle` delegates to `_next?.Handle(context)`, passing the same
`ClearanceContext` forward so the next stage sees every fact the ones before
it already established, and falls back to the current, cleared `result` if
`_next` is null, which is exactly the case at the chain's final link.

`DocumentCheckStage` is the first link, and it halts the earliest it can,
before any duty is computed or any inspection even considered:

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

`DutyCalculationStage` is the second link, and it is the one stage in this
chain that never halts; its entire job is to compute a figure and let the
chain continue regardless of what that figure turns out to be:

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

`InspectionStage` follows the same shape as `DocumentCheckStage`, halting
only when a declaration was flagged for a physical inspection and that
inspection did not pass; a declaration never flagged for inspection at all
skips straight through regardless of `InspectionPassed`, because the check
is `RequiresInspection && !InspectionPassed`, not `!InspectionPassed` alone.
`ReleaseStage`, the final link, always clears, since reaching it at all means
every earlier stage already let the declaration through:

```csharp
public sealed class ReleaseStage : ClearanceStage
{
    protected override ClearanceResult Process(ClearanceContext context) =>
        new(Cleared: true, HaltedStage: null, Reason: null, Duty: context.Duty);
}
```

`CustomsClearancePipeline.Standard` is the one place that wires the four
links into the order this book's example tests against, so no call site
anywhere else constructs a `DocumentCheckStage` and chains it by hand:

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

`SetNext` returns the stage it was just given, not the stage it was called
on, which is what lets `documentCheck.SetNext(dutyCalculation).SetNext(inspection)`
read left to right as the order the chain actually runs in: the first call
wires document check to duty calculation and returns duty calculation, and
the second call wires that returned duty calculation to inspection. A caller
never sees `dutyCalculation`, `inspection`, or `release` directly; `Standard`
returns only `documentCheck`, the chain's first link, because that is the
only reference any caller needs to run the whole sequence through `Handle`.

`ClearanceChainTests.Handle_runs_all_stages_in_order_and_clears_a_complete_declaration`
builds a declaration with complete documents and no inspection required,
runs it through `CustomsClearancePipeline.Standard()`, and asserts
`context.StagesRun` equals exactly `[nameof(DocumentCheckStage),
nameof(DutyCalculationStage), nameof(InspectionStage), nameof(ReleaseStage)]`,
with `result.Duty` equal to `80m` on a declared value of `1000m`, confirming
every link ran, in the order `Standard` wired them, and the duty calculation
landed in the final result. `Handle_halts_at_the_document_stage_when_documents_are_missing`
declares a consignment with `DocumentsComplete: false`, asserts
`result.Cleared` is `false` and `result.HaltedStage` equals
`nameof(DocumentCheckStage)`, and asserts `context.StagesRun` contains only
`DocumentCheckStage`, with `Assert.DoesNotContain` ruling out
`DutyCalculationStage`, `InspectionStage`, and `ReleaseStage` by name,
proving the chain actually stopped rather than merely reporting a halt while
still visiting every later link. `Handle_halts_before_release_when_a_required_inspection_fails`
runs a declaration with complete documents but a failed inspection, asserts
`result.HaltedStage` equals `nameof(InspectionStage)`, and confirms
`context.StagesRun` holds `DocumentCheckStage`, `DutyCalculationStage`, and
`InspectionStage`, with `ReleaseStage` absent, which is the duty calculation
running to completion, recording `Duty`, even though the declaration never
clears customs. `Handle_calculates_duty_for_a_passing_declaration` runs a
declared value of `2500m` through the full chain and asserts both
`result.Duty` and `context.Duty` equal `200m`, confirming the figure the
chain computed mid-pipeline is the same figure that reaches the caller in
the final `ClearanceResult`.

The same pipeline shape, an ordered sequence of independent links each free
to pass a request on or stop it, appears in the ASP.NET Core middleware
pipeline that Kestrel runs every incoming HTTP request through, where each
registered middleware component decides whether to call the next one or
short-circuit the response itself, a structural parallel [Microsoft Learn
documents directly](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/).

## Mediator: two desks that coordinate without referencing each other

A clearance chain decides whether one declaration keeps moving forward. A
customs house also runs desks that need to tell each other things, and the
mediator example in this chapter keeps that coordination completely separate
from the chain. `IClearanceMediator` is the one thing a desk is allowed to
depend on:

```csharp
public interface IClearanceMediator
{
    void Send(string message, ClearanceColleague sender);
}
```

`ClearanceColleague` is the shared base every desk inherits from, and it
holds the mediator reference every desk needs without exposing it directly
to a caller outside the desk itself:

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

`Send` never names another colleague. It calls `Mediator.Send(message,
this)`, handing the mediator both the message and a reference to itself, so
the mediator can tell who sent it without the colleague having to know who
should receive it. `Receive` is just as plain, appending to a private inbox
a caller can read through `Inbox` but never write to directly. `DeclarationDesk`
and `DutyDesk` add nothing to this shape beyond their own constructors:

```csharp
public sealed class DeclarationDesk : ClearanceColleague
{
    public DeclarationDesk(IClearanceMediator mediator)
        : base(mediator)
    {
    }
}
```

`DutyDesk` is identical, taking the same `IClearanceMediator` and passing it
to the same base constructor. Neither class has a field, a property, or a
constructor parameter referring to the other. A reader looking at
`DeclarationDesk`'s source has no way to learn that `DutyDesk` exists at
all, which is the entire point: the two desks are peers that happen to work
together, and nothing in either one's own code expresses that relationship.

`CustomsMediator` is where the relationship actually lives:

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

`Register` exists because of a construction order problem a direct
constructor argument cannot solve on its own: `DeclarationDesk` needs an
`IClearanceMediator` passed into its constructor, and `CustomsMediator`
needs references to both desks before it can route anything between them,
but neither desk can exist yet when the mediator itself is being
constructed. Building `CustomsMediator` first, then `DeclarationDesk` and
`DutyDesk` against that already-built mediator, and finally calling
`Register(declarationDesk, dutyDesk)` to hand the mediator the two
references it was missing, breaks that cycle by wiring the relationship
after every object involved already exists, rather than trying to express a
circular dependency through constructor parameters that cannot resolve it.

`Send` checks `ReferenceEquals(sender, _declarationDesk)` rather than
inspecting the message content or trusting a type check, because identity is
the one thing the mediator needs to know to answer "who is this from and
who should hear it." A message from `_declarationDesk` routes to
`_dutyDesk.Receive(message)`, and a message from `_dutyDesk` routes the
other direction, to `_declarationDesk.Receive(message)`. Neither branch ever
calls `Receive` back on the sender; the routing is strictly to the other
party; a desk that calls `Send` never finds its own message echoed back into
its own `Inbox`, because `Send` only ever reaches the branch for the
colleague that did not send it.

`MediatorTests.Send_routes_a_message_to_the_other_colleague` registers a
`DeclarationDesk` and a `DutyDesk` against one `CustomsMediator`, calls
`declarationDesk.Send("Consignment CN-2001 declared.")`, and asserts
`dutyDesk.Inbox` contains that exact message, confirming the mediator
delivered it to the desk that did not send it.
`Send_does_not_echo_the_message_back_to_the_sender` runs the identical send
and asserts `declarationDesk.Inbox` is empty, which is the half of the
contract a routing bug could violate silently: a mediator that mistakenly
delivered to both desks, or to the sender instead of the recipient, would
still pass a test that only checked the recipient's inbox.
`Send_routes_a_reply_back_through_the_mediator_without_a_direct_reference`
sends one message from `declarationDesk` and a second from `dutyDesk`, and
asserts `declarationDesk.Inbox` contains the duty desk's reply while
`dutyDesk.Inbox` does not, proving the mediator routes correctly in both
directions on the same pair of colleagues, and that a reply reaching
`declarationDesk` never required `DutyDesk` to hold a reference back to
`DeclarationDesk` to send it.

## Build, run, observe

Every stage and every colleague in this example is a synchronous,
in-memory computation, so the suite needs nothing beyond the local .NET 10
SDK. The example is standalone-testable from its own directory:

```sh
cd examples/22-mediator-and-chain-of-responsibility
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 7
  failed: 0
  succeeded: 7
  skipped: 0
  duration: 1s 339ms
```

The seven cases split across what this chapter covers. `ClearanceChainTests`
proves the Chain of Responsibility half: a complete declaration runs through
all four stages in order and clears with the correct duty, a declaration
missing documents halts at the first stage without reaching any later one,
a declaration that fails inspection halts there after duty was already
computed, and a passing declaration's computed duty matches between the
context and the final result. `MediatorTests` proves the Mediator half: a
message sent by one desk reaches the other desk's inbox, that same message
never lands back in the sender's own inbox, and a reply sent the other
direction routes correctly without either desk ever referencing the other
directly.

## When not to use it

A Chain of Responsibility earns the extra `ClearanceStage` subclass, the
`SetNext` wiring, and the shared `ClearanceContext` only when the sequence
of checks is open to changing shape in practice: a check removed for a
lighter-weight declaration, a new check inserted between two existing ones,
or a check reused in a second pipeline built from a different subset of the
same links. A fixed pair of conditions that will never grow a third, never
drop either one, and never run anywhere but inline in one method does not
need four classes and a chain to express what two sequential `if` statements
already say in fewer lines. The tell is a chain with exactly one caller that
always builds it the same way, every single time, with no second
configuration anywhere in the codebase; at that point the chain is one fixed
computation spread across several files instead of one method.

A Mediator pays for itself when the number of participants that need to
coordinate is large enough, or changes often enough, that direct references
between them would multiply faster than a reader can track. Two desks that
will only ever be exactly two desks, with no third one coming and no
likelihood that `DeclarationDesk` ever needs to call a method on `DutyDesk`
beyond sending it one kind of message, get little from `CustomsMediator`
that a direct method call on an injected `DutyDesk` reference would not say
in fewer lines. The failure mode on the other side is a mediator that grows
past routing into deciding business logic itself: a `CustomsMediator.Send`
that starts branching on message content to compute duty, validate a
declaration, or make the decisions each stage in the chain is supposed to
own, has stopped being a router and turned into a single class that
centralizes every rule in the system behind one `Send` method, exactly the
concentration of responsibility this pattern is supposed to prevent by
keeping each colleague's own logic inside that colleague.

> **AI sidebar.** Asked to make two conditions "more flexible" or "ready for
> growth," an AI pairing partner will sometimes produce a full
> `ClearanceStage` subclass, complete with its own `Process` override and a
> `SetNext` wiring call, for a pair of checks that will only ever be exactly
> that pair, treating Chain of Responsibility as the default shape for any
> sequence of conditions even when no second configuration, no insertion
> point, and no reuse across pipelines appear anywhere in the request. A
> related suggestion reaches for a Mediator the moment a second class needs
> to talk to a first one, routing a single direct method call through an
> `IClearanceMediator` interface and a `Send` dispatch for a relationship
> that will only ever involve those two participants. A generated mediator
> can also drift the other way, growing a `Send` method that branches on
> message content to make decisions the colleagues themselves should own,
> pulling every rule in the system into one class instead of leaving each
> colleague responsible for its own logic. Check a generated answer against
> what this chapter actually built: a chain is for a sequence of checks that
> can plausibly change shape or get reused, not any sequence of checks at
> all, and a mediator is for enough participants, or a volatile enough set
> of relationships, that direct references between them would be harder to
> read than one place that routes; a fixed pair of conditions or a fixed
> pair of collaborators usually says the same thing with a plain `if` or a
> plain method call.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of both Mediator and Chain of Responsibility this chapter builds on.
- Microsoft Learn, [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/): the authority behind this chapter's one mention of the Kestrel request pipeline as a real-world illustration of the same ordered, each-link-decides-whether-to-continue shape `ClearanceStage` implements.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
