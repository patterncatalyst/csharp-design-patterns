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

- Explain Decorator as a chain of types that each implement the same
  interface as the thing they wrap, so a chain of any length is itself a
  drop-in implementation of that interface, and build the canonical
  discount-surcharge-tax chain over `IRateCalculator`.
- Trace a worked rate through that chain and explain why the order the
  layers are nested in changes the arithmetic, not just the code.
- Register a fully decorated service from a factory lambda,
  `AddSingleton<IRateCalculator>(sp => ...)`, so a consumer resolving
  `IRateCalculator` from the container always gets the complete chain
  without assembling a single layer itself.
- Build a caching Proxy, `CachingRateGatewayProxy`, that implements the
  same interface as the collaborator it wraps and calls that collaborator
  only once per distinct request, and state how that differs in intent
  from a Decorator even though the shape looks identical.
- Build a dynamic proxy with `DispatchProxy`, explain what
  `Invoke(MethodInfo?, object?[]?)` does with the call it intercepts, and
  tell a plain C# attribute apart from either kind of Proxy.

## Why this matters

A freight rate rarely leaves a pricing pipeline as the one number a base
tariff computed. A 100-kilogram shipment at 5.00 per kilogram starts at
500.00, then a volume discount takes ten percent off, then a fuel
surcharge adds five percent on top of the discounted figure, then tax adds
eight percent on top of that. Each of those three adjustments is a
business rule that changes on its own schedule: a discount tier gets
renegotiated, a surcharge rate moves with fuel prices, a tax rate changes
when a jurisdiction does. None of the three has anything to do with how
the other two are computed, and a system that needs to add a fourth
adjustment, or drop one for a specific lane, should not have to touch the
two it leaves alone to do either.

The direct way to write this prices a shipment in one method: compute the
base amount, then multiply by `1 - discountRate`, then by
`1 + surchargeRate`, then by `1 + taxRate`, all four numbers and three
operations sitting in the same function body. That function prices a
shipment correctly, and it keeps pricing correctly until the day a lane
needs tax but not a surcharge, or a promotional run needs a discount
without the two adjustments after it. Removing the surcharge now means
editing the method that also computes the discount and the tax, re-reading
arithmetic that has nothing to do with the change at hand, and re-running
every test the method has because no single adjustment can be tested or
reused on its own. A caching layer in front of a slow carrier lookup has
the same problem from a different angle: adding a dictionary and a cache
check directly inside the class that already does the real network call
mixes two concerns that change for entirely different reasons, how a rate
is actually fetched and whether a fetched rate is worth remembering, into
one class that now has to be read, tested, and changed as a single unit
whenever either concern moves.

Decorator and Proxy both answer this by wrapping rather than editing. A
Decorator wraps an existing implementation of an interface in a new
implementation of the identical interface, adds exactly one thing, and
hands back something a caller cannot tell apart from the thing it wrapped
except by what the extra behavior does to the result. Stack three
decorators and each one still implements `IRateCalculator`, so removing
the middle layer means deleting one constructor argument, not rewriting an
arithmetic expression buried in a shared method. A Proxy uses the identical
shape, a class implementing the same interface as the thing it wraps and
holding a reference to it, but for a different reason: not to add a new
responsibility a caller asked for, but to control how and when the wrapped
object actually gets called, a cache in front of an expensive lookup, or
an interceptor that records every call a reference makes before letting
it through. Both patterns let a caller hold one interface reference and
never know, or need to know, how many layers or what kind of indirection
sits behind it.

{% include excalidraw.html name="19-decorator-and-proxy" caption="Figure 19.1: BaseRateCalculator is wrapped by DiscountRateDecorator, SurchargeRateDecorator, and TaxRateDecorator in that order, each implementing the same IRateCalculator the layer underneath it does; CachingRateGatewayProxy implements the identical ICarrierRateGateway as the SlowCarrierRateGateway it wraps and answers a repeated request from a dictionary instead of calling it again; and a LoggingRateProxy built by DispatchProxy.Create intercepts every call made through its IRateCalculator reference, records the invoked member, and forwards it to the real target." %}

## Decorator: layering rate adjustments without touching the others

