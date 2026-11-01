---
title: "Factory Method and Abstract Factory"
order: 15
layout: tutorial
sectionid: docs
part: "creating-objects-under-control"
description: "How reified generics change these patterns, with typeof(T), constructor constraints, and type-keyed registries."
---

<!-- EXAMPLE (shipping domain): A gateway per carrier, resolved by type through IGateway<TCarrier>. -->

## Learning objectives

By the end of this chapter you can:

- Explain the Factory Method pattern as a fixed sequence with exactly one
  overridable creation step, and write both the inheritance form
  (`CarrierDispatcher.CreateGateway`) and the delegate alternative
  (`DelegateCarrierDispatcher`) that reach the same result through that one
  step.
- Build an Abstract Factory, `ICarrierGatewayFactory<TCarrier>`, that
  produces a matched family of three collaborators for one carrier, a
  gateway, a request builder, and a response parser, so nothing can
  construct one carrier's gateway next to a different carrier's parser.
- Key a runtime lookup on `typeof(TCarrier)` in `CarrierRegistry`, backed by
  a `where TCarrier : ICarrier` constraint and a `static abstract` `Code`
  member, and explain why a generic type argument in C# is available at
  runtime for that lookup to rely on.
- Add a new carrier, Airco, to an already-running registry with one
  `Register<Airco>` call, and show why `CarrierRegistry` itself needs no
  edit to recognize it.
- Recognize when a factory of either kind is overengineering for a
  single-implementation collaborator, and spot a hand-rolled string-keyed
  dictionary standing in for a typed, `typeof(T)`-keyed registry.

## Why this matters

A shipment dispatch service has to hand a `ShipmentRequest` to whichever
carrier a customer picked, and each carrier's wire protocol is actually
different from the others: Fastline, Seabright, and Airco each expect their
own payload shape going out and return their own raw response format coming
back. The thing that varies by carrier is not one function call but three
collaborating pieces that have to agree with each other: a request builder
that knows how to shape an outgoing payload for that carrier, a gateway that
knows how to send it, and a response parser that knows how to read what came
back. Pick the right family of three for a given carrier and dispatch works.
Mix pieces from two different families, Fastline's builder feeding
Seabright's gateway, and nothing in the type system stops a malformed
payload from going out the door, because a builder and a gateway that each
implement an unrelated, carrier-agnostic interface have no way to check
whether they actually belong to the same carrier.

The obvious first implementation reaches for one method with a switch on a
carrier string, each branch constructing that carrier's builder, calling its
gateway, and parsing its response inline. That method works for three
carriers and keeps working for a fourth, right up until someone adds that
fourth carrier by copying one of the existing branches, changing the
carrier name in three places, and missing one of them, because nothing
forces the three pieces inside a branch to stay a matched set as the branch
count grows. Creating the right family of objects for a carrier chosen at
runtime, without a growing switch that has to be edited every time a carrier
is added, and without a string typo silently crossing two carriers' pieces,
is what this chapter solves before it reaches any mechanics. Factory Method
fixes the sequence a dispatch always runs and isolates the one step that
varies. Abstract Factory groups the three varying collaborators into a
single family a caller asks for by carrier rather than assembling by hand.
Reified generics replace the carrier string with a type the compiler and the
runtime both understand, so resolving a family and adding a new one are both
checked at compile time instead of trusted to match a string correctly.

{% include excalidraw.html name="15-factory-method-and-abstract-factory" caption="Figure 15.1: CarrierDispatcher fixes a build-send-parse sequence with one overridable step; each carrier's factory produces a matched gateway, builder, and parser; and CarrierRegistry resolves a carrier's family by its type rather than a string." %}

## Factory Method: one overridable step inside a fixed sequence

The Gang of Four's Factory Method names a narrow problem: fix the order of
an operation's steps in one place, and leave exactly one of those steps,
which object to create, to something else. `CarrierDispatcher` is that
shape applied to dispatching a shipment. Its public `Dispatch` method runs
build, send, parse in that order, every time, for every carrier that will
ever subclass it:

```csharp
public abstract class CarrierDispatcher
{
    private readonly IShipmentRequestBuilder _builder;
    private readonly IShipmentResponseParser _parser;

    protected CarrierDispatcher(IShipmentRequestBuilder builder, IShipmentResponseParser parser)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(parser);

        _builder = builder;
        _parser = parser;
    }

    protected abstract ICarrierGateway CreateGateway();

    public ShipmentConfirmation Dispatch(ShipmentRequest request)
    {
        var carrierRequest = _builder.Build(request);
        var rawResponse = CreateGateway().Send(carrierRequest);
        return _parser.Parse(rawResponse);
    }
}
```

