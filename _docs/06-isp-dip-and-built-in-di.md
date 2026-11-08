---
title: "ISP, DIP, and DI with the built-in container"
order: 6
layout: tutorial
sectionid: docs
part: "principles"
description: "Interface segregation and dependency inversion where the dependency injection container ships in the base class library, ready to register and resolve with no separate framework decision to make."
---

<!-- EXAMPLE (shipping domain): ICarrierGateway behind a DI registration. -->

## Learning objectives

By the end of this chapter you can:

- State the Interface Segregation Principle and recognize the fat interface it
  targets: a consumer forced to depend on members it never calls.
- Split a fat carrier interface into role interfaces and recompose them for
  implementers that need the whole surface.
- State the Dependency Inversion Principle and build a composition root with
  the built-in `Microsoft.Extensions.DependencyInjection` container.
- Register one singleton instance against three service types with the
  factory-registration overload.
- Swap the concrete carrier by editing one registration, and say when the
  built-in container is enough.

## Why this matters

A shipping backend reaches a carrier at two unrelated moments. A prospect wants
a price: destination, weight, a quote, no booking. A customer wants a shipment
booked and later tracked. Code that only quotes has no reason to hold booking or
tracking members. The carrier also varies: production calls a real carrier API,
while a sandbox, a demo or a pre-launch regional carrier needs a stand-in that
returns predictable values with no network call.

One `ICarrierGateway` with quote, book and status methods fails both ways. A
rate-shopping class depends on members it never calls, and its test double has
to implement all three to satisfy the type. A quote-only freight-broker feed has
no correct `BookAsync`: it throws from a method its interface promises, or
returns a booking that isn't one. Segregation removes that Liskov violation,
because the narrow interface has nothing left to fake.

{% include excalidraw.html name="06-isp-dip-and-built-in-di" caption="Figure 6.1: ISP splits one fat carrier interface into focused role interfaces; DIP routes each consumer through those abstractions instead of a concrete gateway, leaving the built-in container as the only place that resolves one to the other." %}

## The cost of one interface doing three jobs

A first pass reaches for one interface with every carrier operation on it:

```csharp
public interface ICarrierGateway
{
    Task<RateQuote> GetRateAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);
    Task<BookingReceipt> BookAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);
    Task<string> GetTrackingStatusAsync(string trackingNumber, CancellationToken cancellationToken);
}
```

A consumer that only calls `GetRateAsync` still takes an `ICarrierGateway`, and
its substitute must implement `BookAsync` and `GetTrackingStatusAsync` before it
compiles. A quote-only broker integration throws `NotSupportedException` from
two methods, or returns a made-up receipt and status that nothing backs.
**Interface Segregation:** a consumer, and an implementer, depends on exactly the
capabilities it uses.

## Segregating into ICarrierRates and ICarrierTracking

The shipped `ICarrierGateway` is composed from two role interfaces:

```csharp
/// <summary>
/// Quoting a shipment: the only member a rate-shopping consumer needs. Split
/// out of a single fat carrier interface so that consumer depends on nothing
/// it does not call (Interface Segregation Principle).
/// </summary>
public interface ICarrierRates
{
    Task<RateQuote> GetRateAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);
}

/// <summary>
/// Booking a shipment and following it afterward: the two members a booking
/// consumer needs. A consumer that only quotes rates never has to implement
/// or stub these.
/// </summary>
public interface ICarrierTracking
{
    Task<BookingReceipt> BookAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);

    Task<string> GetTrackingStatusAsync(string trackingNumber, CancellationToken cancellationToken);
}

public interface ICarrierGateway : ICarrierRates, ICarrierTracking
{
}
```

`ICarrierGateway` isn't a third role. It's the composition, kept because a full
carrier has both capabilities and the composition root registers one
abstraction. `RestCarrierGateway` and `FakeCarrierGateway` implement
`ICarrierGateway`, so they satisfy both roles. A consumer that only quotes
depends on `ICarrierRates` and never sees `BookAsync`, and the broker
integration implements `ICarrierRates` alone.

## Dependency inversion: who depends on whom

`ShipmentRateAdvisor` depends on `ICarrierRates`, never on `ICarrierGateway` or
a concrete gateway:

```csharp
/// <summary>
/// Quotes a shipment for a customer. Depends only on
/// <see cref="ICarrierRates"/>, the narrow interface it actually calls, not
/// the full <see cref="ICarrierGateway"/>: it has no booking or tracking
/// concern, so Interface Segregation keeps it from depending on members it
/// would otherwise have to fake out in a test.
/// </summary>
public sealed class ShipmentRateAdvisor(ICarrierRates rates)
{
    public async Task<string> DescribeQuoteAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken)
    {
        var quote = await rates.GetRateAsync(destination, weightKilograms, cancellationToken).ConfigureAwait(false);
        return $"{destination}: {quote.Price}, {quote.TransitTime.Days}-day transit";
    }
}
```