The example behind this chapter lives in
`examples/19-decorator-and-proxy`. Every rate calculation in it, base or
decorated, implements the same two-type contract: a `RateRequest` goes in
and a `Money` comes out.

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

`Money` validates on every construction path, so a quote can never end up
negative no matter how many layers computed it, and `Money.Of` is the
call-site sugar every calculator and decorator in the example reaches for
instead of the positional constructor directly. `BaseRateCalculator` is
the innermost implementation, a flat per-kilogram rate with nothing layered
on yet:

```csharp
public sealed class BaseRateCalculator(decimal ratePerKilogram) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(request.Kilograms * ratePerKilogram);
}
```

Each decorator takes an `IRateCalculator` to wrap and implements the exact
same interface itself, so a decorated calculator is never distinguishable
from a bare one by its type signature, only by what `Quote` returns.
`DiscountRateDecorator` asks its inner calculator for a quote and reduces
it by a fractional rate before returning:

```csharp
public sealed class DiscountRateDecorator(IRateCalculator inner, decimal rate) : IRateCalculator
{
    public Money Quote(RateRequest request) => Money.Of(inner.Quote(request).Amount * (1m - rate));
}
```

`SurchargeRateDecorator` and `TaxRateDecorator` are the same shape in the
opposite direction, each multiplying the inner quote by `1m + rate`
instead of `1m - rate`. Nothing about any one of the three decorators
knows the other two exist. `SurchargeRateDecorator` never checks whether
its `inner` is a `BaseRateCalculator`, a `DiscountRateDecorator`, or
another `SurchargeRateDecorator` stacked on top of a discount; it only
needs an `IRateCalculator` to call, which every one of those types already
is. That is the entire mechanism this pattern depends on: because every
layer, base and decorator alike, implements the same interface, any chain
built from them is itself a complete, callable `IRateCalculator`, and a
caller holding the outermost layer never has to know how many are
underneath it.

Stacking the three decorators in a fixed order produces the canonical rate
pipeline this example tests against:

```csharp
var calculator = new TaxRateDecorator(
    new SurchargeRateDecorator(
        new DiscountRateDecorator(new BaseRateCalculator(5.00m), rate: 0.10m),
        rate: 0.05m),
    rate: 0.08m);

var quote = calculator.Quote(new RateRequest("Rotterdam", "Santos", 100m));
```

Reading that chain from the inside out traces the same arithmetic a reader
would compute by hand. A 100-kilogram shipment at 5.00 per kilogram starts
at 500.00. `DiscountRateDecorator` at a 0.10 rate takes that down to
450.00. `SurchargeRateDecorator` at a 0.05 rate pushes 450.00 up to
472.50. `TaxRateDecorator` at a 0.08 rate takes the final step from 472.50
up to 510.30, and `DecoratorChainTests.The_full_chain_applies_discount_then_surcharge_then_tax_in_order`
asserts `calculator.Quote(request)` equals exactly `Money.Of(510.30m)`.
The nesting order is the computation order: tax is nested outermost
because the pipeline wants tax computed on the already-discounted,
already-surcharged amount, not on the bare 500.00 base, and swapping the
nesting would change the final figure even with the identical three rates.
`Removing_the_surcharge_layer_yields_a_different_predictable_result`
drops `SurchargeRateDecorator` from the same chain, applying tax directly
to the discounted 450.00 instead of to 472.50, and lands on 486.00, a
different, equally predictable number reached by deleting exactly one
constructor call and nothing else. `A_single_decorator_layer_is_itself_a_complete_IRateCalculator`
wraps `BaseRateCalculator` in nothing but a 0.20 `DiscountRateDecorator`
and quotes 400.00 on the same request, which is the same interface,
`IRateCalculator`, answering the same `Quote` call with a chain one layer
deep instead of three. `The_undecorated_base_calculator_differs_from_the_fully_decorated_chain`
confirms the obvious but necessary other half: calling `Quote` on the bare
`baseCalculator` next to the fully wrapped chain, over the identical
request, returns two different `Money` values, proving the decorators are
actually doing something rather than passing the request through
unchanged.

