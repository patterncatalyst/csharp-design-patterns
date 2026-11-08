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

- Describe Factory Method as a fixed sequence with one overridable creation step, and write both the inheritance form (`CarrierDispatcher.CreateGateway`) and the delegate form (`DelegateCarrierDispatcher`).
- Build an Abstract Factory, `ICarrierGatewayFactory<TCarrier>`, that produces a matched gateway, request builder and response parser for one carrier.
- Key a runtime lookup on `typeof(TCarrier)` in `CarrierRegistry`, backed by a `where TCarrier : ICarrier` constraint and a `static abstract` `Code` member, and say why the type argument is available at run time.
- Add a carrier, Airco, to a running registry with one `Register<Airco>` call and no edit to `CarrierRegistry`.
- Spot a factory that's overengineering for one implementation, and a string-keyed dictionary standing in for a typed registry.

## Why this matters

A dispatch service hands a `ShipmentRequest` to the carrier a customer picked, and Fastline, Seabright and Airco each have their own wire protocol. What varies is three collaborators that must agree: a request builder that shapes the outgoing payload, a gateway that sends it and a response parser that reads the reply. Fastline's builder feeding Seabright's gateway sends a malformed payload, and carrier-agnostic interfaces give the type system no way to notice.

The obvious version is one method with a switch on a carrier string, each branch building, sending and parsing inline. A fourth carrier gets added by copying a branch and changing the name in three places, and missing one, because nothing keeps the three pieces in a branch a matched set.

- **Factory Method** fixes the dispatch sequence and isolates the one step that varies.
- **Abstract Factory** groups the three collaborators into one family a caller asks for by carrier.
- **Reified generics** replace the carrier string with a type the compiler and the runtime both understand.

{% include excalidraw.html name="15-factory-method-and-abstract-factory" caption="Figure 15.1: CarrierDispatcher fixes a build-send-parse sequence with one overridable step; each carrier's factory produces a matched gateway, builder, and parser; and CarrierRegistry resolves a carrier's family by its type rather than a string." %}

## Pattern: Factory Method

### The problem

Every dispatch runs build, send, parse in that order. Only the gateway it sends through depends on the carrier, and adding a carrier shouldn't reopen the sequence.

### The solution

A **Factory Method** fixes an operation's steps in one place and leaves the choice of which object to create to a single overridable step.

### How C# models it

`Dispatch` runs the sequence for every carrier that will ever subclass `CarrierDispatcher`:

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

`Dispatch` isn't `virtual`, so no subclass can reorder it, skip the parse or call `CreateGateway` twice. `CreateGateway` is the factory method: `protected abstract`, so every concrete dispatcher must supply it, and that's all `FastlineDispatcher` supplies:

```csharp
public sealed class FastlineDispatcher()
    : CarrierDispatcher(new FastlineRequestBuilder(), new FastlineResponseParser())
{
    protected override ICarrierGateway CreateGateway() => new FastlineGateway();
}
```

`SeabrightDispatcher` and `AircoDispatcher` have the same shape. The builder and parser are constructor arguments, not more factory methods: for a given subclass they never vary apart from the gateway. Three overridable steps that must always be overridden together is the coordination problem Abstract Factory solves. A `FastlineDispatcher` sending a 100-kilogram shipment from Rotterdam to Santos returns `ShipmentConfirmation("FASTLINE", "FASTLINE-0020")`.

### The delegate form

The one variable step can be a value instead of an override:

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

No hierarchy and no override: the factory method is a `Func<ICarrierGateway>` passed to the constructor. Given `() => new FastlineGateway()`, a `FastlineRequestBuilder` and a `FastlineResponseParser`, it returns the same confirmation as `FastlineDispatcher`.

| Form | Fits when |
| --- | --- |
| Inheritance (`CarrierDispatcher`) | Subclasses share protected state or behavior a constructor parameter can't carry. |
| Delegate (`DelegateCarrierDispatcher`) | Carriers share nothing beyond the sequence, as here. |

## Pattern: Abstract Factory

### The problem

Factory Method chooses the gateway but doesn't guarantee that the gateway, the builder and the parser belong to the same carrier. Each is a constructor argument or an override, interchangeable with any other implementation of its interface, so nothing stops a dispatcher built from Fastline's builder and Seabright's parser.

### The solution

An **Abstract Factory** provides one interface for creating a family of related objects, so a caller that asks one factory for all of them gets pieces that belong together.

### How C# models it

```csharp
public interface ICarrierGatewayFactory<TCarrier>
    where TCarrier : ICarrier
{
    ICarrierGateway CreateGateway();

    IShipmentRequestBuilder CreateRequestBuilder();

    IShipmentResponseParser CreateParser();
}
```

`FastlineGatewayFactory` closes it over `Fastline`, and every method returns a Fastline type:

```csharp
public sealed class FastlineGatewayFactory : ICarrierGatewayFactory<Fastline>
{
    public ICarrierGateway CreateGateway() => new FastlineGateway();

    public IShipmentRequestBuilder CreateRequestBuilder() => new FastlineRequestBuilder();

    public IShipmentResponseParser CreateParser() => new FastlineResponseParser();
}
```

`SeabrightGatewayFactory` and `AircoGatewayFactory` close over `Seabright` and `Airco`. An `ICarrierGatewayFactory<Fastline>` can't produce a Seabright gateway, because its return types are fixed in its method bodies; once a caller works through one factory, the mismatch can't be expressed. Resolved from a registry, each carrier's factory returns a gateway, builder and parser whose type names all start with that carrier's name.

### Reified generics: typeof(TCarrier) as a runtime key

