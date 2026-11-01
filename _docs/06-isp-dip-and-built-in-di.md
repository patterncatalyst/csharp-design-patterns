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

- State the Interface Segregation Principle precisely and recognize the fat
  interface it targets: a consumer forced to depend on members it never
  calls.
- Split a fat carrier interface into focused role interfaces and recompose
  them into one abstraction for implementers that need the whole
  surface.
- State the Dependency Inversion Principle precisely and build a composition
  root with the built-in `Microsoft.Extensions.DependencyInjection`
  container that wires a concrete gateway behind that abstraction.
- Register the same singleton instance against three different service
  types with the factory-registration overload, and explain how that differs
  from a plain `AddSingleton<TService, TImplementation>()` call.
- Swap the concrete carrier a running application uses by editing one
  registration call, with no consumer class touched, and say when the
  built-in container is enough and when it is not.

## Why this matters

A shipping backend touches a carrier at two different moments that have
nothing to do with each other. A prospective customer wants a price before
committing to anything: destination, weight, and a quote back, no booking
involved. A customer who already committed wants a shipment booked and later
wants to know where it is. Both moments eventually talk to the same carrier
integration, but the code that only quotes rates has no business holding a
reference to booking or tracking behavior it never calls, and the carrier
relationship itself isn't fixed. A production integration talks to a real
carrier's API; a sandbox environment, a demo, or a pre-launch regional
carrier needs a stand-in that returns predictable values without making a
network call. Both facts point at the same two forces the first SOLID
principles in this book already named: something varies, and the code around
it shouldn't have to change when it does. Here, what varies is which
capabilities a consumer needs and which concrete carrier backs them, and that
split motivates Interface Segregation (ISP) and Dependency Inversion (DIP).

The naive shape for this system reaches for one interface, `ICarrierGateway`,
with a method for quoting, a method for booking, and a method for checking
status, and asks every carrier integration and every consumer to work
through all three at once. A rate-shopping class built against that
interface still declares a dependency on booking and tracking members its
own logic never calls, and a unit test for that class needs a test double
that implements all three just to satisfy the type, not because the test
exercises more than one of them. A carrier that only prices shipments and
never books them, a rate-comparison feed from a freight broker, for example,
has no correct implementation for `BookAsync`: it either throws from a method
its own interface promises callers can invoke, or it returns a value that
looks like a successful booking and isn't. A fat interface like that
manufactures a Liskov violation that segregation makes structurally
impossible, because there is nothing left on the narrow interface to fake.

{% include excalidraw.html name="06-isp-dip-and-built-in-di" caption="Figure 6.1: ISP splits one fat carrier interface into focused role interfaces; DIP routes each consumer through those abstractions instead of a concrete gateway, leaving the built-in container as the only place that resolves one to the other." %}

## The cost of one interface doing three jobs

The example behind this chapter lives in `examples/06-isp-dip-and-built-in-di`.
Picture the version of `ICarrierGateway` this chapter doesn't ship, the one
a first pass at the problem naturally reaches for:

```csharp
public interface ICarrierGateway
{
    Task<RateQuote> GetRateAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);
    Task<BookingReceipt> BookAsync(string destination, decimal weightKilograms, CancellationToken cancellationToken);
    Task<string> GetTrackingStatusAsync(string trackingNumber, CancellationToken cancellationToken);
}
```

A rate-shopping consumer built against this interface, something that only
ever calls `GetRateAsync`, still has a constructor parameter typed
`ICarrierGateway`. Its test has to substitute something for `BookAsync` and
`GetTrackingStatusAsync` even though the class under test never calls either
one, because the compiler requires the substitute to implement the full
interface before it type-checks. Widen the carrier roster to include a
quote-only freight broker integration and the fat interface gets worse: that
integration either throws `NotSupportedException` from two methods it
can't implement correctly, or returns a fabricated receipt and a fabricated
tracking status that no caller asked for and nothing backs. Interface
Segregation names the fix: a consumer, and an implementer, should depend on
exactly the capabilities it uses, nothing wider.

## Segregating into ICarrierRates and ICarrierTracking

The chapter's actual `ICarrierGateway` is composed from two role interfaces,
each promising exactly one thing:

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

`ICarrierGateway` isn't a third role; it's the composition of the other
two, kept as one type because a concrete carrier implementation has both
capabilities and the composition root needs exactly one abstraction
to register a concrete gateway against. Nothing about segregating an
interface forbids a single implementer from satisfying more than one role at
once. `RestCarrierGateway` and `FakeCarrierGateway` both implement
`ICarrierGateway` directly, which means both automatically satisfy
`ICarrierRates` and `ICarrierTracking` too. What segregation buys is a
choice for each consumer: a class that only quotes rates can declare a
dependency on `ICarrierRates` alone and never see a `BookAsync` or
`GetTrackingStatusAsync` member on the type it holds, and the freight-broker
problem from the previous section disappears, because that integration now
implements only `ICarrierRates` and has nothing left to fake.

## Dependency inversion: who depends on whom

`ShipmentRateAdvisor` is the narrow consumer this split was built for. It
depends on `ICarrierRates`, never on `ICarrierGateway` and never on a
concrete gateway:

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

