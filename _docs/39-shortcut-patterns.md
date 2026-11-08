---
title: "Shortcut patterns"
order: 39
layout: tutorial
sectionid: docs
part: "patterns-in-the-wild"
description: "Dispatch tables, delegate maps, and FrozenDictionary: shortcuts that are not anti-patterns."
---

<!-- EXAMPLE (shipping domain): A dispatch table for document types, backed by FrozenDictionary. -->

## Learning objectives

By the end of this chapter you can:

- Build a `DocumentDispatcher` whose `Render` resolves a `DocumentKind` to its handler with one `FrozenDictionary` lookup, instead of type checks or a class hierarchy.
- Explain why `ToFrozenDictionary` runs once over a `Dictionary` literal to produce a read-optimized, immutable table every dispatcher shares.
- Distinguish `Render`, which throws `UnknownDocumentKindException` for an unregistered kind, from `TryRender`, which returns `false` and a `null` document.
- Check that a dispatch table has exactly one handler for every defined `DocumentKind`.
- Recognize when a kind's state or dependencies outgrow a rendering method and need a dedicated handler type.

## Why this matters

A consignment's paperwork comes in several kinds: a bill of lading, a commercial invoice, a packing list, a certificate of origin, a customs declaration. Each needs its own rendering, with its own fields and wording, and none carries any other behavior. A bill of lading holds no state across requests, no carrier connection, and notifies nothing. The kinds differ only in data: which fields a renderer reads and the sentence it builds.

The alternatives each cost something:

| Shape | Cost |
| --- | --- |
| `IDocumentRenderer`, five classes, and a factory mapping `DocumentKind` to a type | Six types to say "given this kind, call this method" |
| A `switch` expression over `DocumentKind` | One method that grows an arm per kind; a reader checking one kind scans past the others |
| A dispatch table | A kind maps to a method, built once; dispatch is one dictionary read |

`System.Collections.Frozen` supplies the table type. A `FrozenDictionary` spends extra time once, at construction, organizing itself for reads, and is never mutated afterward. `ToFrozenDictionary` turns a `Dictionary` literal of method groups into that table in one call. The variation stays in one small method per kind, and the routing collapses to a field and a lookup. When five kinds differ only in data, the fitting pattern isn't the one with the most named classes.

{% include excalidraw.html name="39-shortcut-patterns" caption="Figure 39.1: DocumentDispatcher.Render looks up a DocumentKind in a FrozenDictionary built once at startup and calls the matching renderer directly, with no per-kind subclass or switch to maintain." %}

## Five document kinds, one method each

```csharp
public enum DocumentKind
{
    BillOfLading,
    CommercialInvoice,
    PackingList,
    CertificateOfOrigin,
    CustomsDeclaration,
}
```

A request carries its kind and the field values its wording needs:

```csharp
public sealed record DocumentRequest(DocumentKind Kind, string ConsignmentId, IReadOnlyDictionary<string, string> Fields);
```

`Fields` is a string-keyed dictionary rather than one typed record per kind. A bill of lading needs carrier and vessel; a customs declaration needs tariff code and declared value. Putting every kind's fields on one shared type would leave most of them unused on any request. A renderer that doesn't find a key falls back to a default string, so a request missing an optional field still renders.

```csharp
public sealed record RenderedDocument(DocumentKind Kind, string ConsignmentId, string Body);
```

The result echoes `Kind` and `ConsignmentId`, so code several steps downstream can tell which document and consignment a body belongs to without carrying the original request. Both types are records and compare by value.

The wording lives in `DocumentRenderers`, one static method per kind:

```csharp
public static class DocumentRenderers
{
    public static RenderedDocument RenderBillOfLading(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = $"Bill of lading for {request.ConsignmentId}: carrier={Field(request, "carrier")}, vessel={Field(request, "vessel")}.";
        return new RenderedDocument(DocumentKind.BillOfLading, request.ConsignmentId, body);
    }

    public static RenderedDocument RenderCustomsDeclaration(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = $"Customs declaration for {request.ConsignmentId}: tariffCode={Field(request, "tariffCode")}, declaredValue={Field(request, "declaredValue")}.";
        return new RenderedDocument(DocumentKind.CustomsDeclaration, request.ConsignmentId, body);
    }

    private static string Field(DocumentRequest request, string key) =>
        request.Fields.TryGetValue(key, out var value) ? value : "unspecified";
}
```

