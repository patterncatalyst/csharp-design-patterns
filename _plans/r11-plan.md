---
title: "r11 plan — Part 8 FINISH (ch23-24)"
output: false
render_with_liquid: false
---

# r11 — author ch23-24 (Part 8: Coordinating and traversing, FINISH)

Opus-planned 2026-10-31. Branch `release/r11` (based on main@a46eb20). Both chapters hands-on and
both need async; this iteration completes Part 8. Four-wave shape (A code / B diagrams AFTER green
code with LOCKED names / C prose / D validate). Enforces: NO cross-language comparison (describe
`yield`, `IAsyncEnumerable`, `await foreach`, expression trees, and pattern matching POSITIVELY and
temporally, never against Java/C++/JVM/Spring, never "erasure"); keep cross-chapter refs light
(each chapter stands alone, name the pattern/idiom in place, no numeric or title pointers).

## Scope + example assignments (shipping domain, from Notion map)

- **ch23 Iterator: yield return / LINQ / IAsyncEnumerable**
  (`_docs/23-iterator-yield-linq-iasyncenumerable.md`, Tracking domain). Paging a tracking history
  with `yield return` (the compiler-generated state machine is the Tier-1 "dissolve"), LINQ composed
  over the iterator (LINQ operators are themselves deferred iterators), then `IAsyncEnumerable<T>`
  over a live carrier event feed consumed with `await foreach`. Pure BCL (no `System.Linq.Async`:
  `IAsyncEnumerable` + `await foreach` are in-framework; the example uses no LINQ operator over the
  async sequence, so no package). Cite MS Learn (iterators / `yield`; generate-and-consume async
  streams / `await foreach`) + GoF Iterator.
- **ch24 Visitor / Memento / Interpreter** (`_docs/24-visitor-memento-interpreter.md`, Rating/Billing
  domain). Tariff rules modeled as an EXPRESSION TREE = the real Interpreter (the thin-coverage
  pattern, backed by a runnable example): a `TariffRule` AST that is both evaluated and compiled via
  `System.Linq.Expressions` into an executable delegate. Pattern matching (a `switch` expression over
  the same closed type hierarchy) is the Visitor alternative. Booking amendment rollback is Memento.
  Pure BCL (`System.Linq.Expressions` is in-framework; no package). Cite GoF (Visitor, Memento,
  Interpreter) + MS Learn (pattern matching / switch expression; expression trees).

## Confirmed facts

- Part slug `coordinating-traversing` (both stubs carry it; verified). Edit body in place only.
- Each stub's primary include is `{% include excalidraw.html name="<23|24-...>" caption="Figure NN.1:
  TODO caption." %}` whose primary `name` MUST stay exactly `23-iterator-yield-linq-iasyncenumerable`
  and `24-visitor-memento-interpreter` (confirmed). Replace ONLY the caption text, keep the
  `Figure NN.1: ` prefix. `grep -c "TODO caption"` is 1 per stub today; MUST be 0 at the end.
- Each stub has a `## When not to use it` section; the single `> **AI sidebar.**` block lives there
  (exactly one per chapter, no more).
- `_data/diagrams.yml` currently has 42 entries; append from **#43** in chapter order. One diagram
  agent owns diagrams.yml this iteration. Amber accent; light-fill emphasis boxes; no baked-in
  chapter numbers in the art.
- Template example layout: `examples/22-mediator-and-chain-of-responsibility/` (library `.csproj`
  with `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**`, nested `*.Tests/` xUnit v3, a
  `.slnx`, a README). Shared settings inherited from `examples/Directory.Build.props`
  (net10.0, LangVersion 14.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel
  latest-recommended). Root `global.json` already opts in to `Microsoft.Testing.Platform`.
- Async idiom to copy verbatim: library awaits use `.ConfigureAwait(false)` (as in
  `examples/14-.../AsyncAmendmentQueue.cs`); tests pass `TestContext.Current.CancellationToken`
  (xUnit1051, as in `examples/14-.../AsyncAmendmentQueueTests.cs`); consumers use `await foreach`
  (the `yield return this;` iterator in `examples/20-.../ShipmentParcel.cs` is a working in-repo
  reference for the synchronous iterator idiom).