A plain C# attribute looks related to this at first glance, a small
annotation sitting next to a method that seems to add behavior around it,
but the resemblance stops at the syntax. An attribute like `[Obsolete]` or
a custom `[Cacheable]` attached to a method is inert metadata the runtime
stores alongside the method; nothing executes because an attribute is
present unless some other piece of code goes looking for it through
reflection and acts on what it finds. Decorator, by contrast, is ordinary
object composition: `TaxRateDecorator` runs because a caller constructed
it and called `Quote` on it, the same way any other object runs its own
methods. An attribute cannot wrap a call the way `TaxRateDecorator` wraps
`SurchargeRateDecorator`, because an attribute is not an object sitting
between a caller and a target at runtime; it is a label a caller or a
framework has to choose to interpret. The mechanism that actually can
intercept a call the way a hand-written Decorator does without a reader
seeing the wrapping in the constructor chain is `DispatchProxy`, covered
later in this chapter, and the two are easy to conflate precisely because
both end up producing code that runs "around" a method a reader did not
write by hand.

## DI-based decoration: resolving the whole chain from the container

Every example above constructs the chain by hand, which is exactly right
for a unit test that wants to assert against a known arithmetic sequence,
and exactly wrong for a real service that should not scatter
`new TaxRateDecorator(new SurchargeRateDecorator(...))` across every call
site that needs a priced rate. `ServiceCollectionExtensions.AddRatePipeline`
is the one place that lists the chain, registering the bare
`BaseRateCalculator` and then registering `IRateCalculator` itself as a
factory that wraps the resolved base in all three decorators:

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

The second registration is the one worth reading closely. It registers
`IRateCalculator`, not `TaxRateDecorator`, so every consumer in the
application that asks the container for an `IRateCalculator` resolves the
same fully wrapped chain through the identical factory, with no decorator
type named anywhere outside this one extension method. The factory
delegate itself resolves `BaseRateCalculator` from the same provider,
through `GetRequiredService<BaseRateCalculator>()`, rather than
constructing a second, independent base calculator, so the base registered
as its own service and the base feeding the decorated chain are the same
singleton instance. `AddRatePipeline_resolves_a_fully_wrapped_calculator_matching_the_hand_built_chain`
builds a `ServiceCollection`, calls `AddRatePipeline()`, resolves
`IRateCalculator` from the built provider, and asserts its quote on a
100-kilogram Rotterdam-to-Santos request matches the identical hand-built
chain's quote exactly, meaning the container-resolved pipeline and the
one assembled directly in test code price every request identically.
`AddRatePipeline_resolved_calculator_is_the_outermost_tax_decorator_not_the_bare_base`
asserts the resolved `IRateCalculator` is a `TaxRateDecorator` with
`Assert.IsType`, and is not a `BaseRateCalculator` with `Assert.IsNotType`,
confirming a consumer resolving the interface type gets the complete chain
rather than accidentally resolving the innermost layer the container also
happens to know about. `AddRatePipeline_registers_the_base_calculator_as_its_own_resolvable_service_too`
resolves both `BaseRateCalculator` and `IRateCalculator` from the same
provider and asserts their quotes on the same request differ, which is
the whole point of registering both: a consumer that needs the
unadorned rate, a reporting job that wants to see the base figure before
any adjustment, can still ask for `BaseRateCalculator` directly, while
every other consumer asking for `IRateCalculator` gets the adjusted
figure without having to know discount, surcharge, and tax exist as
separate types at all.

This is decoration moved from a constructor call a developer writes by
hand to a composition root a container runs once, at startup, and the
shape of the decorator chain itself does not change between the two; only
who assembles it and when does. Adding a fourth adjustment later, a
seasonal peak surcharge, means adding one more layer to the lambda in
`AddRatePipeline` and nothing else; no consumer that already resolves
`IRateCalculator` has to change, because the interface it depends on never
grew a new member and the factory producing it is the only place aware
that a fourth decorator now exists.

## Proxy: a caching gateway with the same interface as the real one

A caching Proxy wears the identical clothing as a Decorator, a class
implementing the same interface as the thing it wraps and holding a
reference to it, built around a different collaborator entirely in this
example: `ICarrierRateGateway`, which looks up a carrier's quoted rate and
transit time for a shipment rather than computing a tariff.