`Dispatch` carries no `virtual` keyword, so no subclass can reorder it,
skip the parse step, or call `CreateGateway` twice. `CreateGateway` is the
factory method itself: `protected abstract`, with no body in the base
class, so every concrete dispatcher must supply one. `FastlineDispatcher`
supplies exactly that, nothing else:

```csharp
public sealed class FastlineDispatcher()
    : CarrierDispatcher(new FastlineRequestBuilder(), new FastlineResponseParser())
{
    protected override ICarrierGateway CreateGateway() => new FastlineGateway();
}
```

`SeabrightDispatcher` and `AircoDispatcher` follow the identical shape,
each constructing its own builder and parser in the base constructor call
and overriding only `CreateGateway` to return its own gateway. Notice what
is not a factory method here: the builder and the parser arrive as
ordinary constructor arguments, not as a second and third overridable
creation step. They are fixed once, at construction, because for a given
subclass they never change independently of the gateway; only the
transport the dispatcher sends through needs to vary per call in the way
Factory Method is built to express. Promoting the builder and the parser
to factory methods as well would give `CarrierDispatcher` three overridable
steps answering a question only one of them actually has, which carrier is
dispatching, and the other two steps would need to override in lockstep
with the first every single time, which is exactly the coordination problem
Abstract Factory exists to solve instead, covered next.

`CarrierDispatcherTests.FastlineDispatcher_dispatches_through_the_overridden_gateway`
builds a bare `FastlineDispatcher`, dispatches a `ShipmentRequest` from
Rotterdam to Santos weighing 100 kilograms, and asserts a
`ShipmentConfirmation("FASTLINE", "FASTLINE-0020")` comes back, proving
`Dispatch` ran all three steps through `FastlineGateway` without the test
touching `CreateGateway` directly at all.

Inheritance is not the only way to supply that one overridable step.
`DelegateCarrierDispatcher` runs the same build-send-parse sequence, but
takes the gateway as a plain `Func<ICarrierGateway>` constructor parameter
instead of asking a subclass to override anything:

```csharp
public sealed class DelegateCarrierDispatcher(
    Func<ICarrierGateway> gatewayFactory,
    IShipmentRequestBuilder builder,
    IShipmentResponseParser parser)
{
    private readonly Func<ICarrierGateway> _gatewayFactory = gatewayFactory
        ?? throw new ArgumentNullException(nameof(gatewayFactory));
    private readonly IShipmentRequestBuilder _builder = builder
        ?? throw new ArgumentNullException(nameof(builder));
    private readonly IShipmentResponseParser _parser = parser
        ?? throw new ArgumentNullException(nameof(parser));

    public ShipmentConfirmation Dispatch(ShipmentRequest request)
    {
        var carrierRequest = _builder.Build(request);
        var rawResponse = _gatewayFactory().Send(carrierRequest);
        return _parser.Parse(rawResponse);
    }
}
```

There is no `CarrierDispatcher` in this type's hierarchy at all, no
`FastlineDispatcher` class, and no override anywhere. The factory method's
entire job, deciding which `ICarrierGateway` this dispatch sends through,
is a value passed into a constructor instead of a method a subclass fills
in. `CarrierDispatcherTests.DelegateCarrierDispatcher_reaches_parity_with_the_fastline_subclass`
builds a `FastlineDispatcher` and a `DelegateCarrierDispatcher` supplied
with `() => new FastlineGateway()`, a `FastlineRequestBuilder`, and a
`FastlineResponseParser`, dispatches the identical request through both,
and asserts the two `ShipmentConfirmation` results are equal. Reaching
parity with a subclass, for a given carrier, is passing that carrier's own
gateway, builder, and parser into the delegate form; no class hierarchy is
required to get the same confirmation back. The two forms cost about the
same to write for one carrier; the inheritance form pays off once a
dispatcher needs shared protected state or behavior across its own
subclasses that a plain constructor parameter cannot carry, and the
delegate form stays cheaper for exactly the shape this example has, three
independent carriers with nothing shared beyond the sequence itself.

## Abstract Factory: a family of products that belong together

Factory Method answers which gateway a dispatch sends through. It says
nothing about whether that gateway, the builder that fed it, and the
parser reading its response actually belong to the same carrier, because
each of the three is just a constructor argument or an override return
value, interchangeable with any other type implementing the same
interface. Nothing in `CarrierDispatcher`'s signature stops a caller from
constructing a dispatcher with Fastline's builder and Seabright's parser.
Abstract Factory closes exactly that gap: one factory produces the whole
family for a carrier in one place, so a caller asking for "Fastline's
family" gets three pieces guaranteed to match.