- Dependency survey: BOTH examples are pure BCL. No runtime package for either. Test csprojs pin
  the four packages only. (If, while building, ch23's async feed genuinely needs a LINQ operator
  over the async sequence, survey and justify `System.Linq.Async` before adding; default is NOT to
  add it.) Confirm no reader-facing namespace collision: ch21 already owns `ConsignmentTracking` and
  ch17 owns `TariffTables`, so ch23 uses `TrackingHistory` and ch24 uses `TariffRules`.

## LOCKED type names (Wave A builds verbatim; Wave B diagrams copy from the green code)

### ch23 `examples/23-iterator-yield-linq-iasyncenumerable/` (namespace `TrackingHistory`, pure BCL)
Project `TrackingHistory.csproj`; tests `TrackingHistory.Tests`; `TrackingHistory.slnx`.
- `enum TrackingStage { Booked, PickedUp, InTransit, CustomsHold, OutForDelivery, Delivered }`
  (distinct from ch21's `ConsignmentStatus`).
- `sealed record TrackingEvent(string ConsignmentId, TrackingStage Stage, DateTimeOffset OccurredAt,
  string Location)` — one scan in the history / feed.
- `sealed record TrackingPage(int PageNumber, IReadOnlyList<TrackingEvent> Events)` — one page.
- **Synchronous iterator half** `ConsignmentHistory`:
  - ctor from `IEnumerable<TrackingEvent>` (stored as `IReadOnlyList<TrackingEvent>`).
  - `IEnumerable<TrackingPage> Paginate(int pageSize)` — PUBLIC method that EAGERLY validates
    `pageSize` (`ArgumentOutOfRangeException` for <= 0) then delegates to the private iterator. This
    split is the headline deferred-execution lesson: a `yield`-method body is lazy, so validation
    must live in a non-iterator wrapper to throw on call rather than on first enumeration.
  - `private IEnumerable<TrackingPage> PaginateIterator(int pageSize)` — the `yield return` iterator
    (the compiler state machine): buffers into a `List<TrackingEvent>`, `yield return`s a full page,
    and yields a trailing partial page at the end; no trailing empty page when the count divides
    evenly.
  - `IEnumerable<TrackingStage> StagesReached()` — LINQ over the history
    (`_events.Select(e => e.Stage).Distinct()`), demonstrating LINQ operators composing on top of
    (and themselves being) deferred iterators.
- **Async stream half:**
  - `interface ICarrierFeed { IAsyncEnumerable<TrackingEvent> StreamAsync(string consignmentId,
    CancellationToken cancellationToken); }`
  - `sealed class CarrierEventFeed : ICarrierFeed` — `public async IAsyncEnumerable<TrackingEvent>
    StreamAsync(string consignmentId, [EnumeratorCancellation] CancellationToken cancellationToken)`:
    iterates an injected `IReadOnlyList<TrackingEvent>`, `await Task.Yield().ConfigureAwait(false)`
    (or `await Task.Delay(..., cancellationToken).ConfigureAwait(false)` kept deterministic) between
    items so each element is genuinely produced asynchronously, calls
    `cancellationToken.ThrowIfCancellationRequested()`, and `yield return`s each event in order. The
    `[EnumeratorCancellation]` attribute is a required idiom so a caller's `.WithCancellation(ct)`
    reaches the token.
  - `sealed class LiveTrackingReader` — `public async Task<IReadOnlyList<TrackingEvent>>
    CollectAsync(ICarrierFeed feed, string consignmentId, CancellationToken cancellationToken)` using
    `await foreach (var e in feed.StreamAsync(...).WithCancellation(cancellationToken)
    .ConfigureAwait(false))`; plus `public async Task<IReadOnlyList<TrackingEvent>> CollectUntilAsync(
    ICarrierFeed feed, string consignmentId, TrackingStage stop, CancellationToken cancellationToken)`
    that `break`s on the stop stage (proving lazy async consumption halts the producer early).
- **Tests** (`TrackingHistory.Tests`, split `PaginationTests.cs` + `AsyncStreamTests.cs`), ~8:
  1. `Paginate_yields_fixed_size_pages_and_a_final_partial_page` (5 events, size 2 -> pages of 2,2,1;
     page numbers 1,2,3).
  2. `Paginate_with_an_evenly_dividing_size_has_no_trailing_empty_page` (4 events, size 2 -> 2 pages).
  3. `Paginate_validates_page_size_eagerly_before_enumeration` (`Paginate(0)` throws on the call,
     NOT on first `MoveNext`; proves the wrapper/iterator split).
  4. `Paginate_is_deferred_until_enumerated` (building the enumerable does no paging work; e.g. a
     history whose events are observed only once enumeration begins / assert via re-enumeration count
     or a spy source).
  5. `StagesReached_returns_distinct_stages_in_first_seen_order`.
  6. `StreamAsync_yields_every_event_in_order` (await foreach collects all, order preserved).
  7. `CollectUntilAsync_stops_the_feed_early_on_break` (only events up to and including the stop
     stage are consumed).
  8. `StreamAsync_honours_cancellation` (a cancelled token -> `OperationCanceledException` /
     `TaskCanceledException` from the await foreach; proves `[EnumeratorCancellation]` +
     `WithCancellation` wiring). Tests use `TestContext.Current.CancellationToken` for the happy path
     and an explicit `CancellationTokenSource` for the cancellation case.

### ch24 `examples/24-visitor-memento-interpreter/` (namespace `TariffRules`, pure BCL)
Project `TariffRules.csproj`; tests `TariffRules.Tests`; `TariffRules.slnx`.
- Context: `sealed record Shipment(decimal WeightKilograms, decimal DeclaredValue, string
  Destination)`. Amounts are `decimal` throughout (keeps expression-tree arithmetic clean).
- **Interpreter grammar = the expression tree (closed `TariffRule` hierarchy):**
  - `public abstract record TariffRule;`
  - `public sealed record FlatFee(decimal Amount) : TariffRule;`
  - `public sealed record PerKilogram(decimal Rate) : TariffRule;`
  - `public sealed record Surcharge(TariffRule Inner, decimal Percent) : TariffRule;` (composite).
  - `public sealed record Sum(IReadOnlyList<TariffRule> Rules) : TariffRule;` (composite;
    expose `IReadOnlyList`, not `List`/array -> CA1002/CA1819).
- **Visitor alternative = pattern-matching evaluator:**
  - `public static class TariffEvaluator` with `public static decimal Evaluate(TariffRule rule,
    Shipment shipment)` implemented as a recursive `switch` expression over the closed hierarchy
    (`rule switch { FlatFee f => f.Amount, PerKilogram p => p.Rate * shipment.WeightKilograms,
    Surcharge s => Evaluate(s.Inner, shipment) * (1 + s.Percent / 100m), Sum s =>
    s.Rules.Sum(r => Evaluate(r, shipment)), _ => throw new ... }`). The discard arm is required
    because the compiler cannot prove exhaustiveness over a record hierarchy (CS8509); use
    `_ => throw new UnreachableException(...)` (or `ArgumentOutOfRangeException(nameof(rule))`).
- **The real Interpreter realized with `System.Linq.Expressions`:**
  - `public static class TariffCompiler` with `public static Func<Shipment, decimal>
    Compile(TariffRule rule)`: declares `ParameterExpression shipment =
    Expression.Parameter(typeof(Shipment), "shipment")`, walks the same AST via a private
    `Expression Build(TariffRule rule, ParameterExpression shipment)` (constant for `FlatFee`,
    `Expression.Multiply` of the rate constant and `Expression.Property(shipment,
    nameof(Shipment.WeightKilograms))` for `PerKilogram`, multiply-by-(1+percent/100) for `Surcharge`,
    an `Expression.Add` fold for `Sum`), then
    `Expression.Lambda<Func<Shipment, decimal>>(body, shipment).Compile()`. This IS the Interpreter:
    the tariff AST is interpreted into executable code. Same closed hierarchy, same discard-arm need.
- **Memento (booking amendment rollback):**
  - `public enum ServiceLevel { Economy, Standard, Express }`
  - `public sealed record BookingLine(string Sku, int Quantity);` (immutable element).
  - `public sealed class BookingOriginator` — mutable state: `ServiceLevel ServiceLevel`,
    `decimal WeightKilograms`, and `IReadOnlyList<BookingLine> Lines` (backed by a private
    `List<BookingLine>`). Mutators `SetServiceLevel`, `SetWeight`, `AddLine`. `BookingMemento Save()`
    captures a snapshot with a COPIED list (`[.. _lines]`); `void Restore(BookingMemento memento)`
    rolls state back and rebuilds the internal list from the snapshot.
  - `public sealed record BookingMemento(ServiceLevel ServiceLevel, decimal WeightKilograms,
    IReadOnlyList<BookingLine> Lines)` — opaque snapshot. The deep-vs-shallow lesson: because
    `BookingLine` is an immutable record, copying the LIST is a sufficient deep copy of the state;
    if the elements were mutable each would have to be cloned. State this in prose and prove it with
    an isolation test.
  - `public sealed class AmendmentCaretaker` — holds a `Stack<BookingMemento>`;
    `void Checkpoint(BookingOriginator originator)` pushes `originator.Save()`;
    `void Rollback(BookingOriginator originator)` pops and `Restore`s (throws
    `InvalidOperationException` when empty). The caretaker never reads memento internals.
- **Tests** (`TariffRules.Tests`, split `TariffInterpreterTests.cs` + `TariffEvaluatorTests.cs` +
  `MementoTests.cs`), ~11:
  1. `Compiled_flat_fee_returns_the_fee`.
  2. `Compiled_per_kilogram_scales_with_weight`.
  3. `Compiled_surcharge_applies_percent_over_inner`.
  4. `Compiled_sum_adds_component_rules`.
  5. `Compiled_delegate_matches_the_pattern_matching_evaluator` (for a nested
     `Sum`/`Surcharge`/`PerKilogram`/`FlatFee` tree, `TariffCompiler.Compile(tree)(shipment) ==
     TariffEvaluator.Evaluate(tree, shipment)` — the core agreement test proving the two readings of
     the same AST are equivalent).
  6. `Pattern_match_evaluates_a_nested_tree` (the switch-expression evaluator on a deep tree).
  7. `Pattern_match_handles_each_rule_kind` ([Theory]/TheoryData over the four leaf/composite kinds).
  8. `Restore_rolls_back_service_level_and_weight`.
  9. `Rollback_through_caretaker_restores_the_last_checkpoint`.
  10. `Memento_snapshot_is_isolated_from_later_mutation` (checkpoint, then `AddLine`, then rollback ->
      the added line is gone AND mutating after the checkpoint did not alter the captured snapshot;
      proves the list was copied, not aliased — the deep/shallow proof).
  11. `Rollback_with_no_checkpoint_throws` (`InvalidOperationException`).

## Waves

- **Wave A (code, 2 agents parallel — one per chapter):** build each example green. Library csproj
  mirrors the template: `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**`; nested `*.Tests/`
  csproj pins the four packages (xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1,
  Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0), sets `IsTestProject` true + `IsPackable` false
  + `NoWarn $(NoWarn);CA1707`, and OMITS `TestingPlatformDotnetTestSupport`; each example has its own
  `.slnx`; each ships a README mirroring ch22's. No runtime package for either (pure BCL; do not add
  `System.Linq.Async`). Library awaits use `.ConfigureAwait(false)`; async iterator carries
  `[EnumeratorCancellation]`; tests pass `TestContext.Current.CancellationToken` for happy paths and
  an explicit `CancellationTokenSource` for cancellation. Run `dotnet test` per example and CAPTURE
  the real summary, path-sanitized (0 `/home/`, 0 username) for the "Build, run, observe" quote.
  Tests exactly as listed above (ch23 ~8, ch24 ~11). Wrap any fenced `{{`/`}}` in `{% raw %}` if it
  ever reaches a `.md` (collection-expression `[.. _lines]` and interpolated strings are the risk).
- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml from #43):** copy LOCKED real
  class names from the compiled examples (r06 name-drift lesson). Generate paired SVG + `.excalidraw`
  per figure, amber accent, light-fill emphasis boxes, no baked-in chapter numbers. Append to
  `_data/diagrams.yml` in chapter order:
  - **#43 `23-iterator-yield-linq-iasyncenumerable`** (primary; name MUST match the ch23 stub
    include): the `yield return` dissolve — `ConsignmentHistory.Paginate` -> compiler state machine ->
    `TrackingPage` sequence, lazily pulled.
  - **#44 `23-async-stream-await-foreach`** (secondary): `CarrierEventFeed.StreamAsync`
    (`IAsyncEnumerable<TrackingEvent>`, `[EnumeratorCancellation]`) consumed by
    `LiveTrackingReader` via `await foreach` + `WithCancellation`.
  - **#45 `23-deferred-execution`** (density, expected since siblings run past 2500): eager-validation
    wrapper vs lazy iterator body; LINQ composing over the iterator.
  - **#46 `24-visitor-memento-interpreter`** (primary; name MUST match the ch24 stub include): the
    `TariffRule` AST (FlatFee / PerKilogram / Surcharge / Sum) read two ways — `TariffEvaluator`
    (pattern-match switch = Visitor alternative) and `TariffCompiler` (compiled
    `Expression<Func<Shipment,decimal>>` = Interpreter).
  - **#47 `24-pattern-match-and-compiled-tree`** (secondary): the two evaluators over one closed
    hierarchy and the agreement between them.
  - **#48 `24-memento-checkpoint-rollback`** (density/tertiary): `BookingOriginator` ->
    `BookingMemento` snapshot, `AmendmentCaretaker` stack, `Restore`.
  Real captions describing only types that exist in the green code.
