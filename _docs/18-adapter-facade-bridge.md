---
title: "Adapter, Facade, and Bridge"
order: 18
layout: tutorial
sectionid: docs
part: "composing-adapting-structure"
description: "The three structural adapters, plus C#'s implicit and explicit conversion operators."
---

<!-- EXAMPLE (shipping domain): Three carrier APIs behind one interface, with Money and Weight unit conversions via operators. -->

## Learning objectives

By the end of this chapter you can:

- Explain how Adapter reconciles a single target interface,
  `ICarrierGateway`, with three carrier clients whose native request and
  response shapes match neither the target nor each other, and write the
  three adapters that do it.
- Build a Facade, `ShipmentBookingFacade`, that sequences three subsystem
  calls, validate, quote, file, behind one `Book` call, and show why a
  validation failure stops the sequence before a quote is ever requested.
- Separate a Bridge abstraction, `ShipmentLabel`, from its implementor,
  `ILabelRenderer`, so a label kind and an output format vary independently
  without a class for every combination of the two.
- Write a C# user-defined conversion operator in both directions, implicit
  for a widening that never loses information and explicit for a narrowing
  or a unit change that can, and pair each operator with a named alternate
  method.
- Recognize when an Adapter, a Facade, or a Bridge is more machinery than a
  single carrier, a single step, or a single renderer needs, and when a
  conversion belongs in a named method instead of an operator.

## Why this matters

A shipment booking service needs to ask three different carriers for a
quote on the same shipment, and none of the three expose anything close to
the same API. Fastline is a SOAP-era client: it takes plain strings and an
integer weight in grams and answers with a typed response record carrying a
flat cent amount. Seabright is a REST client: it takes a request object
built from port names and a decimal kilogram weight and answers with a
response object priced in dollars. Airco predates both of them: it takes
and returns a bare array of strings in a fixed record layout with no named
fields at all, the kind of flat-file integration that DTOs and JSON both
exist to replace. Three carriers, three request shapes, three response
shapes, and a dispatch service that needs to call each one through the same
two lines of code, build a request and read back a quote, is the gap
Adapter closes before any of the mechanics below.

Once each carrier answers through one interface, booking a shipment is
still three separate calls a caller has to run in the right order: confirm
the origin and destination are addresses a carrier can actually deliver to
before asking any carrier to price something that can never ship, ask the
chosen carrier for a quote, and file the customs declaration a confirmed
quote requires. A caller that runs those steps out of order, quoting before
validating, or filing before a quote exists, produces a label, a charge, or
a customs record around a shipment nobody has confirmed is deliverable.
Facade collapses that three-call sequence into `Book`, one method that runs
validate, quote, file in that fixed order every time, and stops at the
first failure instead of letting a later step run against an invalid
shipment.

A shipping label carries its own two-axis problem. What a label says is a
question of shipment kind: a standard outbound label addresses the
destination with an instruction to deliver during business hours, a return
label addresses the origin with an instruction to send the package back on
refusal. What a label looks like on paper is a question of the printer
reading it: an office printer wants a PDF page, a thermal printer at a
loading dock wants ZPL, the field-delimited command language those printers
accept directly. Neither question depends on the other; a return label is
exactly as valid printed to PDF as to ZPL, and a PDF renderer has to handle
a standard label and a return label without caring which one it was handed.
Two label kinds and two renderers, built as one class per combination,
means four classes today and a fifth every time either axis grows by one.
Bridge keeps the two axes apart, so a label kind can be added without a
matching renderer and a renderer can be added without a matching label
kind.

The same three carriers raise a question with no analogue in object
orchestration: Fastline prices in cents, Seabright prices in dollars, and
Airco expects a weight in pounds where every other carrier in this chapter
speaks kilograms. Converting between those units correctly, and making it
visible at a call site when a conversion can lose information, is what C#'s
user-defined conversion operators are for. This chapter closes with the two
conversions the three adapters above already rely on: `Money` to and from
decimal dollars, and `Kilograms` to and from `Pounds`.

