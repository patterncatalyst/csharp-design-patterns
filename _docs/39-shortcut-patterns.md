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

- Build a `DocumentDispatcher` whose `Render` method resolves a
  `DocumentKind` to its handler through one `FrozenDictionary` lookup built
  once, rather than through a chain of type checks or a class hierarchy.
- Explain why `ToFrozenDictionary` runs once, against a `Dictionary` literal
  keyed by `DocumentKind`, to produce a read-optimized, immutable table that
  every `DocumentDispatcher` instance shares.
- Distinguish `Render`, which throws `UnknownDocumentKindException` for an
  unregistered kind, from `TryRender`, which reports the same miss as a
  `false` return and a `null` document.
- Write a test that proves a dispatch table has exactly one handler for
  every defined `DocumentKind`, closing a gap a plain dictionary literal
  leaves open to a forgotten case.
- Recognize the point at which a kind's own state or dependencies outgrow a
  single rendering method and call for a dedicated handler type instead of
  one more table entry.

## Why this matters

A consignment's paperwork comes in several kinds: a bill of lading, a
commercial invoice, a packing list, a certificate of origin, a customs
declaration. Each kind needs its own rendering, its own fields pulled out of
a request, its own wording wrapped around them, but none of the five carries
any behavior beyond that one rendering step. A bill of lading does not
accumulate state across requests, does not hold a connection to a carrier
system, and does not notify anything when it finishes rendering. What
differs between the five kinds is data: which fields a renderer reads, and
the sentence it builds from them.

Routing a request like this through a full class hierarchy, an
`IDocumentRenderer` interface, five implementing classes, and a factory that
maps a `DocumentKind` to the right concrete type, builds six types to say
something a lookup already says: given this kind, call this method. A
`switch` expression over `DocumentKind` inside one method says the same
thing without the extra types, but it grows a new arm every time the
paperwork catalog adds a kind, and every arm lives inside that same method
body, so a reader checking the wording for a customs declaration has to
scan past four other kinds first.

A dispatch table says it directly: a `DocumentKind` maps to a method, the
map is built once, and looking a request up is one dictionary read.
`System.Collections.Frozen` gives that map a type built for exactly this
shape, a `FrozenDictionary` that spends extra time once, at construction,
organizing itself for read speed, in exchange for every lookup afterward
running against a structure that will never again be mutated. Modern C#
supports this pattern directly through `ToFrozenDictionary`, turning a
`Dictionary` literal of method-group values into an immutable, read-tuned
table in a single call. The variation between document kinds stays where it
belongs, one small method per kind, while the routing between them collapses
to a field and a lookup. Recognizing when a hierarchy of classes is standing
in for what a table already expresses is as much a design skill as knowing
when to reach for Strategy or Factory in the first place: the pattern that
fits five similar kinds differing only in data is not always the pattern
with the most named classes.

{% include excalidraw.html name="39-shortcut-patterns" caption="Figure 39.1: DocumentDispatcher.Render looks up a DocumentKind in a FrozenDictionary built once at startup and calls the matching renderer directly, with no per-kind subclass or switch to maintain." %}

## Five document kinds, one method each

The example behind this chapter lives in `examples/39-shortcut-patterns`.
`DocumentKind` enumerates the paperwork a consignment's documentation can
require:

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

A request to render one document carries its kind alongside the field
values that kind's wording needs:

```csharp
public sealed record DocumentRequest(DocumentKind Kind, string ConsignmentId, IReadOnlyDictionary<string, string> Fields);
```

`Fields` is a plain string-keyed dictionary rather than five separate
strongly typed request records, one per kind, because the fields a
`BillOfLading` needs, carrier and vessel, have nothing to do with the fields
a `CustomsDeclaration` needs, tariff code and declared value, and giving
each kind its own field set inside one shared `DocumentRequest` type would
mean most of those properties sit unused on any given request. `Fields`
carries whatever the chosen kind's renderer asks for, and a renderer that
does not find a key it expects falls back to a default string rather than
throwing, so a request missing an optional field still renders.

`RenderedDocument` is the matching output type, carrying the same kind and
consignment ID forward alongside the body a renderer built:

```csharp
public sealed record RenderedDocument(DocumentKind Kind, string ConsignmentId, string Body);
```

