---
title: "Decorator and Proxy"
order: 19
layout: tutorial
sectionid: docs
part: "composing-adapting-structure"
description: "Decorator and Proxy through DI decoration and DispatchProxy, and the attribute-versus-Decorator confusion."
---

<!-- EXAMPLE (shipping domain): A rate pipeline of discount, surcharge, and tax; a caching proxy over a slow carrier gateway. -->

## Learning objectives

By the end of this chapter you can:

- Build a discount, surcharge and tax chain over `IRateCalculator` in which every layer is itself a complete implementation.
- Trace a rate through the chain and explain why nesting order changes the arithmetic.
- Register the fully decorated service from a factory lambda, `AddSingleton<IRateCalculator>(sp => ...)`, so consumers never assemble a layer.
- Build a caching Proxy, `CachingRateGatewayProxy`, that calls its collaborator once per distinct request, and state how its intent differs from a Decorator's.
- Build a dynamic proxy with `DispatchProxy`, explain what `Invoke(MethodInfo?, object?[]?)` does, and tell a plain attribute apart from either kind of proxy.

## Why this matters

A 100-kilogram shipment at 5.00 per kilogram starts at 500.00. A volume discount takes ten percent off, a fuel surcharge adds five percent to the discounted figure, and tax adds eight percent to that. Each rule changes on its own schedule: discount tiers are renegotiated, surcharges follow fuel prices, tax follows the jurisdiction.

Written as one method, all four numbers and three operations share a body. When a lane needs tax but no surcharge, removing the surcharge means editing the code that also computes the discount and tax, and no adjustment can be tested or reused alone. A cache added inside a slow carrier client mixes two concerns that change for different reasons: how a rate is fetched and whether it's worth remembering.

Both patterns wrap instead of editing. A Decorator implements the interface it wraps and adds one thing to the result. A Proxy has the same shape but controls how and when the wrapped object is called: a cache in front of an expensive lookup, or an interceptor that records each call. Either way the caller holds one interface reference and doesn't know what sits behind it.

{% include excalidraw.html name="19-decorator-and-proxy" caption="Figure 19.1: Decorators layer discount, surcharge, and tax adjustments around a base rate calculator, each implementing the same interface. A caching proxy wraps a slow carrier gateway, and a dynamic proxy logs and forwards every call." %}

## Pattern: Decorator

### The problem

Three independent adjustments apply to one base rate, in a fixed order, and some lanes need only some of them. Adding a fourth, or dropping one for a lane, shouldn't touch the others.

### The solution

A **Decorator** wraps an object in another object with the same interface and adds one responsibility to it.

### How C# models it

Every calculation, base or decorated, takes a `RateRequest` and returns a `Money`:

```csharp
public sealed record RateRequest(string Origin, string Destination, decimal Kilograms);

public readonly record struct Money(decimal Amount)
{
    public static Money Of(decimal amount) => new(amount);
}

public interface IRateCalculator
{
    Money Quote(RateRequest request);
}
```

`Money` validates on every construction path, so no chain can produce a negative quote. The innermost layer is a flat per-kilogram rate:

```csharp
public sealed class BaseRateCalculator(decimal ratePerKilogram) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(request.Kilograms * ratePerKilogram);
}
```

Each decorator takes an `IRateCalculator` and implements the same interface:

```csharp
public sealed class DiscountRateDecorator(IRateCalculator inner, decimal rate) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(inner.Quote(request).Amount * (1m - rate));
}
```

`SurchargeRateDecorator` and `TaxRateDecorator` multiply by `1m + rate` instead. None of the three knows the others exist or checks what `inner` is. Because every layer implements `IRateCalculator`, any chain of them is a complete `IRateCalculator`.

### Order is arithmetic

```csharp
var calculator = new TaxRateDecorator(
    new SurchargeRateDecorator(
        new DiscountRateDecorator(new BaseRateCalculator(5.00m), rate: 0.10m),
        rate: 0.05m),
    rate: 0.08m);

var quote = calculator.Quote(new RateRequest("Rotterdam", "Santos", 100m));
```

Read from the inside out:

| Layer | Rate | Amount |
| --- | --- | --- |
| `BaseRateCalculator` | 5.00 per kg | 500.00 |
| `DiscountRateDecorator` | 0.10 off | 450.00 |
| `SurchargeRateDecorator` | 0.05 on | 472.50 |
| `TaxRateDecorator` | 0.08 on | 510.30 |