{% include excalidraw.html name="18-adapter-facade-bridge" caption="Figure 18.1: Three incompatible carrier clients, FastlineSoapClient, SeabrightRestClient, and AircoLegacyClient, each adapted to the shared ICarrierGateway interface by its own Adapter; ShipmentBookingFacade sequencing an IAddressValidator, that adapted ICarrierGateway, and an ICustomsFiler behind one Book call; and ShipmentLabel as the Bridge abstraction rendering through an independently chosen ILabelRenderer, PdfLabelRenderer or ZplLabelRenderer." %}

## Adapter: three carriers, one gateway

Every call in this chapter starts and ends at the same two shapes.
`ShipmentRequest` carries an origin, a destination, and a weight in
kilograms; `CarrierQuote` carries back a carrier code, a quoted price, and a
transit day count. `ICarrierGateway` is the single method a caller actually
calls, and nothing about that method hints that three wildly different wire
protocols sit behind it:

```csharp
public sealed record ShipmentRequest(string Origin, string Destination, Kilograms Weight);

public sealed record CarrierQuote(string CarrierCode, Money Amount, int TransitDays);

public interface ICarrierGateway
{
    CarrierQuote GetQuote(ShipmentRequest request);
}
```

`FastlineSoapClient` is the first carrier this interface has to hide, and
it already shows the pattern the other two repeat: a native call shaped
around what Fastline's own protocol expects, not around `ShipmentRequest`
or `CarrierQuote` at all. `SubmitQuote` takes an origin code, a destination
code, and a weight in grams, and answers with its own `FastlineQuoteResponse`
record carrying a cent amount and a day count. `FastlineGatewayAdapter` is
the translation between the two, and nothing more:

```csharp
public sealed class FastlineGatewayAdapter(FastlineSoapClient client) : ICarrierGateway
{
    public CarrierQuote GetQuote(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        int grams = (int)(request.Weight.Value * 1000m);
        FastlineQuoteResponse response = client.SubmitQuote(request.Origin, request.Destination, grams);

        return new CarrierQuote("FASTLINE", new Money(response.AmountCents), response.DaysInTransit);
    }
}
```

`GetQuote` does three things and nothing else: convert the kilogram weight
on the incoming request to the grams `SubmitQuote` expects, call
`SubmitQuote`, and wrap its `FastlineQuoteResponse` back into a
`CarrierQuote` under the fixed code `"FASTLINE"`. Fastline already prices in
cents, the same unit `Money` stores internally, so the amount passes
straight into `Money`'s constructor with no conversion operator in sight;
only the weight unit needed translating on the way out.
`CarrierGatewayAdapterTests.FastlineGatewayAdapter_maps_the_SOAP_response_to_a_carrier_quote`
calls the real `FastlineSoapClient` directly with the same request the
adapter under test receives, an ORD-to-DXB shipment at 5 kilograms, and
asserts the adapter's `CarrierQuote` agrees with what that client actually
returned, rather than with a value hardcoded against the adapter's own
arithmetic: the carrier code `"FASTLINE"`, a transit day count read straight
off the client, and an amount equal to the client's cent total converted to
dollars.

`SeabrightGatewayAdapter` and `AircoGatewayAdapter` adapt the same interface
against two more native shapes, and each does a different amount of
translation depending on how far its carrier's units sit from
`ShipmentRequest`'s. Seabright already works in kilograms, so
`SeabrightGatewayAdapter` only has to repackage a `ShipmentRequest` into a
`SeabrightRateRequest` built from port names and a kilogram weight, then
repackage the `SeabrightQuoteDto` that comes back; the one conversion it
needs is narrowing Seabright's decimal dollar rate into `Money`, done
through the named `Money.FromDecimal` method rather than a bare cast. Airco
needs the most translation of the three: `AircoGatewayAdapter` converts the
request's kilogram weight to pounds with an explicit `(Pounds)` cast before
building the three-element string array Airco's decades-old `Quote` method
expects, then parses that method's bare string reply back into a decimal
amount and an integer transit day count by hand, because nothing about a
`string[]` return value names which index holds which field.
`CarrierGatewayAdapterTests.SeabrightGatewayAdapter_maps_the_REST_response_to_a_carrier_quote`
and
`CarrierGatewayAdapterTests.AircoGatewayAdapter_maps_the_flat_file_response_to_a_carrier_quote`
run the identical proof against their own carriers: call the native client
directly, and check the adapter's `CarrierQuote` against what that call
returned.

