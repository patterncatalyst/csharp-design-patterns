---
title: "Strategy: delegates, interfaces, and keyed services"
order: 11
layout: tutorial
sectionid: docs
part: "selecting-varying-behavior"
description: "Three levels of the same pattern: pass a Func, define an interface, or register keyed services, and when each is the right reach."
---

<!-- EXAMPLE (shipping domain): Rate quotes priced three ways: a delegate, an interface, and a keyed service. -->

## Learning objectives

By the end of this chapter you can:

- Explain the GoF Strategy pattern in terms of what varies and what stays
  fixed: the pricing algorithm behind a rate quote changes by service level,
  while the shape of the call, a `RateRequest` in and a `RateQuote` out,
  never does.
- Build a `DelegateRateQuoter` that prices a request through a plain
  `Func<RateRequest, Money>`, with no `IPricingStrategy` interface or
  implementing class anywhere in the type.
- Decide when a named `IPricingStrategy` interface earns its place over a
  bare delegate, and implement three strategies, `StandardTariff`,
  `ExpressTariff`, and `EconomyTariff`, that a `StrategyRateQuoter` resolves
  with no caller-side branching on which one it holds.
- Register multiple implementations of the same interface under different
  string keys with `AddKeyedSingleton`, and resolve one by a runtime key
  with `GetRequiredKeyedService<T>(key)` or by a fixed key baked into a
  constructor with `[FromKeyedServices("key")]`.
- State the difference between `GetRequiredKeyedService`, which throws when
  no service is registered under the requested key, and `GetKeyedService`,
  which returns null, and explain why a non-keyed `GetService<T>()` call
  returns null for a type that has only ever been registered with keys.

## Why this matters

A rate quote for a shipment prices the same request three different ways
depending on one string: the service level. A standard shipment, an
express shipment, and an economy shipment moving the same weight to the
same destination come back with three different prices, because the
algorithm computing the price is different for each one, not because
anything about the request itself changed. That is the exact shape the
Gang of Four's Strategy pattern names: an algorithm that varies
independently of the client asking for it, with the client's call left
untouched no matter which algorithm answers it. What varies here is
strictly the pricing rule; what stays fixed is a `RateRequest` arriving and
a `RateQuote` coming back, and the caller that makes that call never needs
to know which rule ran.

The naive way to write this system reaches for one method that branches
on the service level inline: an `if`/`else if`/`else` chain, or a `switch`
expression, with the standard formula, the express formula, and the
economy formula all written out in the same method body, keyed off a
string comparison against `request.ServiceLevel`. That method works, and
it keeps working right up until a fourth service level, a second markup
schedule for a new region, or a sandbox environment that needs a flat test
rate instead of a real one shows up. Each of those is a routine change,
and each one means reopening a method that already prices three other
service levels correctly, re-reading formulas that have nothing to do with
the one being added, and re-running every test the method has rather than
only the one that covers the new case. A unit test for the standard rate
has no way to exist on its own either, because the method computing it
can't be instantiated or called without also bringing along the express
and economy branches sitting next to it in the same function body.

This chapter works through three ways modern C# lets that pricing
algorithm vary without any of that cost, in increasing order of ceremony.
A plain `Func<RateRequest, Money>` delegate is a Strategy with nothing
built on top of it: a lambda is the algorithm, and swapping it is passing
a different one in. A named `IPricingStrategy` interface is the textbook
GoF shape, earning its keep once an algorithm needs a name, a home for its
own tests, or a place in the domain vocabulary a team actually discusses.
A keyed service registered in the built-in dependency injection container
goes a step further still: the string that picks the algorithm is no
longer something the caller's code even branches on, because the
container resolves it from a runtime key the request already carries. All
three answer the identical two questions, which algorithm runs and how a
caller finds it, with less and less of that decision visible in the
caller's own code as the ceremony goes up, and knowing when to stop at
which level is as much the point of this chapter as any of the three
mechanisms themselves.

