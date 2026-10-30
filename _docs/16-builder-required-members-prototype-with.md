---
title: "Builder, required members, and Prototype with with"
order: 16
layout: tutorial
sectionid: docs
part: "creating-objects-under-control"
description: "Why required members and collection expressions often beat a Builder, and Prototype as a one-line with expression."
---

<!-- EXAMPLE (shipping domain): A booking request with many optional fields; amendments via with. -->

## Learning objectives

By the end of this chapter you can:

- Explain why `required` members combined with object and collection
  initializers close the gap a GoF-era Builder exists to close, a request
  that cannot be constructed without its mandatory fields, and why
  `BookingRequest` needs no builder at all to prove that gap closed.
- State the difference between a compile-time failure and a runtime one:
  an object initializer that omits a `required` member fails to build
  under CS9035, while a fluent builder only discovers a missing field
  when `Build()` runs, and must raise its own exception to report it.
- Build a staged, validating `BookingRequestBuilder` whose fluent methods
  accumulate state and whose `Build()` method enforces cross-field rules
  an object initializer cannot express on its own, and show it produces a
  request equal to the one an object initializer builds directly.
- Implement the Prototype pattern as a `with` expression against a
  reusable template, `BookingTemplates.StandardDomestic`, and explain
  why `with` is a non-destructive clone: the template is never mutated by
  cloning and amending it.
- State the shallow-copy rule `with` follows for a reference-typed member
  such as `IReadOnlyList<LineItem>`, and use a collection-expression
  spread, `[.. template.LineItems, extra]`, to produce a fresh list for an
  amended clone rather than sharing the template's own list.

## Why this matters

A booking request for a shipment carries three facts no carrier can quote
or route around without: where the shipment originates, where it is
going, and what value is being declared for insurance and customs
purposes. Everything else about the request is optional in the sense that
a sensible default exists if the caller says nothing: a service level of
"standard" unless asked otherwise, an empty list of extra line items
unless one is added, no special handling instructions unless the shipment
needs them. A type that models this accurately needs a way to say "these
three things are mandatory, these five are not" and have the compiler,
not a code reviewer or a runtime check buried three calls deep, enforce
that split at the moment a `BookingRequest` comes into existence.

The GoF-era answer to "a type with many optional fields and a few
mandatory ones" is the Builder pattern: a separate object that
accumulates state across a sequence of method calls and produces the
final, immutable object only once, after every mandatory field has had a
chance to be set. That answer made sense against a language with no way
to mark a field mandatory on the type itself: a constructor with eight
parameters, several of them optional, is unreadable at the call site, and
a public setter on every field leaves the type mutable forever after
construction, with no single moment at which "fully and correctly built"
can be asserted. A fluent builder solved both problems by moving
construction into its own staged, named-method sequence and holding the
only `new BookingRequest(...)` call inside a single `Build()` method that
can check whatever it wants before returning.

Modern C# changes the starting point this chapter reasons from. The
`required` modifier lets a property declare, once, right next to its own
definition, that it must be set on every construction path, and the
compiler rejects an object initializer that fails to set it before a
single line of calling code ever runs. A collection-expression default,
`LineItems = []`, gives an optional list member an empty starting value
with no separate null check anywhere downstream. Between the two, a type
with three mandatory fields and five optional ones can often be fully
and safely constructed with one object initializer and zero builder
classes, which is the first half of this chapter. The second half is
where a builder is still the right tool even after `required` exists: once
construction needs a validation rule that spans more than one field, or a
staged assembly sequence a caller builds up across several lines of
business logic before committing to a final request, the fluent form
this chapter writes, `BookingRequestBuilder`, still does something an
object initializer alone cannot. The third half turns to the related but
distinct problem of handing out a fresh request derived from an existing
one, the GoF Prototype pattern, which modern C# answers with a built-in
language feature, the `with` expression, rather than a hand-written
`Clone` method.

{% include excalidraw.html name="16-builder-required-members-prototype-with" caption="Figure 16.1: Three ways to produce a BookingRequest. An object initializer with required Origin, Destination, and DeclaredValue fails to compile under CS9035 if any of the three is missing. BookingRequestBuilder accumulates state through fluent WithOrigin/WithDestination/WithDeclaredValue/AddLineItem calls and validates it at runtime inside Build(), raising BookingValidationException on a missing or invalid field. BookingTemplates.StandardDomestic is cloned and amended with a with expression, a shallow copy that leaves the template's own LineItems list untouched unless the amendment replaces it with a fresh collection-expression spread." %}