```csharp
public sealed record CarrierRate(Money Amount, int TransitDays);

public interface ICarrierRateGateway
{
    CarrierRate GetRate(RateRequest request);
}
```

`SlowCarrierRateGateway` stands in for a real carrier integration
expensive enough to be worth not calling twice for the same question,
counting its own invocations in a public `Calls` property so the cost of
calling it is something a test can observe directly instead of trusting a
comment:

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

`CachingRateGatewayProxy` wraps any `ICarrierRateGateway`, including
`SlowCarrierRateGateway`, and implements the identical interface:

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

`RateRequest` is a record, so two separately constructed requests carrying
the same origin, destination, and weight are equal to each other under the
default record equality the compiler generates, which is exactly what lets
`Dictionary<RateRequest, CarrierRate>` treat two equal-but-distinct request
instances as the same cache key. `CachingRateGatewayProxyTests.A_second_call_with_an_equal_request_returns_the_cached_value_without_calling_the_inner_gateway_again`
substitutes `inner` with NSubstitute, programs it to return a fixed
`CarrierRate` for the shared request, calls `proxy.GetRate` once with that
exact request object and once with a second, separately constructed
`RateRequest` carrying the same three values, and asserts both calls
return the identical expected rate while `inner.Received(1).GetRate(Arg.Any<RateRequest>())`
confirms the substituted collaborator was invoked exactly once across both
calls. `The_slow_gateway_is_invoked_exactly_once_across_repeated_identical_requests`
runs the same check against the real `SlowCarrierRateGateway` instead of a
test double, calling `proxy.GetRate(Request)` three times in a row and
asserting `slow.Calls` equals one, which is the caching behavior proven
against the actual collaborator a production call site would use, not just
a substitute standing in for it. `A_different_request_misses_the_cache_and_calls_the_inner_gateway_again`
calls the proxy once with the shared request and once with a request to a
different destination, and asserts `slow.Calls` equals two, confirming the
cache keys on the full request rather than caching blindly after the
first call regardless of what was asked. `The_cached_value_matches_what_the_inner_gateway_would_have_returned_directly`
calls `slow.GetRate(Request)` directly and `proxy.GetRate(Request)`
through the cache, and asserts the two results are equal, which is the
guarantee a caching Proxy has to uphold above everything else: wrapping a
collaborator in a cache can change how many times it runs, but it can
never change what it answers.

The resemblance to Decorator is real and worth being precise about, not
blurring past. Both `TaxRateDecorator` and `CachingRateGatewayProxy`
implement the same interface they wrap and hold a reference to the thing
underneath. The difference is in what each one is for. A Decorator adds a
responsibility the caller actually wants reflected in the result: a tax
amount that was not there before, a discount that changes the number a
caller receives. A Proxy changes nothing about what the result means to
the caller; `CachingRateGatewayProxy.GetRate` returns the exact
`CarrierRate` the wrapped gateway would have returned, proven directly by
`The_cached_value_matches_what_the_inner_gateway_would_have_returned_directly`,
and the only thing a caller could notice from outside is how long the call
took or whether `SlowCarrierRateGateway.Calls` went up. A Decorator exists
to be seen in the output; a Proxy exists to be invisible in the output and
only observable in cost.

## DispatchProxy: intercepting a call without writing a wrapper class per interface

`CachingRateGatewayProxy` is a Proxy written by hand, one class, one
interface, one piece of behavior to interpose. `DispatchProxy` builds a
different kind of Proxy, one where the forwarding and interception logic
is written once and reused against whatever interface a caller asks it to
stand in front of. `LoggingRateProxy` is that reusable logic for this
example, recording every member a caller invokes through it and forwarding
the call on to a real target:

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