{% include excalidraw.html name="11-strategy-delegates-interfaces-keyed-services" caption="Figure 11.1: The GoF Strategy pattern supplied three ways: a Func<RateRequest, Money> delegate with no interface in sight, a named IPricingStrategy interface implemented by StandardTariff, ExpressTariff, and EconomyTariff, and a keyed DI registration resolved by GetRequiredKeyedService(key); the caller's call shape, a RateRequest in and a RateQuote out, is identical at every level." %}

## One Strategy, three levels of ceremony

The example behind this chapter lives in
`examples/11-strategy-delegates-interfaces-keyed-services`. Every tier
prices the same two types. A `RateRequest` carries a destination, a
weight, and a service level that doubles as a lookup key at the
keyed-service tier:

```csharp
public sealed record RateRequest(string Destination, decimal WeightKilograms, string ServiceLevel);

public sealed record RateQuote(string Destination, Money Price, string ServiceLevel);
```

`Money` is a validating value type, a `readonly record struct` whose
primary constructor rejects a negative amount or a malformed currency code
on every construction path, with `Money.Of(amount)` as the call-site sugar
every tariff in this example uses, defaulting to US dollars. Pricing a
100-kilogram shipment comes back at three different totals depending on
which tariff answers the request: 400.00 USD for `StandardTariff`'s flat
4.00-per-kilogram rate, 650.00 USD for `ExpressTariff`'s 2.50-per-kilogram
surcharge on top of that, and 300.00 USD for `EconomyTariff`'s 1.00-per-
kilogram discount under it. Three quoter types, `DelegateRateQuoter`,
`StrategyRateQuoter`, and `KeyedTariffQuoter`, each expose the identical
`Quote(RateRequest) : RateQuote` call shape, and each gets that 400.00,
650.00, or 300.00 answer from a different mechanism entirely. None of the
three quoters ever branches on the service level string itself; each one
hands the question of which algorithm runs to something else, a supplied
delegate, a constructor-injected interface, or a keyed container
resolution, and that handoff is the whole chapter.

## Level 1: Strategy dissolved to a delegate

The GoF catalogue's own description of Strategy is a family of algorithms,
each one encapsulated, made interchangeable behind a common signature. A
`Func<RateRequest, Money>` already is exactly that: a value of a delegate
type, interchangeable with any other value of the same type, invoked
through one signature regardless of which concrete lambda or method group
it was assigned from. `DelegateRateQuoter` takes one of these in its
constructor and calls it, with no `IPricingStrategy` interface, no
`StandardTariff` class, and no implementing type of any kind in the
picture:

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

`priceStrategy` is the entire strategy. `DelegateRateQuoterTests.Prices_a_request_through_the_supplied_delegate`
constructs a `DelegateRateQuoter` with a lambda computing
`4.00m * request.WeightKilograms` and asserts a 400.00 USD quote for a
100-kilogram request to Rotterdam, the same number `StandardTariff` would
produce, reached with no class implementing anything. The next two tests
make the actual point of this tier: `A_different_delegate_changes_the_quote_from_the_same_quoter_type`
builds a second `DelegateRateQuoter`, same type, against a lambda that
adds a 0.50-per-kilogram surcharge captured in a local `const`, and gets
450.00 USD back from a 100-kilogram request; `A_discount_closure_also_changes_the_quote_without_a_new_type`
builds a third one against a lambda capturing a 1.00-per-kilogram discount
and gets 300.00 USD. Three different prices, three different closures,
one unchanged `DelegateRateQuoter` type across all three. Swapping the
pricing algorithm at this tier is passing a different lambda expression to
a constructor; it is not writing a new class, implementing an interface,
or touching anything that already compiles.