What makes these three adapters one pattern rather than three unrelated
pieces of glue code is that a caller holding an `ICarrierGateway` reference
never needs to know which of the three it is holding. A booking service
calls `GetQuote` once and gets a `CarrierQuote` back, regardless of whether
the call underneath went out over SOAP, over REST, or over a flat-file
protocol older than either. That is the value Adapter buys: the cost of a
carrier's own wire format is paid once, inside the one adapter class that
knows about it, instead of at every call site that needs a quote.

## Facade: one call, three subsystems in order

Getting a quote from an adapted gateway is only the middle step of actually
booking a shipment. Before any carrier is asked for a price, the shipment's
origin and destination need to be addresses a carrier can deliver to; after
a quote comes back, the shipment needs a customs declaration filed before it
can move. `IAddressValidator` and `ICustomsFiler` sit on either side of
`ICarrierGateway`, each with exactly one method:

```csharp
public interface IAddressValidator
{
    void Validate(ShipmentRequest request);
}

public interface ICustomsFiler
{
    string File(ShipmentRequest request, CarrierQuote quote);
}
```

`ShipmentBookingFacade` is the Facade: a single `Book` method a booking
screen calls once, that runs all three subsystems in the one order that
makes sense, validate, then quote, then file:

```csharp
public sealed class ShipmentBookingFacade(IAddressValidator validator, ICarrierGateway gateway, ICustomsFiler filer)
{
    public ShipmentConfirmation Book(ShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        validator.Validate(request);
        CarrierQuote quote = gateway.GetQuote(request);
        string bookingReference = filer.File(request, quote);

        return new ShipmentConfirmation(quote.CarrierCode, bookingReference, quote.Amount);
    }
}
```

`Book` does not hide what the three subsystems do; a caller could still
construct an `AddressValidator`, an adapted `ICarrierGateway`, and a
`CustomsFiler` and call all three directly. What `Book` hides is the order
and the short-circuit: `validator.Validate` runs first and, because it is
not wrapped in a `try`/`catch`, propagates any `AddressValidationException`
straight out of `Book` before `gateway.GetQuote` or `filer.File` ever run.
`ShipmentBookingFacadeTests.Book_validates_quotes_and_files_the_shipment_in_order`
substitutes all three subsystems with NSubstitute, books a two-kilogram
ATL-to-ORD shipment, and checks `validator.Received(1).Validate(request)`,
`gateway.Received(1).GetQuote(request)`, and
`filer.Received(1).File(request, quote)`, with the returned
`ShipmentConfirmation` carrying the carrier code, the booking reference the
substituted filer returned, and the quoted amount.
`Book_surfaces_an_address_validation_failure_without_quoting_or_filing` is
the test that proves the short-circuit directly: a substituted validator is
told to throw `AddressValidationException` for a request addressed to
`"INVALID"`, and the test asserts both that `Book` lets that exception
through unchanged and that `gateway.DidNotReceiveWithAnyArgs().GetQuote` and
`filer.DidNotReceiveWithAnyArgs().File` both hold afterward, meaning the
facade never reached either remaining subsystem once the first one failed.

`AddressValidator`, the real implementation `IAddressValidator` usually
wraps, rejects a blank address or one explicitly marked invalid and raises
`AddressValidationException` naming the address that failed;
`AddressValidator_rejects_a_malformed_address` and
`AddressValidator_accepts_a_well_formed_address` check both sides of that
rule directly against the real validator rather than a substitute.
`AddressValidationException` itself exposes the three constructors expected
of a .NET exception type, parameterless, message-only, and
message-plus-inner-exception, so it composes the same way any built-in
exception type does, whether a caller constructs one directly or a
deserializer reconstructs one.

## Bridge: a label kind and a label format that vary independently

`ShipmentLabel` and `ILabelRenderer` keep the label-kind decision and the
output-format decision apart as two separate type hierarchies instead of
one combined one:

```csharp
public abstract class ShipmentLabel(ILabelRenderer renderer)
{
    protected abstract LabelContent BuildContent();

    public string Render() => renderer.Render(BuildContent());
}

public interface ILabelRenderer
{
    string Render(LabelContent content);
}
```