## Required members and collection expressions replace most builders

The example behind this chapter lives in
`examples/16-builder-required-members-prototype-with`. `Money` is a
validating value type, a `readonly record struct` whose primary
constructor rejects a negative amount or a malformed currency code on
every construction path, with `Money.Of(amount, currency = "USD")` as the
call-site sugar every booking in this example uses. `LineItem` is a plain
immutable record, a description, an amount, and a quantity, with nothing
further to validate. `BookingRequest` is where this chapter's first claim
gets made directly:

```csharp
public sealed record BookingRequest
{
    public required string Origin { get; init; }

    public required string Destination { get; init; }

    public required Money DeclaredValue { get; init; }

    public string ServiceLevel { get; init; } = "standard";

    public IReadOnlyList<LineItem> LineItems { get; init; } = [];

    public bool InsuranceRequested { get; init; }

    public string? SpecialInstructions { get; init; }

    public string? CustomerReference { get; init; }
}
```

`Origin`, `Destination`, and `DeclaredValue` carry the `required` modifier;
the other five properties do not, and each one states its own default
directly at the property, `"standard"` for `ServiceLevel`, an empty
collection expression, `[]`, for `LineItems`, `false` for the `bool`, and
`null` for the two nullable `string?` members. An object initializer that
sets all three required members compiles and runs with no further
ceremony:

{% raw %}
```csharp
var request = new BookingRequest
{
    Origin = "Chicago, IL",
    Destination = "Denver, CO",
    DeclaredValue = Money.Of(500.00m),
};
```
{% endraw %}

`BookingRequestTests.Object_initializer_with_required_members_yields_the_expected_request`
builds exactly this request and asserts every field lands where the type
declares it should: `ServiceLevel` comes back `"standard"` with nothing
in the initializer mentioning it, `LineItems` comes back empty, and
`InsuranceRequested`, `SpecialInstructions`, and `CustomerReference` all
come back at their declared defaults, `false` and two `null`s. Nothing
in the test writes those five defaults explicitly; the type itself
states them once, at the property, and every caller who constructs a
`BookingRequest` without mentioning them gets the same five values for
free from the compiler's own initialization order, not from a
convention a reviewer has to remember to check.

The other half of the claim is what happens when a caller tries to skip a
required member. An object initializer for `BookingRequest` that omits
`DeclaredValue`, for example, does not compile: the C# compiler reports
error CS9035, "Required member 'BookingRequest.DeclaredValue' must be
set in the object initializer or attribute constructor." That failure
happens at build time, before the code that would have constructed a
malformed request ever runs, and before any test suite gets a chance to
catch it at runtime. A GoF-era Builder has no equivalent compile-time
signal available to it, because the type a C# compiler existed to check
before `required` was a plain mutable class with ordinary settable
properties, none of them distinguishable from one another by the
compiler's own rules. The object-initializer path this section
demonstrates removes the entire reason a Builder exists for a type shaped
like `BookingRequest`: three mandatory fields the compiler enforces, five
optional fields with sensible defaults declared once at the property, and
a single expression that produces a fully valid instance with nothing
left to check afterward.

## Where a Builder still fits

`required` and collection-expression defaults answer "how does a caller
avoid forgetting a mandatory field," but they do not answer a second,
related question: what happens when a value fails a rule that is not
expressible as "present or absent," a declared value that is present but
negative, or a destination that is present but an empty string the
compiler is happy to accept because `string` has no built-in notion of
"non-blank." `BookingRequestBuilder` is this example's answer, a fluent,
mutable accumulator that defers every check until the one method that
actually produces a `BookingRequest`:

```csharp
public BookingRequest Build()
{
    if (string.IsNullOrWhiteSpace(_origin))
    {
        throw new BookingValidationException("A booking request needs a non-empty origin.");
    }

    if (string.IsNullOrWhiteSpace(_destination))
    {
        throw new BookingValidationException("A booking request needs a non-empty destination.");
    }

    if (_declaredValue is not { Amount: >= 0m })
    {
        throw new BookingValidationException("A booking request needs a declared value.");
    }

    return new BookingRequest
    {
        Origin = _origin,
        Destination = _destination,
        DeclaredValue = _declaredValue.Value,
        ServiceLevel = _serviceLevel,
        LineItems = _lineItems.Count == 0 ? [] : [.. _lineItems],
        InsuranceRequested = _insuranceRequested,
        SpecialInstructions = _specialInstructions,
        CustomerReference = _customerReference,
    };
}
```