- **Wave C (prose, 2 agents, after code + figures — one per chapter):** write the full spine and
  replace the TODO caption text (keep the `Figure NN.1: ` prefix). Spine order: Learning objectives
  -> Why this matters -> primary FIGURE (after "Why this matters") -> body (with secondary/density
  figures placed where density demands) -> `## Build, run, observe` (quotes the REAL path-sanitized
  `dotnet test` summary, 0 `/home/`, 0 username) -> `## When not to use it` (exactly ONE
  `> **AI sidebar.**`) -> `## For deeper coverage` (GoF via the Pearson publisher link as in ch22 +
  MS Learn). ch23: describe `yield`/the state machine, deferred execution + eager validation,
  LINQ-as-iterators, `IAsyncEnumerable` + `await foreach` + cancellation, all POSITIVELY and
  temporally. ch24: Interpreter as the tariff AST compiled via `System.Linq.Expressions`, pattern
  matching/switch as the Visitor alternative over a closed hierarchy, Memento rollback and the
  deep-vs-shallow copy point. Zero em-dash; no rule-of-three; 0 ban-tier (no editorializing kickers);
  idiomatic C#14 only; NO cross-language; cross-chapter refs light; wrap `{{`/`}}` fenced blocks in
  `{% raw %}`. Body >= 2000 words HARD floor, 2500 target, no ceiling; add the density figure if a
  body runs past ~2500.
