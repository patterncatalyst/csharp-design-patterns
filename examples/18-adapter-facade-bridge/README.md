# 18-adapter-facade-bridge

Three structural patterns over one shipping domain: carrier integration,
booking orchestration, and label printing, plus the conversion operators
that make the value types in all three feel built-in.

- **Adapter.** Three carriers, three genuinely different native shapes, one
  target interface, `ICarrierGateway`. `FastlineSoapClient.SubmitQuote`
  takes plain strings and an integer gram weight and returns a
  `FastlineQuoteResponse`, the shape a generated SOAP client would produce.
  `SeabrightRestClient.RequestRate` takes a `SeabrightRateRequest` DTO and
  returns a `SeabrightQuoteDto`, the shape a REST client would produce.
  `AircoLegacyClient.Quote` takes and returns a bare `string[]`, a flat-file
  integration style that predates DTOs entirely. `FastlineGatewayAdapter`,
  `SeabrightGatewayAdapter`, and `AircoGatewayAdapter` each wrap one of these
  clients and implement `ICarrierGateway`, translating units and shapes so a
  caller holding an `ICarrierGateway` never needs to know which carrier, or
  which era of integration style, is behind it.
- **Facade.** `ShipmentBookingFacade.Book` is the one call a booking screen
  needs: it validates the request through `IAddressValidator`, prices it
  through an `ICarrierGateway`, and files the customs declaration through
  `ICustomsFiler`, returning a single `ShipmentConfirmation`. A bad address
  makes `AddressValidator` throw `AddressValidationException`, and the
  facade surfaces that failure rather than swallowing it; quoting and filing
  never run for a request that fails validation.
- **Bridge.** `ShipmentLabel` is the abstraction: it knows what data a label
  carries but renders it through whichever `ILabelRenderer` it was built
  with. `StandardShipmentLabel` and `ReturnShipmentLabel` are refined
  abstractions; `PdfLabelRenderer` and `ZplLabelRenderer` are the two
  implementors. Any label kind works with any renderer because the two
  hierarchies vary independently: adding a third label kind needs no new
  renderer, and adding a third renderer needs no new label kind.
- **Conversion operators.** `Money` stores cents and widens to `decimal`
  dollars implicitly (lossless) while narrowing from `decimal` back to
  `Money` explicitly (rounds to the nearest cent); each operator has a
  named alternate, `ToDecimal()` and `FromDecimal()`. `Kilograms` and
  `Pounds` convert to each other only explicitly, since the conversion
  factor is not exact in decimal; `ToPounds()`/`ToKilograms()` are the named
  alternates `AircoGatewayAdapter` uses to talk to a carrier that bills in
  pounds while every other type in the example works in kilograms.

**What the tests prove.** `CarrierGatewayAdapterTests` calls each fake native
client directly with the same inputs its adapter uses, then asserts the
adapter's `CarrierQuote` matches a transformation of that native answer,
proving the mapping rather than a hardcoded number. `ShipmentBookingFacadeTests`
substitutes all three collaborators and asserts `Book` calls them in order,
returns a confirmation built from their results, and that a validator
exception stops the gateway and filer from ever being called; a real
`AddressValidator` test proves the exception path end to end. `ShipmentLabelTests`
is a `Theory` over all four label-kind/renderer combinations, asserting each
renders the expected title, carrier code, address, and format marker.
`ConversionOperatorTests` proves `Money`'s implicit widening needs no cast and
agrees with `ToDecimal()`, that the explicit narrowing needs a cast and
agrees with `FromDecimal()`, and that `Kilograms`/`Pounds` round-trip within
a small tolerance through their explicit operators.

Shipping domain, no containers: every client in this example is a
deterministic, in-memory fake of a carrier integration, not a real network
call.

## Build and test

```sh
cd examples/18-adapter-facade-bridge
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