Echoing `Kind` and `ConsignmentId` back on the result rather than leaving a
caller to remember which request produced which body means a caller holding
only a `RenderedDocument`, several requests downstream of the dispatcher
itself, can still answer which document this is and which consignment it
belongs to without threading the original `DocumentRequest` through every
call in between. Both `DocumentRequest` and `RenderedDocument` are
`record` types rather than classes, so two renders of the same kind,
consignment, and body compare equal by value, which is what
`DispatchTests.Render_dispatches_a_bill_of_lading_to_its_handler` relies on
when it checks the handler's own wording, carrier and vessel, inside the
returned body rather than comparing the whole record against a
hand-written expected instance.

`DocumentRenderers` is where the kind-specific wording lives, one static
method per kind:

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

`Field`, the one private helper every rendering method shares, exists to
avoid repeating the same `TryGetValue`-or-default pattern five times over
rather than to decide anything about which kind is being rendered; it takes
the `request` and a `key` and knows nothing about `DocumentKind` at all. The
duplication a dispatch table is built to remove is duplication in routing,
which kind maps to which method, not duplication in a small utility every
method happens to call the same way; collapsing `Field` into the table
itself would mean giving up the one place each renderer still gets to name
its own required fields by their own domain names, carrier and vessel for a
bill of lading, tariff code and declared value for a customs declaration.

Each method reads only the fields its own kind cares about and returns a
`RenderedDocument` carrying the same kind, the same consignment ID, and the
body it built. `PackingList`, `CertificateOfOrigin`, and `CommercialInvoice`
follow the identical shape against their own fields. Nothing in
`DocumentRenderers` branches on `DocumentKind` anywhere; each method already
knows which kind it handles because the method itself is dedicated to that
kind. A reviewer changing the wording for a certificate of origin opens one
method, reads a few lines, and never has to step past the four other kinds'
logic to confirm the change is isolated. This is the part of the design a
table does not replace: the actual rendering behavior still lives in
ordinary methods, written and tested like any other method. What the table
replaces is the part that decides which method runs for which kind.

## Building the table once with FrozenDictionary

{% include excalidraw.html name="39-frozen-table-built-once" caption="Figure 39.2: A seed dictionary of document kinds to renderer methods is frozen once into the shared Handlers table, and every DocumentDispatcher instance reads that one table instead of rebuilding its own copy." %}

`DocumentDispatcher` holds the map from kind to method, built a single time
as a `static readonly` field:

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

Each value in the dictionary literal is a method group, `DocumentRenderers.RenderBillOfLading`
and its four siblings, read as a `Func<DocumentRequest, RenderedDocument>`
delegate without an explicit lambda wrapper anywhere. `ToFrozenDictionary`
runs exactly once, at the moment the type initializes `Handlers`, over a
plain mutable `Dictionary` that exists only long enough to seed the frozen
copy and is then discarded; nothing after that line holds a reference to
the original dictionary, and nothing can mutate the table once
`ToFrozenDictionary` has returned. `FrozenDictionary` is built for exactly
this lifecycle: construct once from a known, fixed set of entries, then
serve an unbounded number of reads against a structure optimized for
lookups rather than for insertions or removals it will never perform again.
A `Dictionary<TKey, TValue>` stays open to mutation for as long as it
exists, which is the right trade-off for a collection that grows and
shrinks over its lifetime, but `Handlers` never grows or shrinks after
construction, so the mutation path `Dictionary` keeps available is a cost
with nothing to show for it. `FrozenDictionary` spends that cost up front,
during the one-time `ToFrozenDictionary` call, and never again.

Immutability after construction carries a second payoff beyond lookup
speed: a `FrozenDictionary` needs no locking around a read, because no
thread can ever observe it mid-mutation. A plain `Dictionary` shared across
multiple callers, even one nobody intends to mutate after setup, still
exposes a mutation API that a future change could call from a second
thread while another thread is reading, a race a reviewer has to rule out
by reading every caller rather than by reading the type itself. `Handlers`
being a `FrozenDictionary` makes that race impossible to introduce by
accident: there is no `Add`, no indexer setter, no method on the type that
would let a later change reach in and mutate it from anywhere, concurrent
request or not. A dispatcher injected into request-handling code that
serves many requests at once reads `Handlers` from as many threads as the
host cares to run, and every one of those reads is safe without a single
lock anywhere in `DocumentDispatcher`.