This is what "dissolving" Strategy into a delegate means concretely: the
GoF pattern's intent, an algorithm a client can swap without the client's
own code changing, is fully satisfied by a `Func<RateRequest, Money>`
value on its own. Nothing about the pattern's purpose requires a named
type, a `Price` method, or an interface declaration; those are the
mechanism a language without first-class functions needs to reach the same
place. A C# closure captures whatever state a particular pricing rule
needs, a surcharge rate, a discount rate, a flat test value for a sandbox
run, directly in its enclosing scope, and that capture is the entire
configuration surface the algorithm has. There is nothing to construct
beyond the lambda itself, and nothing to look up by name, which is exactly
right for a pricing rule that exists for one call site, one test, or one
short-lived experiment and has no other reason to be a citizen of the
domain model.

## Level 2: when the interface earns its place

A delegate stops being the right answer the moment a pricing rule needs to
be more than a lambda somebody wrote inline: a named thing a team discusses
in a planning meeting, a type with its own file and its own focused test
class, something a sandbox environment or a second consumer can reference
by name without recreating the formula. `StandardTariff`, `ExpressTariff`,
and `EconomyTariff` are exactly that. Each implements the one-method
`IPricingStrategy` interface:

```csharp
public interface IPricingStrategy
{
    Money Price(RateRequest request);
}

public sealed class ExpressTariff : IPricingStrategy
{
    public const decimal SurchargePerKilogram = 2.50m;

    public Money Price(RateRequest request) =>
        Money.Of((StandardTariff.BaseRatePerKilogram + SurchargePerKilogram) * request.WeightKilograms);
}
```

`StrategyRateQuoter` is the textbook GoF consumer, a constructor-injected
`IPricingStrategy` it calls through, never a concrete tariff type it
constructs itself:

```csharp
public sealed class StrategyRateQuoter(IPricingStrategy strategy)
{
    public RateQuote Quote(RateRequest request)
    {
        var price = strategy.Price(request);
        return new RateQuote(request.Destination, price, request.ServiceLevel);
    }
}
```

Set the delegate form and the interface form side by side and the
difference is almost entirely in where the algorithm's identity lives.
`DelegateRateQuoter` takes an anonymous value, a `Func<>`, that is the
algorithm; `StrategyRateQuoter` takes a named value, an `IPricingStrategy`,
that implements the algorithm, and that name is what a reviewer reads in a
stack trace, a test name, or a dependency injection registration instead
of an opaque lambda.

{% include codetabs.html langs="Delegate|Interface" %}
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
```csharp
public sealed class StrategyRateQuoter(IPricingStrategy strategy)
{
    public RateQuote Quote(RateRequest request)
    {
        var price = strategy.Price(request);
        return new RateQuote(request.Destination, price, request.ServiceLevel);
    }
}
```

`StrategyRateQuoterTests.Standard_and_express_strategies_yield_different_quotes_from_the_unchanged_caller`
builds one `StrategyRateQuoter` against `new StandardTariff()` and a second
against `new ExpressTariff()`, quotes the same 100-kilogram request to
Singapore through each, and asserts 400.00 USD from the first and 650.00
USD from the second, with `Assert.NotEqual` confirming the two prices
genuinely differ. `Economy_strategy_prices_under_standard_from_the_same_caller_type`
does the same against `EconomyTariff` and gets 300.00 USD. In every case
`StrategyRateQuoter` itself is the identical type, unmodified, across all
three strategies; only the constructor argument changes. That is what the
interface buys over the delegate: `StandardTariff`, `ExpressTariff`, and
`EconomyTariff` are now things a reader can look up by name, document with
their own `BaseRatePerKilogram`, `SurchargePerKilogram`, and
`DiscountPerKilogram` constants, test in isolation with no `RateRequest`
routing logic anywhere nearby, and eventually register in a dependency
injection container the way the next tier does. None of that is available
to a bare `Func<>` value, because a lambda has no identity beyond the code
inside it and nowhere to attach a name, a constant, or a registration.

## Level 3: keyed services pick the strategy by a runtime key