`ShipmentLabel` is the Bridge's abstraction side. It owns exactly one piece
of behavior, building a `LabelContent` record out of whatever a concrete
label kind needs, and delegates everything about turning that content into
a printable string to whichever `ILabelRenderer` it was constructed with.
`StandardShipmentLabel` and `ReturnShipmentLabel` are the two refined
abstractions, each overriding `BuildContent` and nothing else:

```csharp
public sealed class StandardShipmentLabel(ILabelRenderer renderer, CarrierQuote quote, ShipmentRequest request)
    : ShipmentLabel(renderer)
{
    protected override LabelContent BuildContent() =>
        new("Standard Shipment", quote.CarrierCode, request.Destination, "Deliver during business hours.");
}

public sealed class ReturnShipmentLabel(ILabelRenderer renderer, CarrierQuote quote, ShipmentRequest request)
    : ShipmentLabel(renderer)
{
    protected override LabelContent BuildContent() =>
        new("Return Shipment", quote.CarrierCode, request.Origin, "Return to sender on refusal.");
}
```

`PdfLabelRenderer` and `ZplLabelRenderer` are the implementor side, each
turning the same `LabelContent` into a format-distinct string, a stand-in
for actually laying out a PDF page or emitting positioned ZPL fields:
`PdfLabelRenderer`'s output starts with the marker `%PDF-LABEL%`, and
`ZplLabelRenderer`'s starts with `^XA`, the opening field-origin marker a
ZPL-reading printer recognizes. Neither renderer knows or cares whether the
`LabelContent` it receives came from a `StandardShipmentLabel` or a
`ReturnShipmentLabel`; `LabelContent` is the one shape that crosses the
bridge, carrying a title, a carrier code, a destination string, and an
instruction line regardless of which label kind built it.

Two label kinds and two renderers make four combinations, and
`ShipmentLabelTests.Label_renders_correctly_for_every_kind_and_renderer_combination`
is a single `[Theory]` with four `[InlineData]` cases that construct all
four directly: a `StandardShipmentLabel` or a `ReturnShipmentLabel`,
rendered through a `PdfLabelRenderer` or a `ZplLabelRenderer`, against the
same SEA-to-NRT shipment and the same Seabright quote in every case. Each
case checks that the rendered string carries the right title, the right
carrier code, and the right address, the destination for a standard label
and the origin for a return label, and that the output starts with
`%PDF-LABEL%` when the renderer is PDF and `^XA` when it is ZPL, never both.
Adding a third label kind later, a drop-ship label, costs one new
`ShipmentLabel` subclass and no change to either renderer; adding a third
renderer, a thermal printer with its own command set, costs one new
`ILabelRenderer` implementation and no change to either label kind. That
independence, each hierarchy growing without touching the other, is the
reason Bridge is two hierarchies instead of one.

## User-defined conversion operators: implicit for safe, explicit for lossy

The three carrier adapters above already convert units: Fastline prices in
cents, so its adapter needs no money conversion at all; Seabright prices in
dollars, so its adapter narrows a decimal into `Money`; Airco expects a
weight in pounds, so its adapter converts the request's kilogram weight
before building its native call. C# lets a type declare exactly how it
converts to and from another type with a user-defined conversion operator,
and the choice between implicit and explicit is not a style preference. It
is a statement about whether the conversion can lose information.

`Money` stores an amount as an integral cent count rather than a decimal
dollar figure, so repeated addition never drifts the way decimal dollar
arithmetic can. Converting a `Money` value out to decimal dollars is always
exact, because every cent count maps to one unambiguous dollars-and-cents
figure, so that conversion is implicit. Converting a decimal dollar figure
down to `Money` is the opposite case: a value like `1.005m` has no exact
whole-cent representation, so the conversion rounds to the nearest cent
rather than preserving the original figure, and that conversion is
explicit:

```csharp
public readonly record struct Money(long Cents)
{
    public decimal ToDecimal() => Cents / 100m;

    public static implicit operator decimal(Money money) => money.ToDecimal();

    public static Money FromDecimal(decimal dollars) =>
        new(checked((long)Math.Round(dollars * 100m, MidpointRounding.ToEven)));

    public static explicit operator Money(decimal dollars) => FromDecimal(dollars);
}
```