`Invoke` is the one method `DispatchProxy` asks a subclass to override,
and every call made through a proxy built from this type lands here
first. `targetMethod` identifies which member of the proxied interface the
caller actually invoked, as a `MethodInfo` reflecting `IRateCalculator`'s
own `Quote` method, and `args` carries the arguments that call was made
with. `LoggingRateProxy.Invoke` records `targetMethod.Name`, appending
`"Quote"` to its own list, and then calls `targetMethod.Invoke(Target, args)`,
running the exact same method against the real `Target` and returning
whatever that call returns. Nothing about `LoggingRateProxy` knows it is
specifically wrapping an `IRateCalculator`; the same class could sit in
front of `ICarrierRateGateway` or any other interface, because `Invoke`
reads the method and its arguments generically off whatever call arrived,
rather than declaring a `Quote` method of its own that only works for one
interface.

Building one of these proxies goes through `DispatchProxy.Create`, which
needs a public parameterless constructor on the proxy type to do its own
work, ruling out assigning `Target` through a constructor parameter the
way every other type in this example does:

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

`DispatchProxy.Create<IRateCalculator, LoggingRateProxy>()` generates an
object at runtime that implements `IRateCalculator` and routes every call
made on it through `LoggingRateProxy.Invoke`, without a hand-written class
anywhere implementing `IRateCalculator.Quote` directly. `RateProxyFactory.Wrap`
is the one place that casts the result back to the concrete
`LoggingRateProxy` long enough to set `Target`, then hands the caller back
an `IRateCalculator` reference and a separate, read-only view onto the
member names that reference has recorded. `LoggingRateProxyTests.The_proxy_forwards_Quote_to_the_target_and_returns_the_same_result`
wraps a `BaseRateCalculator` with `RateProxyFactory.Wrap`, calls `Quote`
through the returned proxy, and asserts the result equals calling `Quote`
on the unwrapped target directly, while `Assert.Contains(nameof(IRateCalculator.Quote), invokedMembers)`
confirms the proxy actually intercepted the call rather than having handed
the caller the real target by mistake. `The_proxy_records_one_invocation_per_call_made_through_it`
calls `proxy.Quote(Request)` twice and asserts `invokedMembers.Count`
equals two, with every recorded entry equal to `"Quote"`, proving the log
grows by exactly one entry per call rather than batching or
deduplicating. `The_proxy_forwards_to_a_decorated_target_just_as_faithfully_as_a_bare_one`
wraps an already-decorated `TaxRateDecorator` chain instead of a bare
`BaseRateCalculator`, confirming `LoggingRateProxy` is just as transparent
in front of a multi-layer chain as it is in front of the innermost
calculator, which is the same interface guarantee Decorator and the
caching Proxy both depend on applied one more time: a `DispatchProxy`
wrapping an `IRateCalculator` is itself an `IRateCalculator`, indifferent
to how many layers sit behind the reference it was handed.

This is the mechanism that actually does what a reader might mistakenly
expect a plain attribute to do on its own. Decorating a method with
`[Loggable]` and expecting calls to that method to start showing up in a
log requires something to read that attribute and generate or run
interception code in response, whether that is a source generator, a
reflection-based interceptor, or exactly the `DispatchProxy` subclass this
example builds by hand. The attribute alone is a label; `LoggingRateProxy.Invoke`
is the code that actually runs between a caller and a target, and it runs
because `DispatchProxy.Create` built an object that routes through it, not
because any method in the example carries a custom attribute at all.

## Build, run, observe

Every calculator, decorator, gateway, and proxy in this example is a pure,
synchronous computation, so the suite needs nothing beyond the local .NET
10 SDK. The example is standalone-testable from its own directory:

```sh
cd examples/19-decorator-and-proxy
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
  duration: 1s 626ms
```

The 14 cases split across what this chapter covers. `DecoratorChainTests`
proves the Decorator headline: the full discount-surcharge-tax chain
prices a 100-kilogram shipment at 510.30, dropping the surcharge layer
alone lands at 486.00 instead, a single discount layer is itself a
complete `IRateCalculator`, and the bare base calculator differs from the
fully decorated chain on the identical request.
`ServiceCollectionExtensionsTests` proves the DI-decoration half: a
container configured with `AddRatePipeline` resolves an `IRateCalculator`
that is concretely a `TaxRateDecorator`, prices identically to the hand-
built chain, and differs from the separately resolvable
`BaseRateCalculator`. `CachingRateGatewayProxyTests` proves the caching
Proxy: an NSubstitute double confirms the wrapped gateway is called
exactly once across two equal requests, the real `SlowCarrierRateGateway`
confirms the same thing without a test double in the way, a different
request still reaches the wrapped gateway, and the cached value always
matches what the wrapped gateway would have answered directly.
`LoggingRateProxyTests` proves the `DispatchProxy` half: a proxy built by
`RateProxyFactory.Wrap` forwards `Quote` to its target and returns the
identical result, records one `"Quote"` entry per call made through it,
and stays just as transparent in front of a decorated chain as in front of
a bare calculator.