An interface answers "how does a consumer call an algorithm without
knowing its concrete type," but it still leaves open who decides which
concrete type a particular `StrategyRateQuoter` gets constructed with.
Everything so far has answered that at the call site, in test code,
directly: `new StrategyRateQuoter(new ExpressTariff())`. A real rate
quoting service does not get to do that, because the service level
determining which tariff should run arrives as data, a string on the
incoming request, not as a compile-time choice a developer bakes into a
`new` expression. Picking the right tariff by that string, without the
caller writing its own `if ("standard" == request.ServiceLevel)` chain,
is the problem the built-in DI container's keyed services answer.

The [dependency injection documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview)
describes keyed services directly: "you can register services and perform
lookups based on a key. In other words, it's possible to register
multiple services with different keys and use this key for the lookup."
`ServiceCollectionExtensions.AddRateQuoting` is the one composition root
that registers all three tariffs behind the same `IPricingStrategy`
interface, each under its own string key:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRateQuoting(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IPricingStrategy, StandardTariff>("standard");
        services.AddKeyedSingleton<IPricingStrategy, ExpressTariff>("express");
        services.AddKeyedSingleton<IPricingStrategy, EconomyTariff>("economy");

        services.AddSingleton<KeyedTariffQuoter>();
        services.AddSingleton<ExpressLaneQuoter>();

        return services;
    }
}
```

`AddKeyedSingleton` is one of three keyed lifetime overloads the container
ships; `AddKeyedScoped` and `AddKeyedTransient` read identically, just
with a different lifetime applied to each key. The three tariffs in this
example hold no per-call state, so a shared singleton per key costs
nothing to reuse and avoids reconstructing a stateless object on every
request. `KeyedTariffQuoter` resolves the tariff matching a request's
service level directly from an injected `IServiceProvider`, using the
request itself as the source of the key:

```csharp
public sealed class KeyedTariffQuoter(IServiceProvider serviceProvider)
{
    public RateQuote Quote(RateRequest request)
    {
        var strategy = serviceProvider.GetRequiredKeyedService<IPricingStrategy>(request.ServiceLevel);
        var price = strategy.Price(request);
        return new RateQuote(request.Destination, price, request.ServiceLevel);
    }
}
```

`KeyedTariffQuoterTests.Resolves_the_express_strategy_by_key_and_prices_accordingly`
resolves `GetRequiredKeyedService<IPricingStrategy>("express")` straight
from a built provider, asserts the resolved instance is an `ExpressTariff`
with `Assert.IsType`, and confirms it prices a 100-kilogram request at
650.00 USD. `Dispatches_to_the_standard_tariff_by_service_level`,
`Dispatches_to_the_express_tariff_by_service_level`, and
`Dispatches_to_the_economy_tariff_by_service_level` each build a
`KeyedTariffQuoter` through `GetRequiredService` and quote the same
Rotterdam request with a different `ServiceLevel` string, landing at
400.00, 650.00, and 300.00 USD respectively, with no edit to
`KeyedTariffQuoter` between any of the three. `Swapping_the_requested_key_changes_the_quote_without_editing_the_caller`
makes that explicit: the same `KeyedTariffQuoter` instance quotes a
standard request and then an economy request, and the two prices differ
because the key on the request changed, not because anything about the
quoter did. Adding a fourth service level later is one more
`AddKeyedSingleton` line in `AddRateQuoting` and one new tariff class;
`KeyedTariffQuoter` itself never changes, because it was never the thing
that knew the list of valid keys in the first place.

`ExpressLaneQuoter` shows the other shape keyed resolution takes, a fixed
key chosen once at construction rather than a variable one read off each
request:

```csharp
public sealed class ExpressLaneQuoter([FromKeyedServices("express")] IPricingStrategy expressStrategy)
{
    public RateQuote Quote(RateRequest request)
    {
        var price = expressStrategy.Price(request);
        return new RateQuote(request.Destination, price, "express");
    }
}
```

The [`FromKeyedServicesAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.fromkeyedservicesattribute)
on the constructor parameter tells the container which keyed registration
to bind, at the moment `ExpressLaneQuoter` itself is constructed, rather
than asking `KeyedTariffQuoter`'s question, "which key does this
particular call want," at all. `KeyedTariffQuoterTests.FromKeyedServices_consumer_resolves_the_fixed_express_key`
resolves an `ExpressLaneQuoter` and quotes a request whose `ServiceLevel`
is `"standard"`, and still gets 650.00 USD back with the resulting
`RateQuote.ServiceLevel` set to `"express"`, because `ExpressLaneQuoter`
was wired to the express tariff permanently at construction and never
reads the request's own service level to decide which strategy to call.
That is the right tool when a consumer's identity, not the request it is
handling, determines which registration applies, an express-only
processing lane being the clearest case: every request that reaches it
gets the express tariff, regardless of what string it happens to carry.