Every fluent method on `BookingRequestBuilder`, `WithOrigin`,
`WithDestination`, `WithDeclaredValue`, `WithServiceLevel`,
`AddLineItem`, `RequestInsurance`, `WithSpecialInstructions`, and
`WithCustomerReference`, returns `this`, so a caller chains as many of
them as the request needs in whatever order makes sense to the calling
code, and nothing runs until `Build()` is finally called. That deferral
is exactly what a staged assembly sequence needs: a piece of business
logic can call `WithOrigin` on one line because a warehouse lookup just
resolved it, call `AddLineItem` three lines later inside a loop over a
surcharge schedule, and call `Build()` only once every contributing piece
of logic has had its turn, with the three mandatory-field checks and the
non-negative-declared-value check all happening together at that one
final call. An object initializer cannot do this, because an object
initializer is a single expression: every property it sets has to be
known and supplied in one place, which works well for
`BookingRequestTests`'s single-call construction and works poorly for a
request assembled incrementally across a method that branches on several
upstream conditions before it knows the final service level or whether
insurance applies.

`BookingRequestBuilderTests.Builder_produces_a_request_equal_to_the_object_initializer_path`
proves the two paths agree when both are given the identical three
required values: a `BookingRequest` built directly through an object
initializer and a second one built through
`new BookingRequestBuilder().WithOrigin(...).WithDestination(...).WithDeclaredValue(...).Build()`
compare equal with `Assert.Equal`, which holds because `BookingRequest`
is a record and two records compare by value, not by reference.
`BookingRequestBuilderTests.Build_without_an_origin_throws_booking_validation_exception`
proves the runtime side of the validation gap this builder exists to
close: a builder that never calls `WithOrigin` still compiles cleanly,
because `WithOrigin` is an ordinary optional method call on a mutable
accumulator with no `required` concept of its own, and the omission is
only caught when `Build()` runs and throws `BookingValidationException`.
That exception, a `sealed class` with the three constructors CA1032
expects on a custom exception type, a parameterless one, a
message-only one, and a message-plus-inner-exception one, is the runtime
mirror of the compile-time CS9035 error the object-initializer path
raises for the identical kind of omission. The two failures look
different, one a build error with a line number, the other a thrown
exception a test asserts against, but they answer the identical
question: did every mandatory field get set before this request was
treated as complete. A builder is the right tool specifically for the
validation a `required` property cannot express, a cross-field rule like
"the declared value must be non-negative," not for the presence check
`required` already owns outright.

## Prototype via `with`: cloning without a `Clone` method

The GoF Prototype pattern solves a problem one step removed from
construction: given an existing, fully configured object, produce a new,
independent one that starts from the same state and then diverges in a
few specific ways, without re-specifying every field the new object
shares with the original. The GoF-era mechanism for this is a `Clone`
method a type implements itself, usually a shallow memberwise copy the
type's author writes once and every caller then calls instead of
re-constructing an equivalent object field by field. C# records remove
the need to write that method at all: every record, `BookingRequest`
included, supports a `with` expression as a language feature, producing a
new instance that copies every member from the source and then applies
whatever overrides the expression lists.

`BookingTemplates.StandardDomestic` is a prototype in exactly the GoF
sense, a fully populated, ready-to-reuse `BookingRequest` for a shipping
lane this domain books often enough to keep a template around for it:

{% raw %}
```csharp
public static BookingRequest StandardDomestic { get; } = new()
{
    Origin = "Columbus, OH",
    Destination = "Memphis, TN",
    DeclaredValue = Money.Of(1200.00m),
    ServiceLevel = "standard",
    LineItems = [new LineItem("Fuel surcharge", Money.Of(45.00m), 1)],
    InsuranceRequested = true,
    SpecialInstructions = "Dock door 4, weekday deliveries only.",
    CustomerReference = "WH-COL-MEM",
};
```
{% endraw %}

