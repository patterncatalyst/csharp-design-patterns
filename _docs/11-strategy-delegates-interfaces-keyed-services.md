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

- Say what varies and what stays fixed in Strategy: the pricing rule changes by service level, while a `RateRequest` in and a `RateQuote` out never does.
- Price a request through a plain `Func<RateRequest, Money>` with no interface or implementing class.
- Decide when a named `IPricingStrategy` beats a delegate, and resolve `StandardTariff`, `ExpressTariff` and `EconomyTariff` with no caller-side branching.
- Register several implementations under string keys with `AddKeyedSingleton`, and resolve one by a runtime key with `GetRequiredKeyedService<T>(key)` or a fixed key with `[FromKeyedServices("key")]`.
- Say how `GetRequiredKeyedService` and `GetKeyedService` treat an unknown key, and why `GetService<T>()` returns null for a type registered only with keys.

## Why this matters

A rate quote prices the same request three ways depending on one string, the service level. Standard, express and economy shipments of the same weight to the same destination get three prices because the pricing rule differs, not the request.

The obvious version is one method with an `if`/`else` chain or a `switch` on `request.ServiceLevel`, holding all three formulas. It works until a fourth service level, a regional markup or a sandbox with a flat test rate arrives. Each change reopens a method that already prices three levels correctly, and the standard rate can't be tested without the express and economy branches beside it.

C# lets the rule vary at three levels of ceremony: a delegate, a named interface, and a keyed service in the built-in container. The skill is knowing where to stop.

{% include excalidraw.html name="11-strategy-delegates-interfaces-keyed-services" caption="Figure 11.1: The same Strategy pattern supplied three ways, a bare delegate, a named interface with three implementations, and a keyed DI registration, with the caller's call shape unchanged at every level." %}

## Pattern: Strategy

### The problem

A quoting service must price a request by the rule its service level names, add rules without reopening the ones that work, and test each rule on its own. The caller that asks for a quote shouldn't know which rule ran.

### The solution

A **Strategy** encapsulates each algorithm in a family behind one signature, so a client can swap the algorithm without its own code changing.

Every level prices the same two types:

```csharp
public sealed record RateRequest(string Destination, decimal WeightKilograms, string ServiceLevel);

public sealed record RateQuote(string Destination, Money Price, string ServiceLevel);
```

`Money` is a validating `readonly record struct`; `Money.Of(amount)` defaults to US dollars. For 100 kilograms:

| Tariff | Rule | Quote |
| --- | --- | --- |
| `StandardTariff` | 4.00 per kg | 400.00 USD |
| `ExpressTariff` | 2.50 per kg surcharge on standard | 650.00 USD |
| `EconomyTariff` | 1.00 per kg discount under standard | 300.00 USD |

### How C# models it

Three quoters, `DelegateRateQuoter`, `StrategyRateQuoter` and `KeyedTariffQuoter`, expose the same `Quote(RateRequest) : RateQuote` call. None branches on the service level. Each hands the choice to something else: a supplied delegate, a constructor-injected interface or a keyed container lookup.

| Level | Strategy is | Chosen by |
| --- | --- | --- |
| Delegate | A `Func<RateRequest, Money>` | Whoever constructs the quoter |
| Interface | A named `IPricingStrategy` | Whoever constructs the quoter |
| Keyed service | A keyed `IPricingStrategy` registration | A key read from the request, or fixed at construction |

### Level 1: Strategy dissolved to a delegate

A family of interchangeable algorithms behind one signature is what a delegate type already is:

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

`priceStrategy` is the whole strategy. A lambda computing `4.00m * request.WeightKilograms` quotes 400.00 USD for 100 kilograms to Rotterdam. A second lambda that captures a 0.50-per-kilogram surcharge in a local `const` quotes 450.00 USD, and a third with a 1.00 discount quotes 300.00 USD, all through the same `DelegateRateQuoter` type. Swapping the rule is passing a different lambda, not writing a class.

The pattern's intent needs no named type, `Price` method or interface; those are what a language without first-class functions uses to get there. A closure captures the state a rule needs, a surcharge, a discount, a flat sandbox value, and that capture is its whole configuration. That fits a rule that exists for one call site, one test or one experiment.

### Level 2: when to define an interface

A delegate stops fitting when the rule needs to be a named thing: discussed by the team, in its own file with its own tests, referenced by a second consumer without copying the formula.

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

`StrategyRateQuoter` is the GoF consumer. It calls through an injected `IPricingStrategy` and never constructs a tariff:

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

The same 100-kilogram request to Singapore quotes 400.00 USD through `StandardTariff`, 650.00 through `ExpressTariff` and 300.00 through `EconomyTariff`, with `StrategyRateQuoter` unchanged. The difference from the delegate is where the algorithm's identity lives. A tariff has a name a reviewer sees in a stack trace or a registration, its own `BaseRatePerKilogram`, `SurchargePerKilogram` or `DiscountPerKilogram` constant, its own tests, and a type a container can register. A lambda has nowhere to attach any of these.