Two correctness traps around keyed services are worth knowing before
reaching for them, and both are proven directly by this example's tests
rather than left as unverified claims. First, `GetRequiredKeyedService`
and `GetKeyedService` disagree about what to do with an unregistered key:
the [`GetRequiredKeyedService` API reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceproviderkeyedserviceextensions.getrequiredkeyedservice)
documents an `InvalidOperationException` when "there is no service of type
`T`" for the given key, while `GetKeyedService` is the non-throwing
counterpart. `An_unknown_key_throws_from_GetRequiredKeyedService_but_GetKeyedService_returns_null`
asks the same built provider for `"overnight"`, a key `AddRateQuoting`
never registers, through both methods: `GetRequiredKeyedService` throws
`InvalidOperationException`, and `GetKeyedService` returns null. Second,
and easier to miss, keyed and non-keyed registrations for the same service
type do not share a lookup: `A_non_keyed_resolution_returns_null_because_keyed_and_non_keyed_are_separate_graphs`
calls the ordinary, keyless `provider.GetService<IPricingStrategy>()`
against a provider built entirely from `AddRateQuoting`'s three keyed
registrations, and gets null back, even though three tariffs implementing
`IPricingStrategy` are very much registered, just never under the empty,
keyless lookup that plain `GetService` performs. A team that registers
every tariff with `AddKeyedSingleton` and then reaches for a plain
constructor-injected `IPricingStrategy` somewhere else in the same
container, expecting the container to pick one of the keyed registrations
by default, gets a runtime failure at resolution time instead, because no
default exists; a type is either resolvable without a key or resolvable
with one, for each distinct key, and registering it one way buys nothing
for the other.

{% include excalidraw.html name="11-keyed-service-resolution" caption="Figure 11.2: The keyed-DI composition root registering three IPricingStrategy implementations under the standard, express, and economy keys, KeyedTariffQuoter and ExpressLaneQuoter resolving them at runtime, an unrecognized key throwing from GetRequiredKeyedService but returning null from GetKeyedService, and a plain GetService<IPricingStrategy>() returning null because keyed and non-keyed lookups are separate graphs." %}

## Build, run, observe

Every command below runs with the local .NET 10 SDK; nothing in this loop
needs a container. The example is standalone-testable from its own
directory:

```sh
cd examples/11-strategy-delegates-interfaces-keyed-services
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. The suite splits across three files by exactly the
three tiers this chapter covers: three `DelegateRateQuoterTests` cases
proving a quoter prices through whatever delegate it is given and that a
different closure changes the quote without a new type; two
`StrategyRateQuoterTests` cases proving `StandardTariff`, `ExpressTariff`,
and `EconomyTariff` yield different prices from an unchanged
`StrategyRateQuoter`; and eight `KeyedTariffQuoterTests` cases covering
keyed resolution by key, dispatch across all three registered service
levels, the fixed-key `FromKeyedServices` consumer, the
throws-versus-null distinction between `GetRequiredKeyedService` and
`GetKeyedService`, the separate-graphs trap between keyed and non-keyed
resolution, and swapping the requested key with no edit to the caller.
Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 13
  failed: 0
  succeeded: 13
  skipped: 0
  duration: 969ms
```

