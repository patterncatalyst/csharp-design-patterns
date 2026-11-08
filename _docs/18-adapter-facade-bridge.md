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

- Put three carrier clients with unrelated request and response shapes behind one `ICarrierGateway` with an Adapter each.
- Build a Facade, `ShipmentBookingFacade`, that runs validate, quote and file behind one `Book` call and stops at the first failure.
- Separate a Bridge abstraction, `ShipmentLabel`, from its implementor, `ILabelRenderer`, so label kind and output format vary independently.
- Write user-defined conversion operators, implicit for a lossless widening and explicit for a conversion that can lose information, each with a named alternate.
- Recognize when an Adapter, Facade, Bridge or operator is more machinery than the code needs.

## Why this matters

A booking service asks three carriers for a quote on the same shipment, and their APIs share nothing:

| Carrier | Client style | Request | Response |
| --- | --- | --- | --- |
| Fastline | SOAP-era | Strings and an integer weight in grams | A typed record with a cent amount |
| Seabright | REST | A request object with port names and decimal kilograms | A DTO priced in dollars |
| Airco | Flat file | A bare string array in a fixed layout, weight in pounds | A bare string |

Once each carrier answers through one interface, booking is still three calls in a fixed order: validate the addresses, get a quote, file the customs declaration. Out of order, the system charges for or files a shipment nobody confirmed is deliverable.

A label has two independent axes. What it says depends on the kind: a standard label addresses the destination, a return label the origin. What it looks like depends on the printer: PDF for an office printer, ZPL for a thermal printer at the dock. One class per combination means four classes now and more each time either axis grows.

And the carriers disagree on units: cents, dollars, kilograms, pounds. C# conversion operators make each conversion visible, and make it obvious at the call site when one can lose information.

{% include excalidraw.html name="18-adapter-facade-bridge" caption="Figure 18.1: Three incompatible carrier clients each get their own Adapter onto a shared gateway interface; a Facade sequences validation, quoting, and filing behind one call; and a Bridge lets a label kind and its renderer vary independently." %}

## Pattern: Adapter

### The problem

The dispatch service wants to build a request and read back a quote in two lines, whichever carrier answers. Each carrier's client takes and returns its own shapes, so without a common interface every call site carries the translation for every carrier.

### The solution

An **Adapter** converts the interface of an existing class into the interface its callers expect.

### How C# models it

Every call starts and ends at the same two records and one interface:

```csharp
public sealed record ShipmentRequest(string Origin, string Destination, Kilograms Weight);

public sealed record CarrierQuote(string CarrierCode, Money Amount, int TransitDays);

public interface ICarrierGateway
{
    CarrierQuote GetQuote(ShipmentRequest request);
}
```

`FastlineSoapClient.SubmitQuote` takes an origin code, a destination code and a weight in grams, and returns a `FastlineQuoteResponse` with a cent amount and a day count. The adapter is a class with a primary constructor that holds the client:

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

Fastline already prices in cents, the unit `Money` stores, so only the weight needs translating. The other two adapters do more or less work depending on how far their carrier sits from `ShipmentRequest`:

| Adapter | Weight | Price |
| --- | --- | --- |
| `FastlineGatewayAdapter` | Kilograms to grams | Cents passed straight to `Money` |
| `SeabrightGatewayAdapter` | Kilograms, unchanged, in a `SeabrightRateRequest` | Decimal dollars through `Money.FromDecimal` |
| `AircoGatewayAdapter` | Explicit `(Pounds)` cast, into a three-element `string[]` | Bare string reply parsed by hand into amount and transit days |

Each adapter's `CarrierQuote` matches what its native client returns for the same request. A caller holding an `ICarrierGateway` never knows whether the call went out over SOAP, REST or a flat-file protocol. Each carrier's wire format is handled once, inside its adapter, instead of at every call site.

### Conversion operators at the boundary

A user-defined conversion operator declares how a type converts to or from another type. Implicit or explicit states whether the conversion can lose information.

`Money` stores an integral cent count, so repeated addition never drifts. Converting out to decimal dollars is always exact, so it's implicit. Converting a decimal down to `Money` rounds: `1.005m` has no exact whole-cent form. That direction is explicit:

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

`decimal dollars = money;` needs no cast. `var money = (Money)dollars;` does, and the cast tells a reader that a fraction of a cent may go. Each operator agrees with its named method.

`Kilograms` and `Pounds` convert explicitly in both directions, because 2.20462 pounds per kilogram has no exact decimal form either way:

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

A round trip of 10 kilograms through pounds lands within `0.0001` of 10, not on it. The `(Pounds)` cast in `AircoGatewayAdapter` tells a reader, without opening `Kilograms.cs`, that rounding happens there.

| Operator | Direction | Named alternate |
| --- | --- | --- |
| `operator decimal(Money)` | Implicit | `ToDecimal` |
| `operator Money(decimal)` | Explicit | `FromDecimal` |
| `operator Pounds(Kilograms)` | Explicit | `ToPounds` |
| `operator Kilograms(Pounds)` | Explicit | `ToKilograms` |

The named method lets a caller find the conversion without knowing to look for an operator, and gives reflection or a serializer something to call. Analyzer rule CA2225 flags an operator without one.