`ShipmentBookingService` is the wider consumer. It needs both booking and
tracking, so it depends on the full `ICarrierGateway`, and
still never on a concrete type:

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

Neither class imports `RestCarrierGateway` or `FakeCarrierGateway`, and
neither constructs one. Robert C. Martin's formulation of Dependency
Inversion is that high-level modules should not depend on low-level
modules; both should depend on an abstraction, and the abstraction should
not depend on the details. `RestCarrierGateway` implements
`ICarrierGateway` rather than being depended on by name, so the dependency
that would naturally run from a high-level policy down into a concrete
carrier integration is redirected to stop at the interface both sides
share. `ShipmentRateAdvisorTests` makes the payoff concrete: it substitutes
an NSubstitute double for `ICarrierRates` alone and never links against
`RestCarrierGateway`, `FakeCarrierGateway`, or anything that makes a real or
simulated network call.

## Wiring it with the built-in container

C# code reaching for two abstractions still has to answer one question
neither interface answers on its own: which concrete type backs them at
runtime. .NET answers it as part of the platform itself: the dependency
injection container, `Microsoft.Extensions.DependencyInjection`, is
described by the [.NET dependency injection documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview)
as "a built-in part of the framework, along with configuration, logging,
and the options pattern." There is no separate framework decision to make
before a project can register a service; the container is already a NuGet
package reference away, the same one pinned in this chapter's
`CarrierGateways.csproj`.

Every consumer in this library resolves `ICarrierGateway`, `ICarrierRates`,
or `ICarrierTracking` from the container, and `ServiceCollectionExtensions`
is the one composition root that names a concrete gateway type:

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

The built-in container's registration surface, described on the
[service registration documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-registration),
offers more than one shape for a reason. A plain
`services.AddSingleton<TGateway>()` registers the concrete class against
itself, the shape that documentation calls "registering a service with only
an implementation type," and the container alone is responsible for
constructing it and for resolving its own constructor dependencies, if any.
The three calls after it use a different, factory-taking overload, the one
the service registration documentation describes as able to register
"multiple implementations" and pass constructed values rather than relying
on the container's own activation logic. Each factory lambda,
`provider => provider.GetRequiredService<TGateway>()`, hands back the exact
same `TGateway` singleton instance the first line already registered, just
resolved through a different interface type each time. The result is three
service types, `ICarrierGateway`, `ICarrierRates`, and `ICarrierTracking`,
that all resolve to one object. `CarrierGatewayRegistrationTests` proves
this with `Assert.Same(gateway, rates)` and `Assert.Same(gateway, tracking)`
after resolving each interface independently from a built provider: the
container creates exactly one carrier instance and hands it out under three
different faces, which is exactly what lets `ShipmentRateAdvisor` ask for the narrow
`ICarrierRates` face and `ShipmentBookingService` ask for the full
`ICarrierGateway` face without either one knowing or caring that the other
exists.

`IServiceCollection` only describes registrations; nothing is actually
built until `BuildServiceProvider` turns it into an
[IServiceProvider](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview),
the documentation's name for ".NET's built-in service container." A call to
`provider.GetRequiredService<ShipmentRateAdvisor>()` is where the container
does the work DIP shifted away from `ShipmentRateAdvisor` itself: it
inspects the constructor, finds the single `ICarrierRates` parameter,
resolves that from the same registrations above, and passes the result in.
`CarrierGatewayRegistrationTests.Container_builds_consumers_through_constructor_injection`
exercises exactly that resolution path and checks the resulting advisor
actually produces a working quote, not merely that it was constructed.

The registrations above all choose `AddSingleton`, and that choice is
deliberate, not a default left unexamined. The
[service lifetimes documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes)
defines a singleton as created "the first time it's requested," after which
"every subsequent request of the service implementation from the
dependency injection container uses the same instance." A carrier gateway
that holds no per-caller state beyond an in-memory booking ledger is safe to
share for the lifetime of the whole application, and sharing it avoids
reconstructing an HTTP-backed client repeatedly for no benefit. The same
documentation defines the two lifetimes this chapter doesn't use: a
*transient* service "is created each time it's requested from the service
container," and a *scoped* service "indicates that services are created
once per client request (connection)" and "should always be used from
within a scope." Scoped registration exists for state that belongs to one
web request or one unit of work and must not leak into the next one; a
console library with no request boundary has nothing for a scope to attach
to, which is one more reason singleton is the correct lifetime here rather
than merely the convenient one. Choosing the wrong lifetime is a real
failure mode the same documentation calls out directly: resolving a scoped
service from a singleton makes that scoped service behave like a singleton
too, silently promoting its lifetime and producing state that leaks across
requests that were supposed to be isolated from each other.

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

The entire point of routing both consumers through abstractions is that the
composition root becomes the only place a carrier swap touches.
`CarrierGatewaySwapTests` builds two separate containers, one registered
with `AddCarrierGateway<RestCarrierGateway>()` and one with
`AddCarrierGateway<FakeCarrierGateway>()`, then resolves the same
`ShipmentRateAdvisor` and `ShipmentBookingService` types from each:

```csharp
var restServices = new ServiceCollection();
restServices.AddCarrierGateway<RestCarrierGateway>();
await using var restProvider = restServices.BuildServiceProvider();

var fakeServices = new ServiceCollection();
fakeServices.AddCarrierGateway<FakeCarrierGateway>();
await using var fakeProvider = fakeServices.BuildServiceProvider();
```

Neither `ShipmentRateAdvisor` nor `ShipmentBookingService` is recompiled,
edited, or even aware that two different providers exist. What differs
between the two runs is only what each resolved advisor and booking service
produce: the REST-backed gateway prices by destination and weight and
assigns a tracking number formatted `RST-...`, while the fake gateway
returns a flat rate and the fixed tracking number `FAKE-000001`. The test
asserts on exactly that difference,
`Assert.Contains(FakeCarrierGateway.FlatRate.ToString(), fakeDescription, ...)`
and `Assert.StartsWith("RST-", restReceipt.TrackingNumber, ...)`, proving
the swap happened at the registration line and nowhere else. A sandbox
deployment, a demo environment, or an early integration test against a
carrier whose contract isn't signed yet all become a one-line change to
which generic argument `AddCarrierGateway<TGateway>` is called with, not a
search-and-replace across every class that talks to a carrier.

## When not to apply these principles rigidly

Both principles in this chapter earn their cost only when a second
implementation or a second capability-shaped consumer is actually in the
picture. A carrier integration with exactly one implementation, no sandbox
double planned, and no test that would benefit from faking it does
not need `ICarrierRates` and `ICarrierTracking` as separate types; a single
`ICarrierGateway`, or even a concrete class called directly, is the correct
amount of abstraction for that system. Splitting a two-method interface
into two single-method interfaces because narrow interfaces sound
principled, when every consumer already needs every method on the original
interface, produces a maze of role types a reader has to chase across files
for no consumer's benefit. Segregate when consumers' needs actually differ,
the way a rate-shopping advisor and a booking service differ here; stop
once every remaining member on an interface is used by every remaining
consumer; further splitting past that point adds files without adding a
seam any consumer needs.

The built-in container itself is cheap enough that reaching for it even on
a small graph like this chapter's five registrations costs little: there's
no separate package to add or framework decision to weigh, just a handful
of `AddSingleton` calls in the composition root.
That said, a composition root can still be over-engineered in the other
direction: wrapping a single, permanent, always-production dependency in an
interface and a DI registration when the class that uses it is the only
caller that will ever exist adds an abstraction, a constructor parameter,
and a registration line with nothing on the other side of that cost to
justify it. Reach for the interface and the registration when a swap is
real, a test needs a double, or a capability varies by consumer;
skip both when the answer to "what implements this" is, and will stay, one
fixed type.

<blockquote class="ai-sidebar" markdown="1">
Ask an LLM pairing partner to "add dependency injection" to a C# class and
it will sometimes reach past the built-in container for a third-party one,
Autofac or similar, out of habit from whichever training examples it has
seen most, even when nothing in the prompt mentioned a missing capability
and the project already references `Microsoft.Extensions.DependencyInjection`.
It will also tend to register everything as scoped by reflex, carrying over
a web-request mental model into a console library or a background worker
that has no request boundary for a scope to attach to, which is exactly the
registration mistake the service-lifetimes documentation warns against.
Check a generated registration against the forces this chapter named: does
a second implementation actually exist or is coming, does a test need a
substitute, and does this process have a request-like boundary that
justifies anything narrower than singleton. A generated `IRateGateway`
wrapping one C# class with one call site, or a scoped registration inside a
tool that runs start to finish with no request in sight, is solving a
wiring problem the codebase does not have yet.
</blockquote>

## Build, run, observe

Every command below runs with the local .NET 10 SDK the prerequisites
chapter installs; nothing in this loop needs a container. The example is
standalone-testable from its own directory:

```sh
cd examples/06-isp-dip-and-built-in-di
dotnet test
```

It's also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Either path runs the same xUnit v3 suite: four
`CarrierGatewayRegistrationTests` cases that resolve `ICarrierGateway`,
`ICarrierRates`, and `ICarrierTracking` from a built provider and confirm
they share one instance; two `CarrierGatewaySwapTests` cases that build two
separate containers and confirm the registered gateway type, not any
consumer, controls the resolved behavior; and two `ShipmentRateAdvisorTests`
cases that substitute an NSubstitute double for `ICarrierRates` alone.
Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 8
  failed: 0
  succeeded: 8
  skipped: 0
  duration: 1s 232ms
```

## For deeper coverage

- Robert C. Martin, [*Agile Software Development: Principles, Patterns, and Practices*](https://www.pearson.com/en-gb/subject-catalog/p/agile-software-development-principles-patterns-and-practices-pearson-new-international-edition/P200000004932/9781292038360) (Prentice Hall): the original statement of the Interface Segregation Principle and the Dependency Inversion Principle this chapter builds on.
- The official Microsoft Learn documentation cited throughout this chapter: [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview), [Service lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes), and [Service registration](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-registration), the primary source for every claim this chapter makes about the built-in container's behavior.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