## When not to use it

A Decorator chain earns the interface and the extra class per layer only
when more than one layer is actually possible, or when a layer needs to
come off without touching the ones around it. A single adjustment that
every shipment always gets, a jurisdiction with exactly one tax rate and
no discount or surcharge ever applied, does not need `TaxRateDecorator`
wrapping `BaseRateCalculator` at all; a calculator that multiplies by
`1m + taxRate` directly inside its own `Quote` method says the same thing
without an extra interface implementation and an extra constructor
argument standing in for a layer that will never be removed, swapped, or
joined by a second one. The tell is a chain where every call site
constructs the identical nesting in the identical order with no
variation anywhere in the codebase; at that point the "chain" is one fixed
computation wearing three classes instead of one expression.

A Proxy is the wrong reach when there is no real cost, latency, or access
concern to interpose between a caller and the thing it is calling.
Wrapping an already-cheap, already-synchronous calculation in a caching
Proxy adds a dictionary, a cache-miss branch, and a second type
implementing the same interface, for a lookup that was never expensive
enough to justify remembering its answer. `CachingRateGatewayProxy` pays
for itself specifically because `SlowCarrierRateGateway` stands in for a
real cost, a network round trip to a carrier; a `BaseRateCalculator`,
whose entire `Quote` method is one multiplication, would get nothing from
being wrapped the same way, because there is no repeated expense a cache
could avoid.

> **AI sidebar.** Asked to make a single pricing adjustment "flexible" or
> "pluggable," an AI pairing partner will sometimes produce a
> `DiscountRateDecorator` wrapping a `BaseRateCalculator` for an amount
> that one method could compute directly, treating Decorator as the
> default answer to "this value changes the quote" even when no second
> layer, no removable layer, and no second implementation are in the
> picture anywhere in the prompt. A related suggestion reaches for a
> hand-rolled `DispatchProxy` subclass, or a reflection-based interceptor
> built from scratch, to log or time a single method on a single class
> with one call site, where a plain wrapper class implementing the same
> interface directly, with no `MethodInfo` or reflection anywhere, reads
> the same intent in fewer lines and without the indirection a reader has
> to trace through `Invoke` to understand. Check a generated answer
> against the same two questions this chapter asks directly: is there a
> second layer, a removable layer, or a second implementation that
> actually exists or is clearly coming, which is what Decorator is for,
> and is there a real crosscutting concern, a cache, a log, a metric,
> that needs to sit between a caller and a target without either one
> changing, which is what a Proxy, hand-written or built through
> `DispatchProxy`, is for. A single fixed adjustment or a single
> interception point with one implementation and one call site answers no
> to both, and a plain method or a plain wrapper class says everything the
> generated Decorator or dynamic proxy would have said.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Decorator and Proxy, including the distinction between adding a responsibility and controlling access to an object this chapter keeps separate even where the two patterns share a shape.
- Microsoft Learn, [`DispatchProxy`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy) and [`DispatchProxy.Create`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy.create): the authority for the parameterless-constructor requirement and the `Invoke(MethodInfo?, object?[]?)` override `LoggingRateProxy` relies on.
- Microsoft Learn, [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview): the authority for registering a service through a factory delegate, the mechanism `AddRatePipeline` uses to resolve a fully decorated `IRateCalculator` from the container.
- Microsoft Learn, [Attributes](https://learn.microsoft.com/en-us/dotnet/standard/attributes/): the authority for what an attribute is and is not at runtime, the basis for this chapter's distinction between a declarative attribute and the active interception `DispatchProxy` performs.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