{% include excalidraw.html name="18-conversion-operators" caption="Figure 18.2: Money converts to decimal implicitly, since the conversion never loses precision, and from decimal explicitly, since it can round. Kilograms and Pounds convert explicitly both ways, since a unit change can never be exact." %}

## Pattern: Facade

### The problem

Booking a shipment means validating the addresses, getting a quote and filing customs, in that order. A caller that quotes before validating, or files before a quote exists, produces a charge or a customs record for a shipment that can't ship. Every booking screen would have to repeat the sequence correctly.

### The solution

A **Facade** provides one simpler interface over a set of subsystem interfaces.

### How C# models it

The two subsystems on either side of the gateway each have one method:

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

`Book` runs the three in order:

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

`Book` doesn't hide the subsystems; a caller can still use all three directly. It hides the order and the short-circuit. `Validate` isn't wrapped in a `try`/`catch`, so an `AddressValidationException` leaves `Book` unchanged and neither `GetQuote` nor `File` is called. On success each subsystem runs once and the `ShipmentConfirmation` carries the carrier code, the filer's booking reference and the quoted amount.

The real `AddressValidator` rejects a blank address, or one marked invalid, with an `AddressValidationException` naming the address. The exception has the three standard constructors (parameterless, message-only, message plus inner exception), so it composes like any built-in exception type.

## Pattern: Bridge

### The problem

Two label kinds and two output formats, built as one class per combination, give four classes. A drop-ship label adds two more; a third printer adds three. The two decisions don't depend on each other, but the class hierarchy ties them together.

### The solution

A **Bridge** separates an abstraction from its implementation so the two can vary independently.

### How C# models it

`ShipmentLabel` is the abstraction and `ILabelRenderer` the implementor:

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

`ShipmentLabel` builds a `LabelContent` record and hands it to whichever renderer it holds. The two refined abstractions override `BuildContent` and nothing else:

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

`PdfLabelRenderer` output starts with `%PDF-LABEL%`; `ZplLabelRenderer` output starts with `^XA`, the ZPL start-format command. Neither renderer knows which label kind built the content. `LabelContent`, with a title, carrier code, address and instruction line, is the one shape that crosses the bridge.

All four combinations render the right title, carrier code and address (destination for standard, origin for return) with the right format marker. Growth stays on one side:

| Change | Cost |
| --- | --- |
| A drop-ship label | One `ShipmentLabel` subclass; no renderer changes |
| A new printer command set | One `ILabelRenderer`; no label changes |

## When not to use it

- **Adapter.** Skip it when there is one carrier and no second planned. An interface and a class around one client add a layer for a mismatch the adapter never resolves.
- **Facade.** Skip it for a one-step process. A facade over a single call is a second name for the same method.
- **Bridge.** Skip it when one axis has one value. With one label format, each label can format its own string; a renderer interface with one implementation has nothing on the other side.
- **Conversion operator.** Use a named method when the conversion does more than change units or representation. Converting a `ShipmentRequest` into a `CarrierQuote` by defaulting a price would hide that behind a cast, or behind no syntax at all if implicit. A conversion that needs a sentence of explanation belongs in a method whose name carries it.

<blockquote class="ai-sidebar" markdown="1">
Asked to add a conversion between weight or money types, an assistant sometimes marks a lossy conversion implicit so it fires without a cast. `FromDecimal` and the `Kilograms`-`Pounds` conversions are explicit because each can discard a fraction of a cent or a pound; an implicit version lets that loss happen on an ordinary assignment. Asked to make a single carrier client "pluggable," it may build a Bridge with one renderer and one label kind. Check a generated answer with two questions: is the conversion lossless (implicit) or can it discard something (explicit, with a named alternate)? And does a second label kind or renderer exist or have a concrete plan? If not, the Bridge is two hierarchies standing in for one class.
</blockquote>

## Build, run, observe

```sh
cd examples/18-adapter-facade-bridge
dotnet test
```

`scripts/verify.sh` at the repository root also runs it. All 14 tests pass. Each carrier client computes its quote from a fixed formula, so the adapter tests call the real client with nothing mocked:

| Client | Formula | Request | Adapted quote |
| --- | --- | --- | --- |
| `FastlineSoapClient.SubmitQuote` | `1200 + grams * 3` cents, 4 days | ORD to DXB, 5 kg | 162.00 USD |
| `SeabrightRestClient.RequestRate` | `18.50 + weight * 0.95` dollars, 6 days | HKG to RTM, 10 kg | 28.00 USD |
| `AircoLegacyClient.Quote` | `9.99 + pounds * 0.40` dollars, 5 days | LAX to JFK, 4 kg (about 8.82 lb) | 13.52 USD |

The facade tests substitute all three subsystems with NSubstitute; the label tests are one `[Theory]` over the four kind and renderer combinations.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Adapter, Facade, and Bridge.
- Microsoft Learn, [User-defined conversion operators](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/user-defined-conversion-operators): the implicit/explicit distinction behind the `Money`, `Kilograms` and `Pounds` operators.
- Microsoft Learn, [Implicit numeric conversions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions) and [CA2225: Operator overloads have named alternates](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2225): the basis for pairing every operator with a named method.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