- **Wave D (validate):** `lgtm-idiomatic-review` (0 hits both examples); `lgtm-professional-voice`
  (0 ban-tier both, grep the editorializing families); em-dash == 0 across `_docs` + `.cs` + README;
  `grep -c "TODO caption"` across both docs == 0; diagram name-drift gate (every SVG label + caption
  token present in the example source); NO-CROSS-LANGUAGE gate (grep both docs, both example trees
  .cs + README, the new diagrams.yml captions #43-48, and the new SVGs for
  `Java|JVM|erasure|C\+\+|Spring` == 0); CROSS-CHAPTER gate (no numeric cross-refs, no "the X
  chapter"/title pointers); `dotnet test` per example (expect ch23 ~8, ch24 ~11) + `scripts/verify.sh`
  (both auto-discovered, all green); `bundle exec jekyll build` clean (all six figures render); word
  counts recorded; write the verification log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if >~2500); full spine with the primary
figure AFTER "Why this matters"; figure pair(s) on disk + registered in diagrams.yml with matching
`name` and REAL captions (no TODO); example `dotnet test` green and included by `scripts/verify.sh`;
tests assert behaviour (not just build-exits-0); "Build, run, observe" quotes real path-sanitized
output (0 `/home/`, 0 username); exactly one AI sidebar in "When not to use it"; zero em-dash across
docs + .cs + README; no rule-of-three; 0 ban-tier; idiomatic C#14 only; NO cross-language; cross-
chapter refs light; no numeric cross-refs; every figure/caption type exists in the example; cites GoF
(Pearson link) + MS Learn.
- ch23 proves: fixed-size pages + final partial page; no trailing empty page on even division; eager
  page-size validation before enumeration; paging deferred until enumerated; distinct stages in
  first-seen order (LINQ); async stream yields every event in order; early `break` stops the feed;
  cancellation throws. Async idiom honoured (`ConfigureAwait(false)` in library, `[EnumeratorCancel-
  lation]`, `WithCancellation`, `TestContext.Current.CancellationToken` in tests).