A line like `decimal dollars = money;` needs no cast anywhere, because the
compiler already knows, from the `implicit operator decimal(Money)`
declaration, that the conversion cannot fail or discard anything. Going the
other way, `var money = (Money)dollars;` has to write the cast, and that
cast is the signal to a reader that this particular conversion can discard
a fraction of a cent the implicit direction never does.
`ConversionOperatorTests.Money_converts_to_decimal_implicitly_and_matches_ToDecimal`
and
`ConversionOperatorTests.Decimal_converts_to_Money_explicitly_and_round_trips_through_FromDecimal`
check both operators against their named alternates, `ToDecimal` and
`FromDecimal`, and confirm the operator and the method always agree.

`Kilograms` and `Pounds` repeat the same implicit-versus-explicit split for
a different reason: a unit change rather than a rounding of the same
figure. Both conversions here are explicit in both directions, because
2.20462 pounds per kilogram has no exact decimal representation either way;
a round trip through `ToPounds` and then `ToKilograms` lands close to, but
not exactly at, the original value:

```csharp
public readonly record struct Kilograms(decimal Value)
{
    private const decimal PoundsPerKilogram = 2.20462m;

    public Pounds ToPounds() => new(Value * PoundsPerKilogram);

    public static explicit operator Pounds(Kilograms kg) => kg.ToPounds();
}

public readonly record struct Pounds(decimal Value)
{
    private const decimal PoundsPerKilogram = 2.20462m;

    public Kilograms ToKilograms() => new(Value / PoundsPerKilogram);

    public static explicit operator Kilograms(Pounds lb) => lb.ToKilograms();
}
```

`AircoGatewayAdapter` calls the explicit `(Pounds)` cast on `request.Weight`
before building Airco's native record, and that explicit syntax is itself
documentation: a reader scanning the adapter sees a cast and knows, without
opening `Kilograms.cs`, that a rounding step happened right there.
`ConversionOperatorTests.Kilograms_and_Pounds_convert_explicitly_and_round_trip_within_tolerance`
converts a 10-kilogram weight to `Pounds` and back, checks both results
against their named `ToPounds` and `ToKilograms` alternates, and asserts
the round trip lands within `0.0001` of the original 10, not at it exactly.

Every operator in this example ships with a named instance or static method
doing the identical conversion: `ToDecimal` beside the implicit
`operator decimal`, `FromDecimal` beside the explicit `operator Money`,
`ToPounds` beside the explicit `operator Pounds`, `ToKilograms` beside the
explicit `operator Kilograms`. A caller who never thinks to look for an
operator overload on a type still finds the conversion by name, and a
caller working through reflection or a serializer that cannot invoke an
operator syntactically still has a method to call. An analyzer that flags a
conversion operator with no named alternate is catching exactly this gap.

{% include excalidraw.html name="18-conversion-operators" caption="Figure 18.2: Money converts out to decimal implicitly, matched by the named ToDecimal, and converts in from decimal explicitly, matched by FromDecimal; Kilograms and Pounds convert explicitly in both directions, matched by ToPounds and ToKilograms, because a unit change rather than a rounding is what each cast signals." %}

## Build, run, observe

Every carrier client in this example computes its quote from a
deterministic formula instead of calling out over a network, so
`CarrierGatewayAdapterTests` calls the real client directly inside the same
test that exercises the adapter and compares the two outputs with nothing
mocked. `FastlineSoapClient.SubmitQuote` returns `1200 + grams * 3` cents
with a four-day transit when origin and destination differ; against the
shared ORD-to-DXB request at 5 kilograms, 5000 grams, that is 16200 cents,
162.00 USD once `Money` converts it, the figure
`FastlineGatewayAdapter.GetQuote` returns for the identical request.
`SeabrightRestClient.RequestRate` answers `18.50 + weight * 0.95` dollars
with a six-day transit between two different ports; a 10-kilogram
HKG-to-RTM request prices at 28.00 USD through `SeabrightGatewayAdapter`.
`AircoLegacyClient.Quote` answers `9.99 + pounds * 0.40` dollars with a
five-day transit between two different airports; a 4-kilogram LAX-to-JFK
request converts to roughly 8.82 pounds, and `AircoGatewayAdapter.GetQuote`
rounds that carrier's own reply to 13.52 USD.

The example is standalone-testable from its own directory:

```sh
cd examples/18-adapter-facade-bridge
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
  duration: 1s 460ms
```