Something still picks the factory for a shipment, and carrier identity here is a type, not a string. `ICarrier` gives each carrier a `static abstract` code, because the wire code is a fact about the carrier, not about an instance:

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

No `Fastline` value ever exists at run time; the type is used only as a type argument. `Fastline.Code` reads a property on the type, which is how `FastlineRequestBuilder` stamps the code onto an outgoing `CarrierRequest` without constructing a `Fastline`.

`CarrierRegistry` uses the type as the key:

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

`typeof(TCarrier)` works as a key because C# keeps a generic method's type argument available when the method runs, not only while the compiler checks the call. `Register<Fastline>(...)` and `Register<Seabright>(...)` are two instantiations of the same method, each evaluating `typeof` against its own argument, so two distinct `Type` objects land under two entries. `FactoryFor<TCarrier>` casts the stored `object` back once, internally; callers never see it. A missing registration throws `UnknownCarrierException`, one exception type for "never registered", instead of a null or an invalid cast.

| Key | Accepts at compile time | Carries |
| --- | --- | --- |
| `Dictionary<string, object>` | Any string, a real carrier or a typo | Nothing beyond the string |
| `typeof(TCarrier)` with `where TCarrier : ICarrier` | Only types that implement `ICarrier`; `Register<string>` doesn't compile | The carrier's wire code through `TCarrier.Code` |

Adding a carrier is one new factory class and one call:

```csharp
registry.Register<Airco>(new AircoGatewayFactory());

var factory = registry.FactoryFor<Airco>();
```

A registry built with only Fastline and Seabright resolves `FactoryFor<Airco>()` after that call, with no change to `CarrierRegistry`; its dictionary gained one key. Before the call, the same lookup throws `UnknownCarrierException`.

`ShipmentDispatchService` joins the sequence, the family and the lookup:

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

`service.Dispatch<Fastline>(request)` never names `FastlineGateway`, `FastlineRequestBuilder` or `FastlineResponseParser`. The type argument is the only carrier-specific thing in the call, and the compiler checks it against `ICarrier`.

{% include excalidraw.html name="15-type-keyed-carrier-registry" caption="Figure 15.2: CarrierRegistry stores one factory per carrier type and looks it up the same way. Adding a new carrier costs one registration call with no edit to the registry; an unregistered carrier throws rather than returning null." %}

## When not to use it

- **One implementation.** A factory costs a protected override, an interface or a generic constraint that a constructor call doesn't. A dispatcher that only ever sends through one gateway, with no second carrier planned, is one concrete class calling `new FastlineGateway()`. A system that talks to one carrier gets nothing from `ICarrierGatewayFactory<TCarrier>`, because there's no second family to tell apart.
- **A registry without runtime selection.** `CarrierRegistry` pays off because the carrier arrives on the request and isn't known until the call. A warehouse integration that only ever talks to Fastline at one call site gets nothing from it that a directly constructed `FastlineGatewayFactory` doesn't give, and gains a dictionary, a registration step and an `UnknownCarrierException` failure mode.

> **🤖 Explore with your agent**
>
> "Identify each carrier by a type implementing `ICarrier` with a `static abstract string Code`. Register one `ICarrierGatewayFactory<TCarrier>` per carrier in a `CarrierRegistry` keyed by `typeof(TCarrier)`, with `where TCarrier : ICarrier` on `Register` and `FactoryFor`, and throw `UnknownCarrierException` for a missing carrier. `ShipmentDispatchService.Dispatch<TCarrier>` gets its factory from `FactoryFor<TCarrier>()` and does no cast. Don't use `Activator.CreateInstance` or a `Dictionary<string, object>`."
>
> Check with `dotnet test --project CarrierProvisioning.Tests --filter CarrierRegistryTests`, and confirm `Activator` appears nowhere in the reply.

## Build, run, observe

Every gateway computes a deterministic response instead of calling a network. `FastlineGateway.Send` returns `"{Code}|{payload.Length:D4}"`, and the parser turns that into `ShipmentConfirmation(code, "{code}-{len}")`. For `ShipmentRequest("Rotterdam", "Santos", 100m)`, `FastlineRequestBuilder` produces `"Rotterdam>Santos:100"`, 20 characters, so the gateway answers `"FASTLINE|0020"` and the parser returns `ShipmentConfirmation("FASTLINE", "FASTLINE-0020")`. Seabright and Airco give `SEABRIGHT-0020` and `AIRCO-0020` for the same request.

```sh
cd examples/15-factory-method-and-abstract-factory
dotnet test
```

All 14 tests pass, and `scripts/verify.sh` runs them with every other example. They cover family resolution by type, families never crossing, Airco joining with one call, the unknown-carrier exception, both Factory Method forms, end-to-end dispatch for all three carriers, and each builder stamping its carrier's `Code`.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of both Factory Method and Abstract Factory.
- Microsoft Learn, [Generics](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/generics): how a generic type parameter is compiled and resolved, the basis for `typeof(TCarrier)` behaving as a reliable runtime key in `CarrierRegistry`.
- Microsoft Learn, [Constraints on type parameters](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters): what `where TCarrier : ICarrier` rules in and out at compile time.
- Microsoft Learn, ["Static abstract members in interfaces"](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#static-abstract-members-in-interfaces) and [generic math](https://learn.microsoft.com/en-us/dotnet/standard/generics/math): the feature behind `ICarrier.Code` and its broader motivating scenario.
- Microsoft Learn, [`Activator.CreateInstance`](https://learn.microsoft.com/en-us/dotnet/api/system.activator.createinstance): the reflection API a registered, typed factory lookup replaces.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview), including its coverage of registering open generic service types: a container-managed alternative to a hand-built `CarrierRegistry` once a project already depends on the built-in DI container for everything else.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