```csharp
public interface ICarrierGatewayFactory<TCarrier>
    where TCarrier : ICarrier
{
    ICarrierGateway CreateGateway();

    IShipmentRequestBuilder CreateRequestBuilder();

    IShipmentResponseParser CreateParser();
}
```

`FastlineGatewayFactory` implements that interface closed over `Fastline`,
and every method it exposes returns a Fastline-specific type:

```csharp
public sealed class FastlineGatewayFactory : ICarrierGatewayFactory<Fastline>
{
    public ICarrierGateway CreateGateway() => new FastlineGateway();

    public IShipmentRequestBuilder CreateRequestBuilder() => new FastlineRequestBuilder();

    public IShipmentResponseParser CreateParser() => new FastlineResponseParser();
}
```

`SeabrightGatewayFactory` and `AircoGatewayFactory` are the same shape
closed over `Seabright` and `Airco`. A caller holding an
`ICarrierGatewayFactory<Fastline>` has no way to ask it for a Seabright
gateway, because the factory's return types are fixed to Fastline's
concrete classes inside its own method bodies; the mismatch this pattern
exists to prevent is unrepresentable once a caller is working through one
factory instance rather than three independently chosen collaborators.

`CarrierRegistryTests.Resolves_the_fastline_family_by_type` resolves a
`FastlineGatewayFactory` out of a registry and asserts
`factory.CreateGateway()` is a `FastlineGateway`, `CreateRequestBuilder()`
is a `FastlineRequestBuilder`, and `CreateParser()` is a
`FastlineResponseParser`, all three from the same factory call.
`Each_carrier_key_resolves_its_own_family` runs that same check as a
`[Theory]` over all three carriers, asserting every product a factory
returns has a type name starting with that carrier's own prefix, Fastline,
Seabright, or Airco, which is the family guarantee stated as a test rather
than left as a comment: resolve a carrier's factory once, and every piece
it hands back already belongs to that carrier.

## Reified generics: typeof(TCarrier) as a runtime key

Everything so far still needs something to pick which concrete
`ICarrierGatewayFactory<TCarrier>` a `ShipmentDispatchService` runs
against for a given shipment. That selection happens by carrier identity,
and carrier identity in this example is a type, not a string. `ICarrier`
gives each carrier a `static abstract` member rather than an instance
property, because the carrier's wire code is a fact about the carrier
itself, not about any particular object built from it:

```csharp
public interface ICarrier
{
    static abstract string Code { get; }
}

public sealed class Fastline : ICarrier
{
    public static string Code => "FASTLINE";
}
```

`Seabright` and `Airco` implement `ICarrier` the same way, each returning
its own code. None of the three carrier types is ever constructed; a
`Fastline` or `Seabright` value never exists at runtime, only the type
itself, used purely as a type argument. `static abstract` is what makes
that possible: `Fastline.Code` reads a property on the type, with no
instance in sight, which is also exactly what `FastlineRequestBuilder`
does when it stamps a carrier code onto an outgoing `CarrierRequest`
without ever constructing a `Fastline`.

`CarrierRegistry` turns that type into a lookup key directly:

```csharp
public sealed class CarrierRegistry
{
    private readonly Dictionary<Type, object> _factories = new();

    public void Register<TCarrier>(ICarrierGatewayFactory<TCarrier> factory)
        where TCarrier : ICarrier
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factories[typeof(TCarrier)] = factory;
    }

    public ICarrierGatewayFactory<TCarrier> FactoryFor<TCarrier>()
        where TCarrier : ICarrier
    {
        if (_factories.TryGetValue(typeof(TCarrier), out var factory))
        {
            return (ICarrierGatewayFactory<TCarrier>)factory;
        }

        throw new UnknownCarrierException(
            $"No gateway factory is registered for carrier type '{typeof(TCarrier).Name}'.");
    }
}
```

`typeof(TCarrier)` works as a dictionary key here because C# keeps a
generic method's type argument available at the point the method actually
runs, not only while the compiler is checking the call. Calling
`Register<Fastline>(new FastlineGatewayFactory())` and
`Register<Seabright>(new SeabrightGatewayFactory())` closes two different
instantiations of the same generic method, and each one evaluates
`typeof(TCarrier)` against its own type argument: `typeof(Fastline)` and
`typeof(Seabright)` are two distinct `Type` objects the runtime can tell
apart, so the two factories land under two different dictionary entries
with no collision. `FactoryFor<TCarrier>` performs the same lookup in
reverse and casts the stored `object` back to
`ICarrierGatewayFactory<TCarrier>` before returning it, so a caller never
writes that cast by hand and never sees the dictionary's loosely typed
`object` value at all. A missing registration raises
`UnknownCarrierException` rather than returning null or throwing a cast
exception from a mismatched entry, giving a caller one exception type to
catch for "this carrier was never registered."