The 14 cases split across what this chapter covers.
`CarrierGatewayAdapterTests` supplies three facts, one Adapter proof per
carrier, each checking the native client's own deterministic answer against
what the adapter returns. `ShipmentBookingFacadeTests` supplies the Facade
proof: validate-quote-file running in order on a successful booking, a
validation failure stopping the sequence before either remaining subsystem
runs, and the two direct `AddressValidator` checks for a malformed and a
well-formed address. `ShipmentLabelTests` supplies the Bridge proof, one
`[Theory]` covering all four label-kind-by-renderer combinations.
`ConversionOperatorTests` supplies the conversion-operator proof: the
implicit `Money`-to-decimal widening needs no cast and agrees with
`ToDecimal`, the explicit decimal-to-`Money` narrowing round-trips through
`FromDecimal`, and the explicit `Kilograms`-`Pounds` round trip lands within
`0.0001` of its starting value and agrees with `ToPounds` and `ToKilograms`.

## When not to use it

An Adapter pays for itself only when a second incompatible shape already
exists or is clearly coming. Wrapping a single carrier client behind
`ICarrierGateway`, with no second carrier anywhere in the codebase and
nothing planned, adds an interface and a class around a call a caller could
make directly, for a mismatch the adapter will never actually resolve.

The same test applies to Facade: `Book` collapsing three subsystem calls
into one is valuable specifically because three subsystems exist and have
to run in a fixed order with a short-circuit on failure. A booking process
with one step, submit a request and get a confirmation back, gains nothing
from a Facade wrapping a single call in another single call; the facade
would just be a second name for the same method.

Bridge pays off exactly where this chapter uses it: two axes that actually
vary independently, each likely to grow a third. A system that will only
ever print one label format needs no `ILabelRenderer` hierarchy at all;
`StandardShipmentLabel` and `ReturnShipmentLabel` could each format their
own string directly, and introducing a renderer interface for a format with
no second implementation adds an abstraction with nothing on the other side
to justify it.

A conversion operator is trustworthy exactly when it stays unsurprising at
the call site, and the two conversions in this chapter lean on being
explicit specifically because each one can discard something, a sub-cent
amount, a fraction of a pound. A conversion that does something a reader
would not expect from its source and target types, say, converting a
`ShipmentRequest` into a `CarrierQuote` by defaulting a price, fits a named
method better than an operator, because a cast, or worse an implicit
conversion firing with no syntax at all, hides that the conversion is doing
more than reshaping the same value into another type. A conversion that
needs a sentence of explanation beyond "unit A to unit B" or "same value,
different representation" belongs in a method whose name carries that
explanation, not in an operator a reader has to open the source to
understand.

> **AI sidebar.** Asked to add a conversion between two of this chapter's
> weight or money types, an AI pairing partner will sometimes mark a lossy
> conversion implicit so it fires without a cast at every call site, because
> implicit reads as more convenient in a generated diff. `Money` and
> `Kilograms` show why that default is wrong: `FromDecimal` and the
> `Kilograms`-`Pounds` conversions are explicit specifically because each
> one can discard information a reader has not asked to discard, and an
> implicit version of either would let that loss happen on an ordinary
> assignment with no cast in the code to flag it. A second common
> suggestion, asked to let a single carrier's client plug into some future
> carrier abstraction, is a Bridge built around one renderer and one label
> kind with nothing else in sight, two hierarchies standing in for one
> concrete class because the pattern sounds forward-looking. Check a
> generated answer against the same two questions this chapter relies on
> directly: is the conversion lossless, which means implicit, or can it
> discard a fraction of a cent or a pound, which means explicit with a named
> alternate; and does a second label kind or a second renderer already
> exist or have a concrete plan behind it, because a Bridge built for an
> axis that will only ever hold one value is two classes standing in for a
> hierarchy that was never going to grow.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Adapter, Facade, and Bridge this chapter builds on.
- Microsoft Learn, [User-defined conversion operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators): the authority for the implicit/explicit distinction behind `Money`'s and `Kilograms`'/`Pounds`'s operators.
- Microsoft Learn, [Implicit numeric conversions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions) and [CA2225: Operator overloads have named alternates](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2225): the basis for pairing every operator in this chapter with a named method.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