`internal` rather than `private` is a deliberate choice on `Handlers`:
`FrozenTableTests`, in the same example, needs to read the table directly
to confirm it behaves the way a dispatch table is supposed to, without
`DocumentDispatcher` exposing that detail to every other caller in the
assembly. `Table`, a second internal member, exposes the identical
reference under a name a test reads more naturally than `Handlers`:

```csharp
internal static FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>> Table => Handlers;
```

`static` is the other half of what makes this a single shared dispatch
table rather than a per-instance cache that happens to be immutable: every
`DocumentDispatcher` a caller constructs shares the exact same
`FrozenDictionary` reference, built once per process regardless of how many
dispatchers get created. A dispatcher instance exists to give a caller
something stable to hold and inject, with `Render`, `TryRender`, and
`SupportedKinds` as its instance surface, but none of those members read
any state that varies from one dispatcher to the next.

## Render, TryRender, and a table that accounts for every kind

`Render` is the single lookup this whole design exists to make possible:

```csharp
public RenderedDocument Render(DocumentRequest request)
{
    ArgumentNullException.ThrowIfNull(request);
    return Handlers.TryGetValue(request.Kind, out var handler)
        ? handler(request)
        : throw new UnknownDocumentKindException($"No renderer is registered for document kind '{request.Kind}'.");
}
```

`Handlers.TryGetValue` finds the registered handler for `request.Kind` or
reports a miss, and `Render` turns a miss into `UnknownDocumentKindException`,
a dedicated exception type rather than a generic `ArgumentException`, so a
caller catching document-dispatch failures can catch exactly this one. A
miss here means a `DocumentKind` enum value with no entry in `Handlers`,
something that can only happen if a new kind is added to the enum without a
matching entry added to the table, or if a caller casts an out-of-range
integer to `DocumentKind` directly. `TryRender` exists because an
unregistered kind is not always a fault from the caller's point of view:

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

A caller validating a batch of mixed document requests from an upstream
system, where an unsupported kind is routine input to skip rather than an
exceptional condition to unwind the stack over, reaches for `TryRender` and
reads the `bool` result the same way it would read any other `TryGetValue`
call. `Render` and `TryRender` share the identical lookup against
`Handlers`; the only difference between them is what happens on a miss,
throw or report, which is exactly the shape the two-method pattern already
established in `TryGetValue` itself gives a caller: one throwing form for
code that treats a miss as a bug, one non-throwing form for code that
treats a miss as an outcome.

`DispatchTests` exercises both paths against every registered kind rather
than against one example kind picked at random:

```csharp
public static TheoryData<DocumentKind> AllDocumentKinds => new(Enum.GetValues<DocumentKind>());

[Theory]
[MemberData(nameof(AllDocumentKinds))]
public void Render_dispatches_each_document_kind_to_the_matching_handler(DocumentKind kind)
{
    var dispatcher = new DocumentDispatcher();
    var request = new DocumentRequest(kind, "CN-2002", new Dictionary<string, string>());

    var rendered = dispatcher.Render(request);

    Assert.Equal(kind, rendered.Kind);
}
```

`AllDocumentKinds` reads the enum's own values through `Enum.GetValues`,
the same call `FrozenTableTests` uses to check table completeness, so the
`[Theory]` automatically picks up a sixth kind the day one is added,
without anyone having to remember to add a sixth test case by hand. Running
one assertion per kind this way, instead of a single test that renders one
kind and trusts the other four by inference, is what catches a copy-paste
mistake inside the dictionary literal, a `BillOfLading` entry accidentally
pointing at `RenderCommercialInvoice`, that a narrower test would miss
entirely because the handler it checked happened to be wired correctly.

The risk a hand-maintained table like this carries is a document kind added
to the enum without a matching entry added to `Handlers`, a mismatch the
compiler has no way to catch because `DocumentKind` and the dictionary
literal are two separate declarations with no language-level link between
them. `FrozenTableTests` closes that gap with a test built specifically to
catch it:

```csharp
[Fact]
public void Dispatch_table_has_a_handler_for_every_defined_document_kind()
{
    var definedKinds = Enum.GetValues<DocumentKind>().ToHashSet();

    var registeredKinds = DocumentDispatcher.Table.Keys.ToHashSet();

    Assert.Equal(definedKinds, registeredKinds);
}
```