- ch24 proves: compiled delegate for each rule kind (flat fee, per-kilogram, surcharge, sum); the
  compiled Interpreter delegate matches the pattern-matching evaluator on a nested tree; the switch
  evaluator handles each rule kind over the closed hierarchy; memento restores service level + weight;
  caretaker rollback restores the last checkpoint; the snapshot is isolated from later mutation
  (deep/shallow proof); empty rollback throws.
Iteration: jekyll clean; diagrams.yml appended from #43 in chapter order (#43-48); both test csprojs
omit `TestingPlatformDotnetTestSupport` and pin the four packages (IsTestProject, IsPackable false,
NoWarn CA1707); each example own `.slnx`; both examples pure BCL (no runtime package; no
`System.Linq.Async`); no cross-language token anywhere; ch23 primary figure name stays
`23-iterator-yield-linq-iasyncenumerable` and ch24 primary stays `24-visitor-memento-interpreter`;
Part 8 COMPLETE (ch21-24).

## Risks

- **ch23 async:** CA2007 (`ConfigureAwait(false)` required on every library await, including inside
  the async iterator; tests may await without it, consistent with the green ch14 suite);
  `[EnumeratorCancellation]` MUST decorate the iterator's token param or `.WithCancellation(ct)` is a
  silent no-op (cancellation test is the guard); xUnit1051 -> `TestContext.Current.CancellationToken`;
  keep the async feed deterministic (`Task.Yield()` or a token-aware `Task.Delay`, no wall-clock
  flakiness).
- **ch23 iterator deferred-execution gotchas:** a `yield`-method defers its ENTIRE body, so argument
  validation must live in a non-iterator public wrapper that delegates to a private iterator (the
  eager-validation test enforces this split); iterators are cold/re-enumerable; LINQ operators are
  themselves deferred (prove with the distinct-stages test). Trailing partial vs empty page off-by-one.
- **ch24 expression trees:** `System.Linq.Expressions` is in-framework (no package); keep arithmetic
  all-`decimal` so `Expression.Multiply`/`Expression.Add` bind to the decimal operators;
  `Expression.Property(param, nameof(Shipment.WeightKilograms))` for member access;
  `Expression.Lambda<Func<Shipment,decimal>>(...).Compile()`; the compiled-matches-evaluator test is
  the correctness guard.
- **ch24 closed hierarchy + switch expressions:** the compiler cannot prove exhaustiveness over an
  abstract record hierarchy, so every `switch` expression over `TariffRule` needs a discard arm
  (`_ => throw ...`) or CS8509 fails under warnings-as-errors; CA1002/CA1819 (expose `IReadOnlyList`,
  not `List`/array, on `Sum.Rules`); records with an `IReadOnlyList` member use reference equality for
  that member (do not rely on memento/record value-equality over collections); CA1859 may nudge
  concrete-over-interface in internals (follow the analyzer).