### Level 3: keyed services pick the strategy by a runtime key

An interface hides the concrete type, but somebody still picks it, and so far that was a `new` expression: `new StrategyRateQuoter(new ExpressTariff())`. A quoting service gets the service level as data on the request. Picking the tariff by that string without an `if ("standard" == request.ServiceLevel)` chain is what keyed services do. The [dependency injection documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview) puts it this way: "you can register services and perform lookups based on a key. In other words, it's possible to register multiple services with different keys and use this key for the lookup."

One composition root registers all three tariffs behind `IPricingStrategy`:

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

`AddKeyedScoped` and `AddKeyedTransient` read the same with a different lifetime. The tariffs hold no per-call state, so one singleton per key is safe to share. `KeyedTariffQuoter` takes the key from the request:

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

A provider resolves `"express"` to an `ExpressTariff`. The same `KeyedTariffQuoter` quotes a Rotterdam request at 400.00, 650.00 or 300.00 USD as the `ServiceLevel` changes, with no edit between them. A fourth service level is one more `AddKeyedSingleton` line and one tariff class; the quoter never knew the list of keys.

`ExpressLaneQuoter` binds a fixed key at construction instead:

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

[`FromKeyedServicesAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.fromkeyedservicesattribute) tells the container which registration to inject when `ExpressLaneQuoter` is built. A request marked `"standard"` still quotes 650.00 USD with `ServiceLevel` `"express"`, because the quoter never reads the request's level. Use it when the consumer's identity, an express-only lane, decides the registration rather than the request.

### Two keyed-service traps

| Lookup | Unregistered key or keyless lookup |
| --- | --- |
| `GetRequiredKeyedService<IPricingStrategy>("overnight")` | Throws `InvalidOperationException` |
| `GetKeyedService<IPricingStrategy>("overnight")` | Returns null |
| `GetService<IPricingStrategy>()` with only keyed registrations | Returns null |

The [`GetRequiredKeyedService` reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceproviderkeyedserviceextensions.getrequiredkeyedservice) documents the exception when "there is no service of type `T`" for the key.

The keyless case is easier to miss. Three tariffs implement `IPricingStrategy`, but none is registered under the keyless lookup, so a plain constructor-injected `IPricingStrategy` elsewhere in the same container fails at resolution. The container doesn't pick a keyed registration as a default; keyless and each key are separate lookups.

{% include excalidraw.html name="11-keyed-service-resolution" caption="Figure 11.2: A composition root registers three strategy implementations under separate keys; an unrecognized key throws from one resolution method but returns null from another, and a plain lookup finds neither." %}

## When not to use it

- **An interface for a one-off rule.** A rule with one call site, no second consumer, no tests of its own and no name the team uses is a delegate. Promoting it adds a file, an interface with a single implementer and a constructor parameter that exists because the pattern says so. Default to dissolving interface to delegate; reach for `IPricingStrategy` once a second implementation exists, a consumer needs a substitute in tests, or the rule goes into a container.
- **Keyed services without runtime selection.** Keys earn their place when the selecting value is data the caller doesn't control at compile time: a string on a request, a database row, a message header. Two call sites that always want `StandardTariff` and `ExpressTariff` get nothing from `AddKeyedSingleton` that a plain injected `IPricingStrategy` doesn't give, and they gain a string lookup table, a registration per key and a throw-on-miss failure mode.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a strategy.** "Make pricing configurable by service level" leaves open whether the level arrives as runtime data, how many implementations exist, and whether a hand-built `Dictionary<string, IPricingStrategy>` or the container does the lookup. State it:

> Register `StandardTariff`, `ExpressTariff` and `EconomyTariff` as `IPricingStrategy` with `AddKeyedSingleton` under `"standard"`, `"express"` and `"economy"` in one `AddRateQuoting` extension. Have `KeyedTariffQuoter` resolve `GetRequiredKeyedService<IPricingStrategy>(request.ServiceLevel)`. Don't build a dictionary of strategies by hand, and don't add an interface for a rule with one implementation; pass a `Func<RateRequest, Money>` there.

Ask which value selects the strategy at runtime; if none does, the keys go.
</blockquote>

## Build, run, observe

```sh
cd examples/11-strategy-delegates-interfaces-keyed-services
dotnet test
```

All 13 tests pass, and `scripts/verify.sh` runs them with every other example. They cover each level: three for delegates, two for the interface and eight for keyed resolution, including the fixed-key consumer and both traps.

## For deeper coverage

- The official Microsoft Learn documentation: [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview),
  including its keyed services section.
- [`ServiceProviderKeyedServiceExtensions.GetRequiredKeyedService`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.serviceproviderkeyedserviceextensions.getrequiredkeyedservice)
  and [`FromKeyedServicesAttribute`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.fromkeyedservicesattribute)
  in the API reference: the throw-on-miss behavior `KeyedTariffQuoter`
  relies on and the fixed-key constructor binding `ExpressLaneQuoter`
  relies on.
- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the source
  for the Strategy pattern.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
