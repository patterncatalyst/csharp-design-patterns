# 19-decorator-and-proxy

Decorator and Proxy, both implementing the same interface as the object they
wrap, and both composing a `RateRequest` into a priced result without the
caller ever branching on which layer it is holding.

- **Decorator.** `IRateCalculator` is implemented by `BaseRateCalculator`, a
  flat per-kilogram rate, and by three decorators that each wrap another
  `IRateCalculator`: `DiscountRateDecorator` reduces the inner quote,
  `SurchargeRateDecorator` adds to it, and `TaxRateDecorator` adds tax on top
  of whatever the inner calculator already returned. The canonical chain
  wraps them discount, then surcharge, then tax outermost, so tax is computed
  on the already-discounted, already-surcharged amount. Each layer composes
  with any other `IRateCalculator`, including another decorator, which is why
  removing or reordering a layer changes the result in a predictable way
  instead of requiring a different class.
- **Caching Proxy.** `ICarrierRateGateway` is implemented by
  `SlowCarrierRateGateway`, which counts its own calls to make an expensive
  lookup observable without a real network dependency, and by
  `CachingRateGatewayProxy`, which wraps an `ICarrierRateGateway` and answers
  a repeated request for an equal `RateRequest` from a dictionary instead of
  calling the wrapped gateway again. A caller holding an
  `ICarrierRateGateway` cannot tell which one it has.
- **DI decoration.** `ServiceCollectionExtensions.AddRatePipeline` is the one
  place that lists the decorator chain: it registers `BaseRateCalculator` and
  then registers `IRateCalculator` as a factory that wraps it in
  `DiscountRateDecorator`, `SurchargeRateDecorator`, and `TaxRateDecorator`.
  Resolving `IRateCalculator` from the built-in container always returns the
  complete chain; no consumer assembles the layers itself.
- **Runtime Proxy with `DispatchProxy`.** `LoggingRateProxy` intercepts every
  call made through an `IRateCalculator` reference built by
  `DispatchProxy.Create`, records the invoked member's name, and forwards the
  call to the real target. `DispatchProxy.Create` requires a public
  parameterless constructor on the proxy type, so the target is assigned
  afterward through a settable `Target` property rather than through a
  constructor parameter; `RateProxyFactory.Wrap` is the one place that does
  this wiring and hands back both the proxy and its recorded member names.

**What the tests prove.** `DecoratorChainTests` builds the full
discount-surcharge-tax chain over `BaseRateCalculator` and asserts the quote
against a hand-computed expected value, then shows that dropping the
surcharge layer, or using a single layer alone, changes the result
predictably, and that the bare base calculator differs from the decorated
chain. `CachingRateGatewayProxyTests` wraps both an NSubstitute double and a
real `SlowCarrierRateGateway` in `CachingRateGatewayProxy` and asserts a
second call for an equal request returns the cached value while the wrapped
gateway is called exactly once, a different request still reaches the
wrapped gateway, and the cached value matches what the wrapped gateway would
have returned directly. `ServiceCollectionExtensionsTests` builds a real
`ServiceProvider` from `AddRatePipeline()` and asserts the resolved
`IRateCalculator` is the outermost `TaxRateDecorator`, not the bare
`BaseRateCalculator`, and that it quotes exactly like the hand-built chain.
`LoggingRateProxyTests` wraps a real `IRateCalculator` with
`RateProxyFactory.Wrap`, asserts the proxy's result matches the target's
result, and asserts the proxy recorded the `Quote` member name once per call,
including when the wrapped target is itself a decorator chain.

Shipping domain, no containers: `Money` is a validating value type, and every
calculator, decorator, and gateway is a pure, synchronous computation. A call
counter on `SlowCarrierRateGateway` stands in for network latency, so the
caching proxy's behavior is observable without a real clock or network
dependency to fake out.

## Build and test

```sh
cd examples/19-decorator-and-proxy
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