- **ch24 Memento deep-vs-shallow copy:** `Save()` MUST copy the line list (`[.. _lines]`), not alias
  it; because `BookingLine` is immutable, copying the list is a sufficient deep copy (if elements
  were mutable, each would need cloning) — the isolation test proves it; `Restore` rebuilds the
  internal list rather than assigning the snapshot's reference.
- **All:** diagram name drift (diagrams generated AFTER green code with locked names + Wave D gate);
  the TODO-caption trap (the primary-figure include caption is owned by neither the diagram nor the
  prose agent by default — Wave C explicitly replaces it, Wave D greps `TODO caption` == 0); NO-CROSS-
  LANGUAGE slip is especially tempting at "expression trees" and "pattern matching" (stay positive
  and temporal, never "unlike Java/C++"); heavy cross-chapter refs; Liquid `{{ }}` in fenced blocks
  (`[.. _lines]` and interpolated strings); `.slnx`/`global.json` wiring; CA1707 test-only; path
  leaks in the quoted output; 2000-word floor.

## r11 verification log

Opus-validated 2026-10-31 (branch `release/r11`). Overall: PASS (one voice-repair round: 13 ban-tier
words swapped + "actually" filler trimmed from 10 lines to 1, re-scan 0 ban-tier).

**Build / test (all green):**
- `examples/23-iterator-yield-linq-iasyncenumerable` `dotnet test`: 8/8.
- `examples/24-visitor-memento-interpreter` `dotnet test`: 14/14.
- `scripts/verify.sh`: both new examples auto-discovered and green alongside the rest (21 suites, 0 failures).
- `bundle exec jekyll build`: clean, all six r11 figures render.

**Content:** both chapters full spine in order (figure after "Why this matters"); "Build, run,
observe" quotes real counts (8/14), path-sanitized (0 `/home/`, 0 username); exactly one AI sidebar
each in "When not to use it"; body prose 3123 (ch23) / 3066 (ch24), each with density figures; 0
em-dash; citations learn.microsoft.com + GoF (pearson).

**NO CROSS-LANGUAGE:** grep for Java/JVM/erasure/C++/Spring across both docs, both example trees
(.cs + README), the six new diagrams.yml captions (#43-48), and the six SVGs = ZERO.

**CROSS-CHAPTER REFS LIGHT:** no numeric cross-refs; no title/topic pointers; all "chapter" hits are
self-references.

**Diagrams (name-accuracy gate):** six figures (#43-48, chapter order) each paired on disk,
registered, included in the right chapter; every SVG label + caption token verified present in the
example source. No drift. (Generated after green code with locked names; two labels corrected during
Wave B self-check: `ct` -> `cancellationToken`, and a non-literal `MoveNextAsync` label reworded.)

**Code idiom:** `lgtm-idiomatic-review` 0 hits on both. ch23: `[EnumeratorCancellation]` on the async
iterator token, `ConfigureAwait(false)` on library awaits, eager-validate `Paginate` wrapper over the
lazy `PaginateIterator`, `.WithCancellation` wiring, `IReadOnlyList` exposure. ch24: closed
`TariffRule` record hierarchy with required discard arms (CS8509), `System.Linq.Expressions` compile
with all-`decimal` arithmetic, `Sum.Rules` as `IReadOnlyList`, `Save()`/`Restore` copy the line list
(`[.. _lines]`). Both test csprojs set IsTestProject + IsPackable false + NoWarn CA1707, pin the four
packages, omit TestingPlatformDotnetTestSupport; each example own `.slnx`; both examples pure BCL (no
runtime package, no System.Linq.Async). Em-dash across both examples' .cs + README = 0.

**Voice:** `lgtm-professional-voice` 0 ban-tier on both after the repair round (watch-tier left as-is
per prior iterations). `grep -c "TODO caption"` = 0.

**Part 8 is COMPLETE** (ch21-24).
