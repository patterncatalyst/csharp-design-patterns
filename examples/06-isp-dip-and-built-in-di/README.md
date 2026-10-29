# 06-isp-dip-and-built-in-di

`ICarrierGateway` behind the built-in `Microsoft.Extensions.DependencyInjection`
container: an Interface Segregation split into `ICarrierRates` and
`ICarrierTracking`, and a Dependency Inversion composition root,
`AddCarrierGateway`, that wires either `RestCarrierGateway` (the production
stand-in) or `FakeCarrierGateway` (a deterministic sandbox stand-in) behind
those abstractions.

**What the tests prove.** `CarrierGatewayRegistrationTests` resolves
`ICarrierGateway` from a built container and asserts it returns a priced rate
quote and a bookable, trackable receipt, and that the narrow interfaces
(`ICarrierRates`, `ICarrierTracking`) resolve to the same registered instance.
`CarrierGatewaySwapTests` builds two containers that differ only in which
gateway type `AddCarrierGateway<TGateway>` registers, then resolves the same
unmodified `ShipmentRateAdvisor` and `ShipmentBookingService` from each: the
resolved quote text and tracking-number format differ, proving the swap
happened at the registration, not in any consumer. `ShipmentRateAdvisorTests`
builds `ShipmentRateAdvisor` directly against an NSubstitute substitute of
`ICarrierRates` alone, with no stub for booking or tracking, proving the ISP
split keeps that consumer's test double narrow.

Shipping domain, no containers: `RestCarrierGateway` stands in for a real
carrier's REST API (a per-destination surcharge plus a per-kilogram rate, an
in-memory booking ledger) without making a network call.

## Build and test

```sh
cd examples/06-isp-dip-and-built-in-di
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