`ShipmentBookingService` needs booking and tracking, so it depends on the full
`ICarrierGateway`, and still on no concrete type:

```csharp
/// <summary>
/// Books a shipment and reports back its tracking status. Depends on the
/// full <see cref="ICarrierGateway"/> abstraction, never a concrete gateway:
/// swapping <see cref="RestCarrierGateway"/> for <see cref="FakeCarrierGateway"/>
/// (or any future implementation) changes only the composition root's
/// registration, never this class (Dependency Inversion Principle).
/// </summary>
public sealed class ShipmentBookingService(ICarrierGateway gateway)
{
    public Task<BookingReceipt> BookShipmentAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken) =>
        gateway.BookAsync(destination, weightKilograms, cancellationToken);

    public Task<string> CheckStatusAsync(string trackingNumber, CancellationToken cancellationToken) =>
        gateway.GetTrackingStatusAsync(trackingNumber, cancellationToken);
}
```

Neither class names or constructs `RestCarrierGateway` or `FakeCarrierGateway`.
Robert C. Martin's **Dependency Inversion**: high-level modules don't depend on
low-level modules; both depend on an abstraction, and the abstraction doesn't
depend on details. The dependency that would run from rate-quoting policy down
into a carrier integration stops at the interface both share. The advisor is
tested with an NSubstitute double for `ICarrierRates` alone, with no gateway and
no network call, real or simulated.

## Wiring it with the built-in container

Something still has to choose the concrete type. In .NET, the dependency
injection container, `Microsoft.Extensions.DependencyInjection`, is "a built-in
part of the framework, along with configuration, logging, and the options
pattern" ([.NET dependency injection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview)).
There's no separate framework to choose; it's one package reference, pinned in
`CarrierGateways.csproj`.

`ServiceCollectionExtensions` is the one composition root that names a concrete
gateway:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarrierGateway(this IServiceCollection services) =>
        services.AddCarrierGateway<RestCarrierGateway>();

    public static IServiceCollection AddCarrierGateway<TGateway>(this IServiceCollection services)
        where TGateway : class, ICarrierGateway
    {
        services.AddSingleton<TGateway>();
        services.AddSingleton<ICarrierGateway>(provider => provider.GetRequiredService<TGateway>());
        services.AddSingleton<ICarrierRates>(provider => provider.GetRequiredService<TGateway>());
        services.AddSingleton<ICarrierTracking>(provider => provider.GetRequiredService<TGateway>());

        services.AddSingleton<ShipmentRateAdvisor>();
        services.AddSingleton<ShipmentBookingService>();

        return services;
    }
}
```

The [service registration documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-registration)
describes the overloads used here:

| Call | Shape | Effect |
|---|---|---|
| `services.AddSingleton<TGateway>()` | Implementation type only | The container constructs `TGateway` and resolves its constructor dependencies. |
| `services.AddSingleton<ICarrierRates>(provider => provider.GetRequiredService<TGateway>())` | Factory | Returns the already-registered `TGateway` singleton under another service type. |

The three factory registrations make `ICarrierGateway`, `ICarrierRates` and
`ICarrierTracking` resolve to one object: resolving each from a built provider
returns the same instance. `ShipmentRateAdvisor` takes the narrow face and
`ShipmentBookingService` the full one, without either knowing about the other.

`IServiceCollection` only describes registrations. `BuildServiceProvider` turns
it into an [IServiceProvider](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview),
".NET's built-in service container." `provider.GetRequiredService<ShipmentRateAdvisor>()`
does the work DIP moved out of the advisor: it inspects the constructor, finds
the `ICarrierRates` parameter, resolves it and passes it in. The resolved
advisor produces a working quote.

### Lifetimes

Every registration is `AddSingleton`. The
[service lifetimes documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes)
defines the three:

| Lifetime | Created | Use here |
|---|---|---|
| Singleton | "the first time it's requested"; every later request gets the same instance | The gateway: it holds no per-caller state beyond an in-memory booking ledger, and sharing it avoids rebuilding an HTTP-backed client. |
| Transient | "each time it's requested from the service container" | Not used. |
| Scoped | "once per client request (connection)", and "should always be used from within a scope" | Not used: a console library has no request boundary for a scope to attach to. |

The documentation calls out the failure: resolving a scoped service from a
singleton makes it behave as a singleton, so state leaks across requests meant
to be isolated.

{% include excalidraw.html name="06-singleton-fanout-registration" caption="Figure 6.2: One singleton registration fans out through three factory registrations, each resolving back to the same gateway instance under a different interface." %}
<!-- DIAGRAM SPEC: left column, one box "services.AddSingleton<TGateway>()"
     feeding a single "TGateway singleton instance" box. Three arrows fan out
     right from that instance box to three destination boxes stacked
     vertically: "ICarrierGateway", "ICarrierRates", "ICarrierTracking", each
     arrow labeled "provider => provider.GetRequiredService<TGateway>()".
     Below, two consumer boxes: "ShipmentRateAdvisor" with an arrow in from
     only "ICarrierRates", and "ShipmentBookingService" with an arrow in from
     only "ICarrierGateway". Caption emphasizes one instance, three resolvable
     faces, two consumers each taking only the face they use. -->

## Swapping the registration: no consumer edits

The composition root is the only place a carrier swap touches. Build two
containers, one per gateway, and resolve the same consumers from each:

```csharp
var restServices = new ServiceCollection();
restServices.AddCarrierGateway<RestCarrierGateway>();
await using var restProvider = restServices.BuildServiceProvider();