A caller who needs a request that matches `StandardDomestic` in every
respect except a different destination and an upgraded service level
writes one `with` expression naming only the two fields that change:

```csharp
var amended = BookingTemplates.StandardDomestic
    with { ServiceLevel = "express", Destination = "Atlanta, GA" };
```

`BookingTemplatesTests.With_expression_clones_and_amends_without_mutating_the_template`
proves both halves of what a prototype clone has to guarantee. First, the
amendment itself: `amended.ServiceLevel` comes back `"express"` and
`amended.Destination` comes back `"Atlanta, GA"`, the two fields the
`with` expression named, while `amended.Origin`,
`amended.DeclaredValue`, and `amended.CustomerReference` all equal the
template's own values, carried across unchanged because the expression
never mentioned them. Second, and this is the property that makes
`with` a true Prototype clone rather than a mutation in disguise, the
template itself is untouched afterward: the same test asserts
`template.ServiceLevel` is still `"standard"` and `template.Destination`
is still `"Memphis, TN"` after `amended` was produced from it.
`BookingTemplates.StandardDomestic` is a `static` property read by every
caller in the process; if producing `amended` had mutated the shared
template instead of cloning it, every other caller reading
`StandardDomestic` afterward would see the mutated "express to Atlanta"
version instead of the standard domestic lane the template is supposed
to represent permanently. `with` cannot cause that failure, because it
always produces a new object and never writes through the receiver it
was called on.

## The shallow-copy rule and the collection-expression spread

`with` copies the source record member by member, and for a value-typed
member like `DeclaredValue`, a `Money`, that copy is a fully
independent duplicate, exactly as it would be for an `int` or a
`decimal`. For a reference-typed member, the copy is a reference to the
same underlying object the source still points to, not a deep
duplication of whatever that object holds internally. `LineItems` is
typed `IReadOnlyList<LineItem>`, an interface over a reference type, and
a `with` expression that does not mention `LineItems` at all copies the
reference itself: the amended clone and the source both end up pointing
at the identical list instance in memory. That is fine as long as the
underlying list is never mutated through either reference, which an
`IReadOnlyList<T>` already guarantees it cannot be through the interface
itself, but it means a clone that wants a different set of line items
cannot append to the list it inherited from the template, because
appending to a shared list would be visible to the template and to every
other clone taken from it.

The fix is to replace the member outright rather than try to mutate what
`with` copied, and the collection-expression spread operator gives that
replacement a one-line form:

```csharp
var amended = template with { LineItems = [.. template.LineItems, extraItem] };
```

`[.. template.LineItems, extraItem]` builds a brand-new list, copying
every element out of `template.LineItems` and appending `extraItem`
after them, and assigns that new list to `amended.LineItems`. The
template's own `LineItems` reference is never touched: `template` still
points at its original list, with its original count, and `amended`
points at a second, independent list one element longer.
`BookingTemplatesTests.Collection_spread_amend_copies_rather_than_shares_the_template_line_items`
proves exactly this: after the spread, `amended.LineItems.Count` is one
more than `template.LineItems.Count` was before the clone, `template`'s
own count is unchanged, `amended.LineItems` contains the new
`extraItem`, and `template.LineItems` does not. Had the amendment instead
tried something like calling `Add` on `template.LineItems` directly, it
would not compile, because `IReadOnlyList<T>` exposes no `Add` method at
all; the type itself rules out the mutation path that would otherwise
make this bug possible to write. The spread is the idiom this example
uses instead: produce a wholly new collection from the old one plus
whatever changes, and hand that new collection to `with` as the override
for the member, rather than reaching for any mutation of a collection a
`with` expression already shared with its source by reference.

This is the one caveat a reader needs to use `with` safely as a
Prototype mechanism: it is an unconditional shallow copy for every
value-typed member, and reference-sharing for every reference-typed
member unless the `with` expression's own override list replaces that
member with something new. `Money` and `string` both behave safely under
this rule without any extra care, `Money` because it is a value type
copied wholesale and `string` because it is immutable so a shared
reference can never be mutated out from under either side anyway. A
member typed as a mutable collection, a mutable nested object, or
anything else whose state can change after construction is the one
category that needs the explicit-replacement treatment this section
walks through, and `LineItems` is this example's case of exactly that
category.

## Build, run, observe