Tax sits outermost because it's charged on the discounted, surcharged amount; a different nesting gives a different figure with the same three rates. Delete the surcharge constructor call and tax applies to 450.00, giving 486.00. A single 0.20 discount layer over the base quotes 400.00: a chain one layer deep is still an `IRateCalculator`.

### Decorator is not an attribute

An attribute such as `[Obsolete]` or a custom `[Cacheable]` looks like it adds behavior around a method, but it's inert metadata. Nothing runs because it's present unless other code finds it through reflection and acts on it. A Decorator is ordinary composition: `TaxRateDecorator` runs because a caller constructed it and called `Quote`. The mechanism that can intercept a call without a visible constructor chain is `DispatchProxy`, covered under Proxy.

### Resolving the whole chain from the container

Building the chain by hand suits a unit test; scattering `new TaxRateDecorator(new SurchargeRateDecorator(...))` across call sites doesn't. `AddRatePipeline` lists the chain once:

{% raw %}
```csharp
public static IServiceCollection AddRatePipeline(this IServiceCollection services)
{
    services.AddSingleton(_ => new BaseRateCalculator(ratePerKilogram: 5.00m));

    services.AddSingleton<IRateCalculator>(provider => new TaxRateDecorator(
        new SurchargeRateDecorator(
            new DiscountRateDecorator(provider.GetRequiredService<BaseRateCalculator>(), rate: 0.10m),
            rate: 0.05m),
        rate: 0.08m));

    return services;
}
```
{% endraw %}

The second registration is for `IRateCalculator`, not `TaxRateDecorator`, so every consumer gets the full chain and no decorator type is named outside this method. The factory resolves `BaseRateCalculator` from the provider, so the base registered as its own service and the base inside the chain are the same singleton.

| Resolve | Concrete type | Quote for 100 kg |
| --- | --- | --- |
| `IRateCalculator` | `TaxRateDecorator` | Matches the hand-built chain |
| `BaseRateCalculator` | `BaseRateCalculator` | The unadjusted figure |

A reporting job that wants the base figure asks for `BaseRateCalculator`; everyone else gets the adjusted rate without knowing discount, surcharge and tax exist. A fourth adjustment, such as a seasonal peak surcharge, is one more layer in the lambda and no change to any consumer.

## Pattern: Proxy

### The problem

A carrier rate lookup is a network round trip. The same request comes in repeatedly, and putting a dictionary inside the carrier client ties caching to fetching.

### The solution

A **Proxy** stands in for another object with the same interface and controls access to it.

### How C# models it

The collaborator returns a carrier's rate and transit time:

```csharp
public sealed record CarrierRate(Money Amount, int TransitDays);

public interface ICarrierRateGateway
{
    CarrierRate GetRate(RateRequest request);
}
```

`SlowCarrierRateGateway` stands in for the real integration and counts its own invocations:

```csharp
public sealed class SlowCarrierRateGateway : ICarrierRateGateway
{
    public int Calls { get; private set; }

    public CarrierRate GetRate(RateRequest request)
    {
        Calls++;
        return new CarrierRate(Money.Of(request.Kilograms * 7.50m), TransitDays: 3);
    }
}
```

The proxy implements the same interface:

```csharp
public sealed class CachingRateGatewayProxy(ICarrierRateGateway inner) : ICarrierRateGateway
{
    private readonly Dictionary<RateRequest, CarrierRate> cache = [];

    public CarrierRate GetRate(RateRequest request)
    {
        if (cache.TryGetValue(request, out var cached))
        {
            return cached;
        }

        var rate = inner.GetRate(request);
        cache[request] = rate;
        return rate;
    }
}
```

`RateRequest` is a record, so two separately constructed requests with the same origin, destination and weight are equal and hit the same cache key.

| Calls through the proxy | Inner gateway calls |
| --- | --- |
| One request, then an equal but separate instance | 1 |
| The same request three times | 1 |
| Two requests to different destinations | 2 |

The cached value always equals what the inner gateway returns directly. A cache may change how often the collaborator runs, never what it answers.

### Proxy against Decorator

Both `TaxRateDecorator` and `CachingRateGatewayProxy` implement the interface they wrap and hold the thing underneath. The difference is intent:

| | Decorator | Proxy |
| --- | --- | --- |
| Changes the result | Yes: adds tax, removes a discount | No: same answer as the wrapped object |
| What a caller can observe | The output | Only cost: time taken, `Calls` |
| Typical uses | Pricing adjustments, formatting | Caching, logging, access control |

### DispatchProxy: one interceptor for any interface

`CachingRateGatewayProxy` is hand-written: one class for one interface. `DispatchProxy` lets interception logic be written once and placed in front of any interface:

```csharp
public class LoggingRateProxy : DispatchProxy
{
    private readonly List<string> invokedMembers = [];

    public IRateCalculator Target { get; set; } = null!;

    public IReadOnlyList<string> InvokedMembers => invokedMembers;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        invokedMembers.Add(targetMethod.Name);
        return targetMethod.Invoke(Target, args);
    }
}
```

Every call through the proxy lands in `Invoke`. `targetMethod` is the interface member the caller invoked, here `IRateCalculator.Quote`, and `args` holds its arguments. The override records the name and runs the same method on `Target`. It reads the method generically, so the same logic could front `ICarrierRateGateway`.

`DispatchProxy.Create` needs a public parameterless constructor on the proxy type, so `Target` is a settable property instead of a constructor parameter:

{% raw %}
```csharp
public static class RateProxyFactory
{
    public static IRateCalculator Wrap(IRateCalculator target, out IReadOnlyList<string> invokedMembers)
    {
        var proxy = DispatchProxy.Create<IRateCalculator, LoggingRateProxy>();
        var loggingProxy = (LoggingRateProxy)proxy;
        loggingProxy.Target = target;

        invokedMembers = loggingProxy.InvokedMembers;
        return proxy;
    }
}
```
{% endraw %}

`Create` generates an object at runtime that implements `IRateCalculator` and routes every call through `Invoke`; no class implements `Quote` by hand. `Wrap` casts back to `LoggingRateProxy` only to set `Target`. The proxied `Quote` returns the same result as the target, records one `"Quote"` per call, and is just as transparent in front of a full `TaxRateDecorator` chain as in front of a bare calculator.

This is what a reader might expect an attribute to do. A `[Loggable]` method logs nothing until a source generator, a reflection-based interceptor or a `DispatchProxy` subclass reads the attribute and runs code. `LoggingRateProxy.Invoke` runs because `Create` built an object that routes through it.

{% include excalidraw.html name="19-di-decoration-and-dispatchproxy" caption="Figure 19.2: A DI registration resolves the fully decorated rate calculator from one factory, with no decorator named at any call site. A DispatchProxy subclass intercepts every call, records it, and forwards it to the real target." %}

## When not to use it

- **Decorator.** Skip it when there's one adjustment that never comes off and never gets a neighbor. A jurisdiction with one tax rate and no discount or surcharge needs one `Quote` that multiplies by `1m + taxRate`. The tell is every call site building the same nesting in the same order: one fixed computation spread over three classes.
- **Proxy.** Skip it when there's no cost, latency or access concern to interpose. `CachingRateGatewayProxy` pays off because the gateway stands in for a network round trip. Wrapping `BaseRateCalculator`, whose `Quote` is one multiplication, adds a dictionary, a branch and a type for nothing.

<blockquote class="ai-sidebar" markdown="1">
**Specifying a decorator.** "Add a discount to the rate" doesn't say whether it's a removable layer, which order the layers run in or where the wrapping happens. State those in the prompt:

> Add each pricing step as a `sealed` class implementing `IRateCalculator` that takes the inner calculator through its primary constructor, as `DiscountRateDecorator`, `SurchargeRateDecorator` and `TaxRateDecorator` do. Compose them in `AddRatePipeline` in `ServiceCollectionExtensions.cs` as discount, then surcharge, then tax, and register `BaseRateCalculator` on its own. Cache carrier rates in `CachingRateGatewayProxy` around `ICarrierRateGateway`, keyed on `RateRequest`. Use `DispatchProxy`, as `LoggingRateProxy` does, only when one interception applies to any interface.

Run `dotnet test --project RateComposition.Tests --filter DecoratorChainTests`; `CachingRateGatewayProxyTests` must still pass.
</blockquote>

## Build, run, observe

```sh
cd examples/19-decorator-and-proxy
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. Every type is a pure, synchronous computation. All 14 tests pass, covering the chain arithmetic (510.30, 486.00 without the surcharge, 400.00 for one discount layer), the container-resolved pipeline, the caching proxy against both an NSubstitute double and the real slow gateway, and the `DispatchProxy` wrapper.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Decorator and Proxy, including the distinction between adding a responsibility and controlling access to an object.
- Microsoft Learn, [`DispatchProxy`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy) and [`DispatchProxy.Create`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy.create): the parameterless-constructor requirement and the `Invoke(MethodInfo?, object?[]?)` override.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview): registering a service through a factory delegate, as `AddRatePipeline` does.
- Microsoft Learn, [Attributes](https://learn.microsoft.com/en-us/dotnet/standard/attributes/): what an attribute is and isn't at runtime.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