`Enum.GetValues<DocumentKind>()` reads every value the enum currently
declares, independent of what `Handlers` happens to register, and comparing
that set against `DocumentDispatcher.Table.Keys` fails the moment someone
adds a sixth document kind without adding its renderer to the table. A
second test, `Dispatch_table_is_a_single_frozen_instance_shared_across_dispatchers`,
constructs two separate `DocumentDispatcher` instances and asserts
`DocumentDispatcher.Table` returns the identical reference both times with
`Assert.Same`, confirming the table really is built once at the type level
rather than once per instance. A third, `Dispatch_table_is_a_frozen_dictionary`,
checks the runtime type of `Table` directly, so a future refactor that
swaps `FrozenDictionary` for a plain `Dictionary` under time pressure fails
a test instead of silently losing the immutability guarantee this design
depends on.

## A dispatch table beside the hierarchy it replaces

`SupportedKinds` gives a caller a way to ask what the dispatcher can handle
without attempting a render and catching a failure:

```csharp
public IReadOnlyCollection<DocumentKind> SupportedKinds => Handlers.Keys;
```

Reading `Handlers.Keys` through a public property, rather than exposing
`Handlers` itself, keeps the table's existence as an implementation detail
from the outside while still letting a caller check membership or populate
a dropdown of valid kinds from a single, already-built source instead of a
second, separately maintained list.

A table like this one stays the right shape exactly as long as the thing
varying between kinds is data: which fields to read, what words to put
around them. The moment one kind needs something the others do not, its own
mutable state across calls, a constructor-injected dependency on a carrier
rate service, a lifecycle that opens a resource before rendering and closes
it after, a single `Func<DocumentRequest, RenderedDocument>` entry can no
longer carry it. A `Func` is a stateless pointer to a method; it has no
constructor to inject a dependency into and no instance fields to hold
state between calls. At that point the right move is not to force the
outlier into the table anyway, perhaps by reaching into static state from
inside its rendering method, but to let that one kind graduate into a real
type, implementing a shared interface the way a Strategy or a Factory
product would, while every other kind stays exactly where it is, a method
group in the table. A design does not have to choose uniformly between "all
five kinds are table entries" and "all five kinds are classes"; a table and
a small number of dedicated types can sit side by side, with the table
routing to whichever shape a given kind needs.

{% include excalidraw.html name="39-dispatch-table-vs-hierarchy" caption="Figure 39.3: The same five document kinds can route through one frozen lookup table or through an interface with a class per kind and a factory. The table wins when the variation is pure data." %}

Both shapes in that comparison solve the identical routing problem, kind in,
correct behavior out, and both hide that routing behind one call a caller
makes without knowing which shape sits on the other side. The difference is
entirely in what each shape can carry. A class implementing a shared
renderer interface can hold constructor-injected collaborators, accumulate
instance state, and participate in whatever lifecycle a dependency injection
container manages for it. A method group sitting in a `FrozenDictionary` can
do none of that; it is a static reference to a static method, and the only
thing it varies on is the `DocumentRequest` passed into it. Choosing the
table when every kind's behavior fits a stateless method, and choosing the
hierarchy the moment one kind's behavior does not, keeps the design matched
to what is varying instead of matched to a habit of reaching for
one shape regardless of what the problem calls for.

The cost of adding a sixth document kind makes the difference concrete. In
the table, a sixth kind means one new `DocumentKind` enum member, one new
static method on `DocumentRenderers` that reads that kind's own fields, and
one new line in the dictionary literal that feeds `ToFrozenDictionary`.
`FrozenTableTests.Dispatch_table_has_a_handler_for_every_defined_document_kind`
fails immediately if the third step is forgotten, so the gap does not
survive past the next test run. In the hierarchy, a sixth kind means a
sixth implementing class, a constructor for it even when that constructor
takes nothing, and a new branch in whatever factory maps `DocumentKind` to
the right concrete type, with the same missing-case risk the table carries,
now spread across a factory method instead of caught by a single
enum-versus-keys comparison. Neither shape is free of the risk that a new
case slips through unregistered; the table's version of that risk is
smaller because there is less surface for a new case to be registered on.

