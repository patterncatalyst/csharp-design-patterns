# 39-shortcut-patterns

A dispatch table over a shipping-document enum, the Shortcut-pattern
alternative to routing a `DocumentRequest` through a Visitor hierarchy or a
hand-rolled switch expression.

- **`DocumentKind`** enumerates the paperwork a consignment can require:
  `BillOfLading`, `CommercialInvoice`, `PackingList`, `CertificateOfOrigin`,
  and `CustomsDeclaration`.
- **`DocumentRenderers`** holds one static method per kind. Each takes a
  `DocumentRequest` and returns a `RenderedDocument` with a body built from
  that kind's own fields; the methods never share a switch, so adding a kind's
  wording never touches another kind's method.
- **`DocumentDispatcher`** builds its dispatch table once, as a
  `FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>>`
  keyed by `DocumentKind` with a `DocumentRenderers` method group as each
  value, the same `FrozenDictionary`-over-a-fixed-seed shape the chapter's
  Flyweight registries use for interned codes. `Render` looks the kind up and
  invokes the matching delegate, or throws `UnknownDocumentKindException` for
  a kind with no registered handler. `TryRender` is the non-throwing
  counterpart for a caller that expects a miss to be routine.

**What the tests prove.** `DispatchTests` sends a `BillOfLading` request
through `Render` and checks the handler's own wording lands in the body, then
runs every `DocumentKind` through `Render` via a `[Theory]` to confirm each
kind dispatches to its own handler rather than falling through to a default.
Separate cases show the consignment ID survives into the rendered document, an
unregistered kind throws `UnknownDocumentKindException` from `Render`, and
`TryRender` reports the same miss as a `false` return with a `null` document
instead of throwing. `FrozenTableTests` checks the dispatch table has exactly
one handler per defined `DocumentKind`, that `DocumentDispatcher.Table` is the
same `FrozenDictionary` instance across multiple `DocumentDispatcher`
instances and repeated reads (it is built once as a `static readonly` field,
not per instance), and that the table really is a `FrozenDictionary`.

Shipping domain, no containers: dispatch over the enum is a pure, synchronous
lookup, no network or I/O to fake out.

## Build and test

```sh
cd examples/39-shortcut-patterns
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