var fakeServices = new ServiceCollection();
fakeServices.AddCarrierGateway<FakeCarrierGateway>();
await using var fakeProvider = fakeServices.BuildServiceProvider();
```

`ShipmentRateAdvisor` and `ShipmentBookingService` aren't edited or recompiled.
Only their output differs:

| Registered gateway | Price | Tracking number |
|---|---|---|
| `RestCarrierGateway` | By destination and weight | `RST-...` |
| `FakeCarrierGateway` | `FakeCarrierGateway.FlatRate` | `FAKE-000001` |

A sandbox, a demo or an early integration against a carrier whose contract isn't
signed becomes a change to the generic argument of `AddCarrierGateway<TGateway>`,
not a search-and-replace across every class that talks to a carrier.

## When not to apply these principles rigidly

Both principles pay only when a second implementation or a differently shaped
consumer exists.

- **One implementation, no sandbox double, no test that needs a fake:** a single
  `ICarrierGateway`, or a concrete class called directly, is the right amount of
  abstraction.
- **Every consumer uses every member:** splitting a two-method interface into two
  single-method ones gives a reader role types to chase across files for no
  consumer's benefit. Segregate where needs differ, as the advisor's and the
  booking service's do, and stop when every remaining member is used by every
  remaining consumer.
- **One permanent production dependency with one caller:** an interface, a
  constructor parameter and a registration line buy nothing.

Use the interface and registration when a swap is real, a test needs a double,
or a capability varies by consumer. The built-in container itself is cheap: no
extra package and no framework decision, just a few `AddSingleton` calls.

<blockquote class="ai-sidebar" markdown="1">
**Specifying the wiring.** "Add dependency injection" leaves open the container, the lifetime and how much of the gateway each consumer sees.

> Wire the gateway with `Microsoft.Extensions.DependencyInjection` only, in `ServiceCollectionExtensions.AddCarrierGateway<TGateway>`; add no third-party container. Register `TGateway` once with `AddSingleton` and forward `ICarrierGateway`, `ICarrierRates` and `ICarrierTracking` to that one instance. This library has no request boundary, so nothing is scoped. `ShipmentRateAdvisor` takes only `ICarrierRates`; `ShipmentBookingService` takes `ICarrierGateway`. Both receive dependencies through primary constructors, never from `IServiceProvider`. Tests swap in `FakeCarrierGateway` with `AddCarrierGateway<FakeCarrierGateway>()`.

Check with `dotnet test --project CarrierGateways.Tests --filter CarrierGatewayRegistrationTests`; `Narrow_interfaces_resolve_to_the_same_registered_gateway_instance` must pass.
</blockquote>

## Build, run, observe

```sh
cd examples/06-isp-dip-and-built-in-di
dotnet test
```

All 8 tests pass; `scripts/verify.sh` runs the same suite. The three carrier
interfaces resolve to one instance, the registered gateway type alone decides
the resolved behavior, and the advisor works against a substitute for
`ICarrierRates` alone.

## For deeper coverage

- Robert C. Martin, [*Agile Software Development: Principles, Patterns, and Practices*](https://www.pearson.com/en-gb/subject-catalog/p/agile-software-development-principles-patterns-and-practices-pearson-new-international-edition/P200000004932/9781292038360) (Prentice Hall): the original statement of the Interface Segregation Principle and the Dependency Inversion Principle.
- Microsoft Learn: [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview), [Service lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes), and [Service registration](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-registration), the primary source for every claim about the built-in container's behavior.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