`Field` removes a repeated `TryGetValue`-or-default and knows nothing about `DocumentKind`. The duplication a dispatch table removes is in routing, not in a small helper; each renderer still names its own fields in domain terms. `PackingList`, `CertificateOfOrigin` and `CommercialInvoice` follow the same shape. Nothing in `DocumentRenderers` branches on `DocumentKind`, so changing the certificate-of-origin wording means opening one method. The table doesn't replace rendering behavior, which stays in ordinary methods; it replaces the code that chooses which method runs.

## Building the table once with FrozenDictionary

{% include excalidraw.html name="39-frozen-table-built-once" caption="Figure 39.2: A seed dictionary of document kinds to renderer methods is frozen once into the shared Handlers table, and every DocumentDispatcher instance reads that one table instead of rebuilding its own copy." %}

```csharp
public sealed class DocumentDispatcher
{
    internal static readonly FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>> Handlers =
        new Dictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>>
        {
            [DocumentKind.BillOfLading] = DocumentRenderers.RenderBillOfLading,
            [DocumentKind.CommercialInvoice] = DocumentRenderers.RenderCommercialInvoice,
            [DocumentKind.PackingList] = DocumentRenderers.RenderPackingList,
            [DocumentKind.CertificateOfOrigin] = DocumentRenderers.RenderCertificateOfOrigin,
            [DocumentKind.CustomsDeclaration] = DocumentRenderers.RenderCustomsDeclaration,
        }.ToFrozenDictionary();
}
```

Each value is a method group converted to `Func<DocumentRequest, RenderedDocument>` with no lambda. `ToFrozenDictionary` runs once, when the type initializes `Handlers`. The seed `Dictionary` exists only to build the frozen copy; nothing holds it afterward, and nothing can mutate the table.

| | `Dictionary<TKey, TValue>` | `FrozenDictionary<TKey, TValue>` |
| --- | --- | --- |
| Construction | Cheap | Extra work, once, to optimize for reads |
| Mutation API | Always available | None: no `Add`, no indexer setter |
| Concurrent reads | Safe only if every caller is known not to mutate | Safe by type |
| Fits | Collections that grow and shrink | A fixed set known at startup |

The second row matters as much as speed. A plain `Dictionary` that nobody intends to mutate still exposes a mutation API that a later change could call from another thread mid-read, and a reviewer can only rule that out by reading every caller. With `FrozenDictionary`, the race can't be introduced: request-handling code reads `Handlers` from as many threads as the host runs, with no lock in `DocumentDispatcher`.

`Handlers` is `internal` so code in the same assembly can inspect the table, through it or through an internal `Table` property returning the same reference, without exposing it to every caller. It's `static`, so every `DocumentDispatcher` shares one table, built once per process. A dispatcher instance is something stable to hold and inject; its members `Render`, `TryRender` and `SupportedKinds` read no per-instance state.

## Render, TryRender, and a table that accounts for every kind

```csharp
public RenderedDocument Render(DocumentRequest request)
{
    ArgumentNullException.ThrowIfNull(request);
    return Handlers.TryGetValue(request.Kind, out var handler)
        ? handler(request)
        : throw new UnknownDocumentKindException($"No renderer is registered for document kind '{request.Kind}'.");
}
```

A miss throws `UnknownDocumentKindException`, a dedicated type a caller can catch precisely. A miss happens only when an enum member is added without a table entry, or a caller casts an out-of-range integer to `DocumentKind`. For callers where a miss is routine, `TryRender` reports it:

```csharp
public bool TryRender(DocumentRequest request, out RenderedDocument? document)
{
    ArgumentNullException.ThrowIfNull(request);
    if (Handlers.TryGetValue(request.Kind, out var handler))
    {
        document = handler(request);
        return true;
    }

    document = null;
    return false;
}
```

A caller validating a mixed batch from an upstream system, where an unsupported kind is input to skip, reads the `bool` like any `TryGetValue`. Both methods share the lookup and differ only on a miss: one form for code that treats a miss as a bug, one for code that treats it as an outcome.

### Keeping the table complete

The compiler can't link `DocumentKind` to the dictionary literal; they're separate declarations. Three checks close the gap:

- **Completeness.** `Enum.GetValues<DocumentKind>()` compared as a set with `DocumentDispatcher.Table.Keys` fails the moment a sixth kind is added without its renderer.
- **Every kind dispatches to its own handler.** Rendering each value from `Enum.GetValues<DocumentKind>()` and checking the result's `Kind` catches a copy-paste slip, such as `BillOfLading` pointing at `RenderCommercialInvoice`, and picks up a new kind automatically.
- **One frozen instance.** Two dispatchers return the same `Table` reference, and its runtime type is `FrozenDictionary`, so a refactor to a plain `Dictionary` fails instead of silently losing immutability.

## A dispatch table beside the hierarchy it replaces

```csharp
public IReadOnlyCollection<DocumentKind> SupportedKinds => Handlers.Keys;
```

`SupportedKinds` lets a caller check membership, or fill a list of valid kinds, from the table itself instead of a second, separately maintained list, while `Handlers` stays an implementation detail.

The table stays the right shape while what varies between kinds is data. When one kind needs its own mutable state across calls, a constructor-injected carrier rate service, or a lifecycle that opens a resource before rendering, a `Func` entry can't carry it: a `Func` has no constructor to inject into and no fields to hold state. Don't force that kind in by reaching into static state from its method. Let it graduate to a type implementing a shared interface, the way a Strategy or Factory product would, and leave the other kinds as method groups. A table and a few dedicated types can sit side by side, with the table routing to whichever shape a kind needs.

{% include excalidraw.html name="39-dispatch-table-vs-hierarchy" caption="Figure 39.3: The same five document kinds can route through one frozen lookup table or through an interface with a class per kind and a factory. The table wins when the variation is pure data." %}

Both shapes hide routing behind one call. They differ in what they can carry: a class can hold injected collaborators, keep instance state and join a container-managed lifecycle; a method group in a `FrozenDictionary` is a static reference to a static method that varies only on its `DocumentRequest`.

Adding a sixth kind shows the difference:

| Step | Table | Hierarchy |
| --- | --- | --- |
| New code | An enum member, a static method, one line in the literal | An enum member, a class with a constructor, a factory branch |
| A forgotten registration | Caught by the enum-versus-keys check | Same risk, spread across the factory |

Neither shape eliminates an unregistered case; the table has less surface on which to forget one.

## Build, run, observe

Every renderer is a pure, synchronous function, so nothing beyond the SDK is needed:

```sh
cd examples/39-shortcut-patterns
dotnet test
```

All 13 tests pass; `scripts/verify.sh` at the repo root runs them with every other example. They cover the bill-of-lading wording, per-kind dispatch for every `DocumentKind`, the consignment ID carried through unchanged, the throwing and non-throwing misses, and the three table checks: completeness, one shared instance, and the frozen runtime type.

## When not to use it

- **A case needs state or a dependency.** A renderer that calls a live carrier rate service, caches across requests for a consignment, or opens a connection before producing a body needs a constructor and an instance. Reaching into a static service locator or a static mutable field from a table method hides that dependency behind a signature that claims to take only a `DocumentRequest`. A small Strategy or Factory hierarchy expresses it openly.
- **There's one case.** A single document kind needs one method called directly, not a one-member enum and a one-entry `FrozenDictionary`.

Once a table must carry something a delegate can't express, or has nothing left to route between, an interface with a constructor behind it, or no abstraction at all, fits better.

<blockquote class="ai-sidebar" markdown="1">
Asked to add a sixth document kind, an assistant will sometimes propose retiring the table for an `IDocumentRenderer` interface, a class per kind and a factory, treating the hierarchy as the more extensible answer whatever the new kind needs. If the sixth kind differs only in which fields it reads and which words surround them, that rewrite trades one dictionary entry and one static method for a sixth class and a sixth factory branch, with no behavior the table lacked. Check the answer against what varies: a hierarchy earns its weight when a case needs its own state, injected dependency or lifecycle, not when the case count passes a round number. A new pure-data kind belongs in the `Dictionary` literal feeding `ToFrozenDictionary`, beside the other five.
</blockquote>

## For deeper coverage

- Microsoft Learn, [`FrozenDictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2): the authority for the read-optimized, build-once guarantee `ToFrozenDictionary` gives `DocumentDispatcher.Handlers`.
- Microsoft Learn, [`System.Collections.Frozen` namespace](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen): the source for when a frozen collection fits better than a regular `Dictionary` or an immutable collection type.
- Microsoft Learn, [`Enumerable.ToFrozenDictionary` method](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary.tofrozendictionary): the extension method the dispatch table is built with, including the overload that accepts a key selector.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Strategy and Factory Method, the hierarchy shapes a dispatch table stands in for when the variation between cases is data rather than behavior.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
