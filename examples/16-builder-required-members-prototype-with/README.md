# 16-builder-required-members-prototype-with

Builder dissolved by required members and collection expressions, and
Prototype expressed as `with`, both producing the same `BookingRequest`:

- **Required members (Builder's compile-time half).** `BookingRequest` marks
  `Origin`, `Destination`, and `DeclaredValue` as `required`. An object
  initializer that omits any of them fails to compile; there is no way to
  construct an incomplete instance. `LineItems` defaults to an empty
  collection expression (`[]`), and the other members default to sensible
  values, so a caller states only what differs from the default.
- **`BookingRequestBuilder` (Builder's runtime half).** A fluent,
  constructor-free builder, `WithOrigin`, `WithDestination`,
  `WithDeclaredValue`, `WithServiceLevel`, `AddLineItem`, `RequestInsurance`,
  `WithSpecialInstructions`, `WithCustomerReference`, kept alongside the
  object-initializer path it stands next to. Where a required member only
  fails at compile time, this builder's `Build()` method discovers a missing
  field at run time and reports it with `BookingValidationException`, the
  closest a general-purpose builder gets to the compiler's own check.
- **Prototype via `with` (`BookingTemplates`).** `StandardDomestic` is a
  fully populated `BookingRequest` prototype for a recurring shipping lane.
  Cloning it with `with { ... }` replaces GoF's `Clone` method: the language
  performs the copy, and the `with` initializer states only the fields that
  change. A collection-expression spread, `LineItems = [.. template.LineItems,
  extraItem]`, clones the line items into a fresh list rather than sharing
  the template's.

**What the tests prove.** `BookingRequestTests` builds a `BookingRequest`
through the object-initializer path and checks every required member lands
and every init-optional member defaults as declared.
`BookingTemplatesTests` clones `BookingTemplates.StandardDomestic` with
`with`, amending two members, and asserts the amended instance differs where
intended while the original template is untouched; a second test spreads the
template's line items into an amended copy and asserts the amended instance
gained a line item while the template's own `LineItems` count is unchanged.
`BookingRequestBuilderTests` builds the same values through
`BookingRequestBuilder` and asserts the result is equal, by record value
equality, to the object-initializer path, then asserts that omitting the
origin makes `Build()` throw `BookingValidationException`.

Shipping domain, no containers: `Money` is a validating value type, and
`BookingRequest`, `LineItem`, `BookingRequestBuilder`, and
`BookingTemplates` are pure, synchronous domain code, no network or I/O to
fake out.

## Build and test

```sh
cd examples/16-builder-required-members-prototype-with
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