## When not to use it

Each tier in this chapter costs more ceremony than the one before it, and
that cost only pays for itself when the thing it buys is actually needed.
A delegate is enough, and an interface is over-abstraction, when a pricing
rule has exactly one call site, no second consumer, no independent test
suite of its own, and nothing about it that a team would ever discuss by
name in a planning conversation. Promoting a one-off lambda to a named
`IPricingStrategy` implementation adds a file, an interface a reviewer has
to trace back to its single implementer, and a constructor parameter that
exists only because the pattern says it should, not because a second
tariff is coming or a test needs to substitute one. The direction worth
defaulting toward is the dissolve this chapter demonstrates, interface
down to delegate, not the reverse; reach for `IPricingStrategy` only once
a second named implementation genuinely exists, a consumer needs to be
tested against a substitute, or the algorithm is about to be registered in
a container the way this chapter's third tier does.

Keyed services are the wrong reach even more often than a standalone
interface is. The entire value of a keyed registration is that the
selecting value is data the caller does not control at compile time, a
string arriving on a request, a row from a database, a header on an
incoming message. A fixed, small set of named strategies with nothing
picking among them at runtime, two call sites that will only ever want
`StandardTariff` and `ExpressTariff` respectively and never ask for either
by a string key, gets nothing from `AddKeyedSingleton` that a plain
constructor-injected `IPricingStrategy`, registered once per consumer,
does not already provide, and it adds a string-keyed lookup table, a
composition root entry per key, and the throw-on-miss behavior this
chapter just walked through as a new failure mode with nothing on the
other side to justify it. Keyed services earn their place exactly where
`KeyedTariffQuoter` uses them: a key that already exists as part of the
request, with no code anywhere that would otherwise have to branch on it.

> **AI sidebar.** Ask an AI pairing partner to make a pricing choice
> "configurable by service level" and a common result is a hand-rolled
> `Dictionary<string, IPricingStrategy>` built and populated by hand inside
> a factory class, wired up with a static initializer or a module
> constructor, reinventing exactly what `AddKeyedSingleton` and
> `GetRequiredKeyedService` already do, out of habit from material written
> before keyed services existed in the base class library. The same prompt
> sometimes produces the opposite mistake: an `IPricingStrategy` interface
> and a DI registration wrapped around a single tariff that will never
> have a second implementation, because "add an abstraction" is the
> reflexive answer to "make this swappable" regardless of whether a swap
> is actually planned. Check a generated answer against the two questions
> this chapter asks directly: does the selecting value genuinely arrive as
> runtime data the code does not control, which is what a keyed
> registration is for, and does a second implementation of the interface
> actually exist or is clearly coming, which is what the interface itself
> is for. A dictionary a developer populates by hand with every valid key
> already known at compile time is not configurability; it is the
> container's own job, done worse and left untested by the suite the
> container's own keyed-service tests already provide for free.

## For deeper coverage

- The official Microsoft Learn documentation cited throughout this
  chapter: [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview),
  including its keyed services section, the primary source for every claim
  this chapter makes about `AddKeyedSingleton` and resolving a service by a
  runtime key.
- [`ServiceProviderKeyedServiceExtensions.GetRequiredKeyedService`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceproviderkeyedserviceextensions.getrequiredkeyedservice)
  and [`FromKeyedServicesAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.fromkeyedservicesattribute)
  in the API reference: the throw-on-miss behavior `KeyedTariffQuoter`
  relies on and the fixed-key constructor binding `ExpressLaneQuoter`
  relies on, respectively. Both claims are also backed directly by
  `KeyedTariffQuoterTests` in the runnable example.
- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the source
  for the Strategy pattern this chapter dissolves across three levels of
  ceremony.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