The `where TCarrier : ICarrier` constraint on both `Register` and
`FactoryFor` does its own share of the work before any of that lookup
runs. It rules out calling `Register<string>(...)` or
`FactoryFor<CarrierRequest>()` at compile time, because neither type
implements `ICarrier`; the registry can only ever be asked about a type
that is actually a carrier, which is a stronger guarantee than a
`Dictionary<string, object>` keyed by carrier name could offer, since any
string at all compiles as a key there, matching a real carrier or not.
Combined with the `static abstract Code` member, the constraint means a
`TCarrier` type argument always carries both a compile-time guarantee,
it really is a carrier, and a runtime fact, its own wire code, reachable
through `TCarrier.Code` inside generic code that needs it.

Adding a new carrier to a running registry costs exactly one call and one
new factory class, with no change to `CarrierRegistry` itself:

```csharp
registry.Register<Airco>(new AircoGatewayFactory());

var factory = registry.FactoryFor<Airco>();
```

`CarrierRegistryTests.A_new_carrier_resolves_after_one_register_call_with_no_registry_edit`
builds a registry holding only Fastline and Seabright, calls
`Register<Airco>(new AircoGatewayFactory())` against that already-built
registry, and then resolves `FactoryFor<Airco>()` successfully,
demonstrating that `CarrierRegistry`'s source never needed to learn about
Airco in advance: the dictionary it already owned simply gained one more
key. `An_unregistered_carrier_throws_unknown_carrier_exception` confirms
the other side of that: asking the same Airco-free registry for
`FactoryFor<Airco>()` throws `UnknownCarrierException` rather than
returning a stale factory or a null reference.

`ShipmentDispatchService` is the end-to-end consumer that ties Factory
Method's sequence to Abstract Factory's family and reified generics'
lookup in one call:

```csharp
public sealed class ShipmentDispatchService(CarrierRegistry registry)
{
    private readonly CarrierRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public ShipmentConfirmation Dispatch<TCarrier>(ShipmentRequest request)
        where TCarrier : ICarrier
    {
        var factory = _registry.FactoryFor<TCarrier>();
        var carrierRequest = factory.CreateRequestBuilder().Build(request);
        var rawResponse = factory.CreateGateway().Send(carrierRequest);
        return factory.CreateParser().Parse(rawResponse);
    }
}
```

A caller invoking `service.Dispatch<Fastline>(request)` never names
`FastlineGateway`, `FastlineRequestBuilder`, or `FastlineResponseParser`
anywhere; the only carrier-specific thing in the call is the type argument
itself, and the compiler checks that argument against `ICarrier` before the
call is even allowed to compile.

{% include excalidraw.html name="15-type-keyed-carrier-registry" caption="Figure 15.2: CarrierRegistry stores one factory per carrier type and looks it up the same way. Adding a new carrier costs one registration call with no edit to the registry; an unregistered carrier throws rather than returning null." %}

## Build, run, observe

Every gateway in this example computes a deterministic response instead of
calling out over a network, so the suite needs nothing beyond the local
.NET 10 SDK. `FastlineGateway.Send` returns `"{Code}|{payload.Length:D4}"`,
and the matching parser turns that back into
`ShipmentConfirmation(code, "{code}-{len}")`. For the shared test request,
`ShipmentRequest("Rotterdam", "Santos", 100m)`, `FastlineRequestBuilder`
produces the payload `"Rotterdam>Santos:100"`, 20 characters long, so
Fastline's gateway answers `"FASTLINE|0020"` and its parser turns that into
`ShipmentConfirmation("FASTLINE", "FASTLINE-0020")`, the exact value both
`CarrierDispatcherTests` cases and `ShipmentDispatchServiceTests.Dispatches_a_fastline_shipment_end_to_end`
assert. Seabright and Airco follow the identical arithmetic under their
own codes, landing on `SEABRIGHT-0020` and `AIRCO-0020` for the same
request.

The example is standalone-testable from its own directory:

```sh
cd examples/15-factory-method-and-abstract-factory
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 14
  failed: 0
  succeeded: 14
  skipped: 0
  duration: 3s 957ms
```