## Build, run, observe

Every renderer in this example is a pure, synchronous function over a
`DocumentRequest`, so the suite needs nothing beyond the local .NET 10 SDK.
The example is standalone-testable from its own directory:

```sh
cd examples/39-shortcut-patterns
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 13
  failed: 0
  succeeded: 13
  skipped: 0
  duration: 863ms
```

The thirteen cases split across what this chapter covers. `DispatchTests`
sends a `BillOfLading` request through `Render` and checks the handler's own
wording lands in the body, runs every `DocumentKind` through `Render` via a
`[Theory]` to confirm each kind dispatches to its own handler rather than
falling through to a default, confirms the consignment ID survives into the
rendered document unchanged, and proves an unregistered kind throws
`UnknownDocumentKindException` from `Render` while the same miss comes back
from `TryRender` as a `false` return with a `null` document instead.
`FrozenTableTests` proves the table itself: a handler is registered for
every defined `DocumentKind` with none missing and none extra, the table
returned by `DocumentDispatcher.Table` is the same instance across multiple
dispatcher constructions, and the table really is a `FrozenDictionary`
rather than a plain, still-mutable `Dictionary`.

## When not to use it

A dispatch table is the wrong reach the moment a case needs something a
stateless method cannot hold. A renderer that has to call out to a live
carrier rate service, cache a result across multiple requests for the same
consignment, or open a connection before it can produce a body needs a
constructor to inject that dependency into and an instance to carry it
between calls, neither of which a `Func` stored in a `FrozenDictionary`
provides. Forcing that case into the table anyway, by reaching into a
static service locator or a static mutable field from inside the rendering
method, hides a real dependency behind a signature that claims to take
nothing but a `DocumentRequest`, which is worse than the small amount of
hierarchy a Strategy or Factory would have cost to express that dependency
in the open.

A table is also the wrong reach when there is only one case to route. A
single document kind with a single renderer needs neither a `DocumentKind`
enum with one member nor a `FrozenDictionary` with one entry; it needs one
method, called directly, with no lookup standing between a caller and the
only behavior that exists. The tell for both situations is the same: once a
table has to carry something a plain delegate value cannot express, or once
it has nothing left to route between, the shortcut has stopped describing
the problem accurately, and an interface with a constructor behind it, or
no abstraction at all, says the same thing without the mismatch.

<blockquote class="ai-sidebar" markdown="1">
Asked to add a sixth document kind to this dispatcher, an AI pairing
partner will sometimes propose retiring the table altogether in favor of an
`IDocumentRenderer` interface, five implementing classes, and a factory
that maps `DocumentKind` to the right one, treating a class-per-case
hierarchy as the more sophisticated or more extensible answer regardless of
what the new kind needs. If the sixth kind still differs from the
other five only in which fields it reads and which words it wraps around
them, that rewrite trades one dictionary entry and one static method for a
sixth class, a sixth branch in a factory, and no behavior the table did not
already provide. Check a generated answer against what varies: a
hierarchy earns its weight when a case needs its own state, its own
injected dependency, or its own lifecycle, not whenever the number of cases
grows past some round number. A new entry that is still pure data in, pure
string out belongs in the `Dictionary` literal feeding `ToFrozenDictionary`,
right beside the five that are already there.
</blockquote>

## For deeper coverage

- Microsoft Learn, [`FrozenDictionary<TKey,TValue>`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2): the authority for the read-optimized, build-once guarantee `ToFrozenDictionary` gives `DocumentDispatcher.Handlers`.
- Microsoft Learn, [`System.Collections.Frozen` namespace](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen): the source for when a frozen collection fits better than a regular `Dictionary` or an immutable collection type.
- Microsoft Learn, [`Enumerable.ToFrozenDictionary` method](https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary.tofrozendictionary): the extension method this chapter's dispatch table is built with, including the overload that accepts a key selector.
- Gamma, Helm, Johnson, and Vlissides, [*Design Patterns: Elements of Reusable Object-Oriented Software*](https://www.pearson.com/en-us/subject-catalog/p/design-patterns-elements-of-reusable-object-oriented-software/P200000009480) (Addison-Wesley, 1994): the original statements of Strategy and Factory Method, the hierarchy shapes a dispatch table stands in for when the variation between cases is data rather than behavior.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
