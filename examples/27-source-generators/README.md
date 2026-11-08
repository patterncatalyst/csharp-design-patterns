# 27-source-generators

A Roslyn incremental source generator that turns a plain data file into
compiled, strongly-typed code, read at build time instead of hand-written.

- **The manifest.** `carriers.manifest` is a flat `Code,DisplayName,Endpoint`
  table, one row per shipping carrier: DHL Express, FedEx Ground, UPS
  Worldwide, USPS Priority. It is registered on `CarrierManifest.csproj` as an
  `AdditionalFile`, which is how a source generator sees a file that is not
  itself C#.
- **The generator.** `CarrierGatewayGenerator`, in the nested
  `CarrierManifest.Generation` project, is an `IIncrementalGenerator`. Its
  pipeline reads every `.manifest` additional file, parses each line into a
  `CarrierEntry`, combines all of them into a `CarrierManifestModel`, and emits
  one `<Code>CarrierGateway : ICarrierGateway` class per row plus a
  `GeneratedCarrierRegistry` with an `All` list and a `Find(code)` lookup. The
  generator only depends on `ICarrierGateway`, the hand-written contract in
  the consuming project; it never defines the contract itself. Adding a fifth
  carrier is a one-line edit to the manifest, not a new hand-written class.
- **Incremental caching.** The pipeline's model types wrap the parsed rows in
  `EquatableArray<T>`, a small struct that compares an `ImmutableArray<T>` by
  its elements instead of by the array's identity. An unrelated edit elsewhere
  in the project leaves the manifest-reading stages `Cached` either way. When
  the manifest is re-read with the same content, the parse reruns; with
  `EquatableArray<T>` it produces an equal model, so it reports `Unchanged` and
  the combine stage stays `Cached`. A plain `ImmutableArray<CarrierEntry>`
  compares unequal, and every stage downstream reruns.
- **The consumer.** `CarrierDispatch`, hand-written in the top-level project,
  calls only `GeneratedCarrierRegistry.Find` and `.All`; it never references a
  specific generated gateway type by name, so it proves the generated code
  both compiles and is callable as ordinary application code.

**Toolchain note.** A source generator runs inside the host compiler process,
so `CarrierManifest.Generation.csproj` targets `netstandard2.0` and pins
`Microsoft.CodeAnalysis.CSharp` to **5.0.0**, matching the Roslyn version the
.NET 10 SDK 10.0.113 host actually ships, rather than NuGet-latest (5.9.0),
which is newer than the host and fails to load generator assemblies under
warnings-as-errors.

**What the tests prove.** The test project drives `CarrierGatewayGenerator`
directly with a raw `CSharpGeneratorDriver` over an in-memory
`AdditionalText`, with no xunit-v2-era verifier package in the loop.
`GeneratorEmitTests` asserts the generator emits one gateway class per
manifest row, that `GeneratedCarrierRegistry` wires all of them into `All` and
`Find`, that the generated code compiles with zero errors against the
hand-written `ICarrierGateway` contract, and that a manifest-free run produces
no generated source at all. `GeneratorCachingTests` reads the driver's own tracked steps. Both stages
report `New` on the first run and `Cached` after an unrelated change. After an
equal-content manifest is swapped in with `ReplaceAdditionalText`, the parse
reports `Unchanged` and the combine `Cached`; that case fails if the model
holds a plain `ImmutableArray<T>`.

Shipping domain, no containers: parsing a manifest and compiling generated
source is pure, synchronous, in-memory work, no network or I/O to fake out.

## Build and test

```sh
cd examples/27-source-generators
dotnet build -c Release CarrierManifest.csproj
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
