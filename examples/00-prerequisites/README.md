# 00-prerequisites

The toolchain smoke test for this book: a `Consignment` record and the two
small value types it needs, `ConsignmentId` and `Weight`.

**What the test proves.** The six tests in `Consignments.Tests/ConsignmentTests.cs`
verify behaviour, not just a green build: two consignments built from the same
values compare equal (record value semantics), a `with` expression amends one
property and leaves the original untouched, and the primary constructors of
`Consignment`, `Weight`, and `ConsignmentId` each reject the invalid input they
claim to reject (a blank destination, a non-positive weight, a blank id) by
throwing rather than silently accepting it.

Shipping domain, no containers: `Consignment` models a single booked shipment
(an id, a destination, a weight), nothing more. There is no database, no HTTP
call, and nothing to run beyond the local `dotnet` CLI.

## Build and test

```sh
cd examples/00-prerequisites
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
