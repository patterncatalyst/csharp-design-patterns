# 15-factory-method-and-abstract-factory

Factory Method and Abstract Factory over the same problem: dispatching a
`ShipmentRequest` through one of three carriers, Fastline, Seabright, and
Airco, each with its own gateway, request builder, and response parser.

- **Carrier keys, reified.** `ICarrier` is a marker interface with a
  `static abstract string Code` member, so it carries real content instead
  of standing in as an empty tag. `Fastline`, `Seabright`, and `Airco` are
  sealed, never-instantiated types that implement it. Every generic type
  parameter constrained to `ICarrier` is a compile-time guarantee that a
  carrier's gateway, builder, and parser all belong to the same family.
- **Abstract Factory.** `ICarrierGatewayFactory<TCarrier>` produces a
  matched family of three products, `ICarrierGateway`,
  `IShipmentRequestBuilder`, and `IShipmentResponseParser`, so a caller can
  never mix Fastline's builder with Seabright's gateway. `FastlineGatewayFactory`,
  `SeabrightGatewayFactory`, and `AircoGatewayFactory` are the concrete
  families.
- **Type-keyed registry.** `CarrierRegistry` stores a factory per carrier in
  a `Dictionary<Type, object>` keyed by `typeof(TCarrier)`.
  `Register<TCarrier>` and `FactoryFor<TCarrier>` are reified generics in
  practice: the dictionary itself is untyped, but every caller of
  `FactoryFor<TCarrier>` gets back an already-cast
  `ICarrierGatewayFactory<TCarrier>`, not `object`. Adding Airco after
  Fastline and Seabright are already wired up costs one `Register<Airco>`
  call; `CarrierRegistry` itself never changes.
- **Factory Method.** `CarrierDispatcher` is the abstract template: it
  builds the carrier request, sends it through whatever
  `CreateGateway()` returns, and parses the response. `FastlineDispatcher`,
  `SeabrightDispatcher`, and `AircoDispatcher` override `CreateGateway()`
  and supply the matching builder and parser through the base constructor.
  `DelegateCarrierDispatcher` is the delegate alternative: the same
  build-send-parse template, but the factory method is a constructor
  parameter, `Func<ICarrierGateway>`, instead of a subclass override.
- **End-to-end entry point.** `ShipmentDispatchService` resolves a carrier's
  whole family from a `CarrierRegistry` by `Dispatch<TCarrier>` and runs
  build, send, parse through it. The caller names only the carrier's
  reified key type, never a concrete gateway, builder, or parser.

**What the tests prove.** `CarrierRegistryTests` resolves the Fastline
family by `typeof(Fastline)` and asserts the returned factory's gateway,
builder, and parser are the Fastline-specific types; a theory across
Fastline, Seabright, and Airco shows each type key yields its own family
without cross-contamination; one `Register<Airco>` call resolves Airco with
no edit to `CarrierRegistry`; and a never-registered carrier throws
`UnknownCarrierException` from `FactoryFor<T>()`. `CarrierDispatcherTests`
shows `FastlineDispatcher.Dispatch` yields the expected
`ShipmentConfirmation`, and that `DelegateCarrierDispatcher` configured the
same way reaches an equal confirmation, the subclass and delegate forms are
genuinely interchangeable. `ShipmentDispatchServiceTests` dispatches a
shipment end to end for all three carriers through `CarrierRegistry`.
`RequestBuilderTests` proves the static-abstract reification: each
builder stamps `CarrierRequest.Carrier` from its carrier type's own `Code`,
never from a value the caller passes in.

Shipping domain, no containers: every gateway, builder, and parser is a
pure, synchronous computation, no network or I/O to fake out.

## Build and test

```sh
cd examples/15-factory-method-and-abstract-factory
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