Every command below runs with the local .NET 10 SDK; nothing in this
loop needs a container. The example is standalone-testable from its own
directory:

```sh
cd examples/16-builder-required-members-prototype-with
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. The suite splits across three files: one
`BookingRequestTests` case proving the object-initializer path yields the
expected required and default values; two `BookingTemplatesTests` cases
proving `with` clones and amends `BookingTemplates.StandardDomestic`
without mutating it, and proving the collection-expression spread copies
line items into a fresh list rather than sharing the template's own; and
two `BookingRequestBuilderTests` cases proving the fluent builder
produces a request equal to the object-initializer path and that
`Build()` throws `BookingValidationException` when a required field,
here the origin, is never supplied. Observed locally on Fedora with the
.NET 10 SDK:

```
Test run summary: Passed!
  total: 5
  failed: 0
  succeeded: 5
  skipped: 0
  duration: 3s 863ms
```

## When not to use it

A Builder is over-engineering for a type whose only job is to require a
few fields and default the rest, which is most of what `BookingRequest`
itself needs. Writing a `BookingRequestBuilder`-shaped accumulator for a
type with no cross-field validation rule and no staged, multi-step
assembly sequence adds a class, a private field for every property the
record already declares, and a `Build()` method whose only job is to
copy those fields into an object initializer that could have been
written directly at the call site in the first place. The tell is the
validation rule: if nothing in `Build()` would ever throw, because every
rule the type needs is already expressible as "this member is required"
or "this member defaults to X," the builder is a slower, more verbose
way to reach exactly what `new BookingRequest { ... }` already produces
in one expression, and the fluent class should not exist.

Prototype via `with` is the wrong reach in the opposite direction: when
there is no template worth keeping around. `with` pays for itself when a
real, recurring configuration exists somewhere a team already recognizes
by name, a standard domestic lane booked often enough to deserve a
`BookingTemplates.StandardDomestic` property, not for a one-off request
built from scratch exactly once. Introducing a static template for a
shape of request that is never reused is a layer of indirection a reader
has to trace back to its one caller, with nothing gained over simply
writing the object initializer at that single call site. The shallow-copy
caveat from the previous section is also a reason to reach for `with`
carefully rather than reflexively: a type with several mutable
reference-typed members, each needing its own explicit-replacement
override on every clone to stay independent of its source, is paying
Prototype's cost, remembering which members need replacing and which do
not, for a type that might be better served by a constructor or a
builder that states every member explicitly on each construction instead
of relying on a partial copy to get most of them right by default.

> **AI sidebar.** Asked to make a type like `BookingRequest`
> "configurable with a fluent API," an AI pairing partner will often
> produce a complete `BookingRequestBuilder`, fluent methods and all, for
> a record that `required` members and a plain object initializer already
> construct safely in one expression, reaching for the GoF-era answer out
> of habit from training material written before `required` existed in
> the language. The opposite failure shows up around Prototype just as
> often: a generated `with` clone that amends one or two fields and
> leaves a mutable collection member untouched, on the assumption that
> `with` performs a full deep copy, which then surprises the caller the
> first time code appends to what it believes is the clone's own private
> list and watches the change appear on the original template as well.
> Check a generated answer against two questions before accepting it:
> does `Build()` perform a check a `required` member cannot express, or
> would an object initializer do everything the builder does, and does
> the `with` expression replace every reference-typed member the caller
> intends to change independently, or does it leave at least one shared
> with the source. A `with` expression that only overrides value-typed
> fields and silently shares a list, a dictionary, or any other mutable
> reference with its source is not a complete clone; it is half of one.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of
  Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480)
  (Addison-Wesley, 1994): the original statements of both Builder and
  Prototype this chapter sets against `required` members and the `with`
  expression.
- Microsoft Learn, [`required` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required):
  the authority for the compile-time CS9035 check `BookingRequest`'s
  three required members rely on.
- Microsoft Learn, [Object and collection initializers](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers):
  the authority for the object-initializer construction path this
  chapter builds `BookingRequest` instances through, including the
  collection-expression default `LineItems` declares.
- Microsoft Learn, [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records) and
  the [`with` expression](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression):
  the authority for the non-destructive, shallow-copy semantics
  `BookingTemplates.StandardDomestic` relies on when it is cloned and
  amended.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
