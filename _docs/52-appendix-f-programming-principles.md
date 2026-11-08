---
title: "Programming principles reviewed in code"
order: 52
layout: tutorial
sectionid: docs
part: appendices
appendix: "F"
description: "A code-first review of the wider programming-principle vocabulary beyond SOLID."
---

## Learning objectives

By the end of this appendix you can:

- Name eleven principles from outside SOLID (DRY, orthogonality, YAGNI, KISS,
  composition over inheritance, the Law of Demeter, fail-fast, the
  Robustness Principle, the Boy-Scout Rule, separation of concerns and
  Brooks's Law) and say what each asks of a design.
- Point to a pattern that embodies each principle in working code.
- Trace `ArgumentNullException.ThrowIfNull` back to fail-fast, and a source
  generator or analyzer back to DRY.
- Explain why composition over inheritance is the reason Strategy, Decorator
  and Bridge pass a collaborator in instead of extending a base class.
- Find the source of each principle in the bibliography.

## Why this matters

SOLID and the Gang of Four catalogue are named throughout. An older
vocabulary sits under the same decisions and mostly goes unnamed: only
Single Responsibility is named where it's introduced. Each principle below is
tied to a pattern that already applies it, so a design choice made for a
concrete reason also gets its name in the wider literature. It's a
recognition aid, not a new rule on top.

| Principle | Asks for | Where it shows in C# |
| --- | --- | --- |
| DRY | One authoritative representation of each piece of knowledge | Template Method; source generators; Roslyn analyzers |
| Orthogonality | Changing one part forces no change elsewhere | DI behind an interface; keyed services |
| YAGNI | No mechanism before its requirement exists | A `FrozenDictionary` dispatch table instead of a class per case |
| KISS | The form a reader can hold in their head | A `Func<>` delegate before an interface or keyed service |
| Composition over inheritance | Pass behavior in; don't inherit it | Strategy, Decorator, Bridge |
| Law of Demeter | Talk only to immediate collaborators | Facade; Mediator |
| Fail-fast | Reject a broken precondition at the boundary | `ArgumentNullException.ThrowIfNull`; `ValidateOnStart()` |
| Robustness Principle | Liberal in what you accept, conservative in what you send | Adapter; the Options validator |
| Boy-Scout Rule | Leave touched code cleaner, one small step at a time | Extracting a seam before refactoring legacy code |
| Separation of concerns | Things that change for different reasons live apart | Single Responsibility; layering |
| Brooks's Law | Adding people to a late project makes it later | The refactor-or-rewrite decision |

## DRY: don't repeat yourself

Thomas and Hunt state DRY as "every piece of knowledge must have a single,
unambiguous, authoritative representation within a system." Template Method
applies it directly. The order of a customs declaration's three steps
(collect required fields, calculate duty, validate) is written once, in a
non-virtual template method, and each country's rules fill in the steps
without restating the order:

```csharp
public CustomsDeclaration Build(Shipment shipment)
{
    ArgumentNullException.ThrowIfNull(shipment);

    var requiredFields = CollectRequiredFields(shipment);
    var duty = CalculateDuty(shipment);
    var validationMessages = Validate(shipment);

    return new CustomsDeclaration(shipment.DestinationCountry, requiredFields, duty, validationMessages);
}
```

Source generators and analyzers move DRY into the build. A hand-maintained
carrier registry is a second copy of what the `ICarrierGateway`
implementations already say; an `IIncrementalGenerator` that reads the
implementations and emits the registry keeps one representation and derives
the rest at compile time. A Roslyn analyzer does the same for a convention
such as money in minor units: the rule lives once, in the analyzer, and the
compiler checks it on every build instead of a reviewer on every pull
request.

## Orthogonality

An orthogonal design lets one part change without forcing a change in an
unrelated one. Dependency injection behind an interface gives you that: swap
a `StandardTariff` for an `ExpressTariff` behind `IPricingStrategy` and the
`StrategyRateQuoter` that consumes it doesn't change. Keyed services extend
it to named variants:

```csharp
services.AddKeyedSingleton<IPricingStrategy, StandardTariff>("standard");
services.AddKeyedSingleton<IPricingStrategy, ExpressTariff>("express");
services.AddKeyedSingleton<IPricingStrategy, EconomyTariff>("economy");
```

Changing the express tariff's algorithm doesn't touch how the standard or
economy tariffs are registered or resolved.

## YAGNI: you aren't gonna need it

Don't build a mechanism for a requirement that hasn't arrived. Five document
kinds, each a pure rendering step with no state or dependencies, need one
lookup built once, not an `IDocumentRenderer` interface, five classes and a
factory:

```csharp
internal static readonly FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>> Handlers =
    new Dictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>>
    {
        [DocumentKind.BillOfLading] = DocumentRenderers.RenderBillOfLading,
        [DocumentKind.CommercialInvoice] = DocumentRenderers.RenderCommercialInvoice,
        [DocumentKind.PackingList] = DocumentRenderers.RenderPackingList,
        [DocumentKind.CertificateOfOrigin] = DocumentRenderers.RenderCertificateOfOrigin,
        [DocumentKind.CustomsDeclaration] = DocumentRenderers.RenderCustomsDeclaration,
    }.ToFrozenDictionary();
```

The table stops paying off when a kind's own state or dependencies outgrow a
single rendering method; then a dedicated handler type is worth it.

The inverse is an anti-pattern: Strategy and Abstract Factory wrapped around
an `IRateComponent` and `IRateComponentFactory` that only ever have one real
shape. Build the abstraction the current requirement needs.

## KISS and the cost of a pattern

McConnell's simplicity means preferring the solution a reader can hold in
their head over the merely more general one. Strategy comes in three forms
of rising cost: a `Func<RateRequest, Money>` delegate, a named
`IPricingStrategy` interface, and a keyed-service registration. The delegate
has no interface, class or registration to read:

```csharp
public sealed class DelegateRateQuoter(Func<RateRequest, Money> priceStrategy)
{
    public RateQuote Quote(RateRequest request)
    {
        var price = priceStrategy(request);
        return new RateQuote(request.Destination, price, request.ServiceLevel);
    }
}
```

The interface or keyed form is worth its cost only when several call sites
select a strategy by name or at run time. Ask whether a method would do
before writing the general form.

## Composition over inheritance

The Gang of Four prefer object composition to class inheritance.

| Pattern | What's composed in |
| --- | --- |
| Strategy | A pricing algorithm, held by `StrategyRateQuoter` as a reference; any `IPricingStrategy` works, including one compiled later |
| Decorator | Behavior wrapped around a component instead of added by a deeper subclass |
| Bridge | A label kind and a label format as two independent collaborators, instead of one subclass per kind-format pair |

Inheriting a variant fixes it at compile time and ties it to one base class.
Passing it in keeps it swappable and keeps the varying parts independent.

## Law of Demeter and tell-don't-ask

A caller that talks only to its immediate collaborators, never reaching
through one to another, follows the Law of Demeter. A Facade turns a booking
that would chain through address validation, carrier quoting and customs
filing into one `Book` call; the facade knows the order so the caller
doesn't:

```csharp
public sealed class ShipmentBookingFacade(IAddressValidator validator, ICarrierGateway gateway, ICustomsFiler filer)
{
    public ShipmentConfirmation Book(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        validator.Validate(request);
        CarrierQuote quote = gateway.GetQuote(request);
        string bookingReference = filer.File(request, quote);

        return new ShipmentConfirmation(quote.CarrierCode, bookingReference, quote.Amount);
    }
}
```

Mediator does the same for peers. Two desks that would hold references to
each other to coordinate a handoff each talk only to a shared mediator. That's
tell-don't-ask: tell the mediator what happened instead of querying the other
desk's state.

## Fail-fast

Detect a broken precondition where it first becomes observable, before it
spreads into a state that's harder to diagnose.
`ArgumentNullException.ThrowIfNull` at the top of a method throws before any
step runs, instead of a `NullReferenceException` from deep inside an
unrelated call. It appears across Template Method, State, Iterator and
generic math code, and in composition roots:

```csharp
ArgumentNullException.ThrowIfNull(services);
ArgumentNullException.ThrowIfNull(configuration);

services
    .AddOptions<CarrierGatewayOptions>()
    .Bind(configuration.GetSection(CarrierGatewayOptions.SectionName))
    .ValidateOnStart();

services.AddSingleton<IValidateOptions<CarrierGatewayOptions>, CarrierGatewayOptionsValidator>();
```

Clean Code and Code Complete both separate a guard clause, which throws
because the caller broke a precondition, from a `TryParse`-style doer or a
`Result` value that reports an expected failure the caller asked about.

The dispose pattern applies the same idea to lifetime. A type that holds no
unmanaged resource directly needs no finalizer and no `Dispose(bool)`
override: the failure a finalizer guards against doesn't exist for it, and
adding one would hide that.

## The Robustness Principle

Postel's principle is to be conservative in what you send and liberal in
what you accept. Adapter applies it at the system boundary: three carriers
expose three request and response shapes, and three `ICarrierGateway`
adapters translate each into the one `ShippingRequest`/`ShippingResponse`
pair the rest of the system uses. No caller learns three formats.

The Options pattern applies it to configuration.
`CarrierGatewayOptionsValidator`, run through `ValidateOnStart()` in the
listing above, collects every problem in the section in one pass, and the
bound `CarrierGatewayOptions` refuses to start a gateway against
configuration nobody checked. Tester-Doer parsing has the same shape: accept
the raw input and report a precise failure, so a malformed value never
reaches a collaborator not written to expect it.

## The Boy-Scout Rule and doing one thing

Clean Code's Boy-Scout Rule: leave the code cleaner than you found it, in the
small way the current change already touches. With legacy code that means
one mechanical first move. A sealed `LegacyRateEngine` with its
collaborators wired inside gets a seam that an `IRateEngine` interface can
stand behind, and nothing else: no pricing fix, no reorganizing, no renames.
The refactor-or-rewrite decision depends on that step. A stable contract made
in one reviewable change lets a team build a rewrite beside a refactor and
compare both against the same cases.

## Separation of concerns

Martin builds much of *Agile Software Development* on separation of concerns
before stating SOLID: keep what changes for different reasons in different
places. Single Responsibility is its class-level form. A class that changes
for a billing reason and for a shipment-tracking reason mixes two concerns,
and a change for one risks the other. Layering gateways, services and
callers so each depends only downward applies the principle one level up: a
change to how a carrier rate is calculated stays in the pricing concern and
never touches the code that renders a bill of lading.

## Brooks's Law

Adding people to a late software project makes it later. It's a scheduling
principle, and it belongs in a refactor-or-rewrite decision for that reason.
New engineers on a late rewrite need its assumptions explained before they
contribute, and that time comes from the calendar the rewrite was already
short on. A seam's stable contract doesn't change that, but it means the
team chooses between refactor and rewrite on evidence instead of a schedule
assumption. Distrust any estimate that depends on adding people to meet a
deadline.

## For deeper coverage

- Thomas and Hunt, [*The Pragmatic Programmer: Your Journey to Mastery*](https://pragprog.com/titles/tpp20/the-pragmatic-programmer-20th-anniversary-edition/) (Addison-Wesley / Pragmatic Bookshelf, 2019): the source for DRY, orthogonality, YAGNI, the Law of Demeter, and the Robustness Principle reviewed above.
- Martin, [*Clean Code: A Handbook of Agile Software Craftsmanship*](https://www.pearson.com/en-us/subject-catalog/p/clean-code-a-handbook-of-agile-software-craftsmanship/P200000009044/9780132350884) (Prentice Hall, 2008): the source for the Boy-Scout Rule and the fail-fast guard-clause discipline reviewed above.
- McConnell, [*Code Complete: A Practical Handbook of Software Construction*](https://www.microsoftpressstore.com/store/code-complete-9780735619678) (Microsoft Press, 2004): the source for the simplicity and construction-practice framing behind the KISS and fail-fast sections above.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480/9780321700698) (Addison-Wesley, 1994): the source for composition over inheritance and the Strategy, Decorator, and Bridge examples above.
- Martin, [*Agile Software Development: Principles, Patterns, and Practices*](https://www.pearson.com/en-gb/subject-catalog/p/agile-software-development-principles-patterns-and-practices-pearson-new-international-edition/P200000004932/9781292038360) (Prentice Hall, 2002): the source for separation of concerns and the SOLID principles it builds toward.
- Brooks, [*The Mythical Man-Month: Essays on Software Engineering*](https://www.pearson.com/en-us/subject-catalog/p/Brooks-Mythical-Man-Month-Anniversary-Edition-The-Essays-On-Software-Engineering-2nd-Edition/P200000000149/9780132119160) (Addison-Wesley, 1995): the source for Brooks's Law.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
