# 25-extension-members

C# 14 extension members attaching new behavior to a sealed, unowned carrier
SDK type, without subclassing it or wrapping it.

- **The unowned type.** `CarrierShipment`, in `CarrierParcels.Vendor`, stands
  in for a third-party carrier SDK assembly we cannot modify: a `sealed`
  class with a constructor and three read-only properties
  (`WeightGrams`, `DestinationCode`, `DeclaredValueMinorUnits`), no methods.
- **Extension members.** `CarrierShipmentExtensions` is a single static class
  with three `extension` blocks over `CarrierShipment`:
  - An instance block exposing `WeightKilograms` and `IsHeavy` as extension
    properties, and `WithAddedWeight` as an extension method that returns a
    new shipment rather than mutating the sealed original.
  - A static block exposing `Empty` as a static extension property and
    `FromKilograms` as a static extension method, both called the same way a
    reader would call a real static member on `CarrierShipment`.
  - An operator block adding `operator +` so two shipments combine with plain
    `+`, plus a named `Add` static extension method as the CA2225-required
    friendly alternate for the operator.
- **Classic form, same file.** `NormalisedDestination` is an ordinary
  `this`-parameter extension method on the same static class, showing the
  pre-C# 14 form coexisting with the new `extension` blocks.

**What the tests prove.** `ExtensionMemberTests` checks the instance extension
properties compute the right values, `WithAddedWeight` never changes the
original shipment, and the two static extension members (`Empty`,
`FromKilograms`) and the classic `NormalisedDestination` extension method
behave as plain static or instance members would. `ExtensionOperatorTests`
checks `operator +` combines weights and declared values from the left
operand's destination, then calls an instance extension property, a static
extension member, and the operator together on the sealed vendor type to
prove all three extension member kinds resolve on a type that declares none
of them itself.

Shipping domain, no containers, pure BCL: this example has no runtime
dependency beyond the .NET 10 SDK itself.

## Build and test

```sh
cd examples/25-extension-members
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