The 14 cases split across what this chapter covers. `CarrierRegistryTests`
proves the reified-generics headline: a carrier's whole family resolves by
`typeof(TCarrier)`, the Fastline, Seabright, and Airco families never
cross, a new carrier joins with one `Register` call and zero edits to
`CarrierRegistry`, and an unregistered carrier throws
`UnknownCarrierException` rather than failing silently.
`CarrierDispatcherTests` proves the Factory Method half: a
`FastlineDispatcher` subclass dispatches correctly through its overridden
`CreateGateway`, and `DelegateCarrierDispatcher` reaches the identical
confirmation through a constructor-supplied `Func<ICarrierGateway>`
instead of a subclass at all. `ShipmentDispatchServiceTests` proves the
end-to-end path for all three carriers, resolving each one's full family
through the registry and running build, send, parse without the caller
naming a single concrete type. `RequestBuilderTests` proves the
static-abstract reification directly: each carrier's own `Code` ends up on
the `CarrierRequest` its matching builder produces, read straight off the
carrier type rather than passed in as a separate argument a caller could
get wrong.

## When not to use it

A factory, Method or Abstract, costs a protected override, an interface,
or a generic constraint that a plain constructor call does not. That cost
buys something only when a real second implementation exists or is
clearly coming. A dispatcher built to send through exactly one gateway,
with no second carrier planned and nothing elsewhere in the codebase
constructing an alternative, does not need `CarrierDispatcher`'s abstract
`CreateGateway` step or a `DelegateCarrierDispatcher` constructor
parameter for a function that will only ever be one lambda; a single
concrete class calling `new FastlineGateway()` directly says the same
thing with less to read. The same test applies to Abstract Factory: if a
system only ever talks to one carrier, `ICarrierGatewayFactory<TCarrier>`
adds a generic interface and an implementing class around three
collaborators a caller could just construct inline, because there is no
second family for the interface to distinguish from the first.

A type-keyed registry is its own, separate reach, and it is the wrong one
more often than the factory pattern underneath it. `CarrierRegistry` pays
off specifically because the carrier is runtime data, a value that
arrives on an incoming shipment request and is not known until the call
happens. A fixed, small number of carriers with each one wired to exactly
one call site at compile time, a warehouse integration that only ever
talks to Fastline and never needs to resolve any other carrier by a type
argument, gets nothing from `CarrierRegistry` that a directly constructed
`FastlineGatewayFactory` does not already provide, and it adds a
dictionary, a registration step, and an `UnknownCarrierException` failure
mode with no runtime selection anywhere to justify carrying it.

<blockquote class="ai-sidebar" markdown="1">
Asked to make carrier resolution "pluggable" or
"extensible," an AI pairing partner will sometimes reach for
`Activator.CreateInstance(carrierType)` against a carrier naming
convention and a public parameterless constructor, reflecting a type
into existence by name instead of looking up an already-registered
factory instance. That approach loses the compile-time check
`where TCarrier : ICarrier` already provides, fails at runtime instead of
compile time when a typo or a renamed type breaks the naming
convention, and pays a reflection cost on every resolution that
`CarrierRegistry`'s dictionary lookup does not. A second common
suggestion is a hand-rolled `Dictionary<string, object>` keyed by a
carrier name string, with a cast back to the right interface type at
every call site, reinventing `CarrierRegistry` with a string doing the
job `typeof(TCarrier)` already does more safely. Check a generated
answer against what `CarrierRegistry` already proves: a `typeof(T)`-keyed
`Dictionary<Type, object>` behind a constrained generic method gives
every caller a compile-time-checked key and a cast the registry performs
once, internally, instead of a cast every caller repeats by hand against
a string that the compiler cannot verify matches anything real.
</blockquote>

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of both Factory Method and Abstract Factory this chapter builds on.
- Microsoft Learn, [Generics](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/generics): the authority for how a generic type parameter is compiled and resolved, the basis for `typeof(TCarrier)` behaving as a reliable runtime key in `CarrierRegistry`.
- Microsoft Learn, [Constraints on type parameters](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters): the authority for what `where TCarrier : ICarrier` rules in and out at compile time.
- Microsoft Learn, ["Static abstract members in interfaces"](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#static-abstract-members-in-interfaces) and [generic math](https://learn.microsoft.com/en-us/dotnet/standard/generics/math): the feature behind `ICarrier.Code` and its broader motivating scenario.
- Microsoft Learn, [`Activator.CreateInstance`](https://learn.microsoft.com/en-us/dotnet/api/system.activator.createinstance): the API this chapter's AI sidebar contrasts with a registered, typed factory lookup.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview), including its coverage of registering open generic service types: a container-managed alternative to a hand-built `CarrierRegistry` once a project already depends on the built-in DI container for everything else.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
