---
title: "r12 plan — Part 9 (ch25, ch26, ch29)"
output: false
render_with_liquid: false
---

# r12 — author ch25, ch26, ch29 (Part 9: Idiomatic C#, pure-BCL subset)

Opus-planned 2026-10-31. Branch `release/r12` (based on main@8c2dfc2). Three pure-BCL language-feature
chapters from Part 9; the two Roslyn build-time chapters (ch27 source generators, ch28 analyzers) are
deferred to r13 because they share a new netstandard2.0 Microsoft.CodeAnalysis toolchain. This is the
FIRST research-heavy part: the authority is Microsoft Learn + the C# language spec + runnable
experiments, NOT blog posts. Four-wave shape (A code, 3 parallel agents / B diagrams AFTER green code
with LOCKED names / C prose, 3 agents / D validate). Enforces: NO cross-language comparison (describe
extension members, expression trees, static abstract interface members, and generic math POSITIVELY
and temporally, never against Java/JVM/C++/CRTP/"policy-based design"/erasure/Spring/Smalltalk); keep
cross-chapter refs light (each chapter stands alone; ch26 must NOT lean on the already-merged ch24,
re-explain in place).

## Scope + example assignments (shipping domain, from the Notion per-chapter map)

- **ch25 Extension methods and C# 14 extension members** (`_docs/25-extension-members.md`, Carrier
  domain). Extending a carrier's SDK type you do NOT own, using C# 14 extension members: extension
  properties, static extension members, and extension operators, to decorate/adapt a type you cannot
  modify. Pure BCL (C# 14 extension members are a language feature, no package). Also shows a classic
  `this`-parameter extension method coexisting with the new `extension` block form (source/binary
  compatible). Cite Microsoft Learn (extension members, extension operators) + the C# 14 language
  spec; GoF only lightly (Adapter/Decorator in spirit, not the subject).
- **ch26 Expression trees: code as data** (`_docs/26-expression-trees.md`, Rating domain). The tariff
  rule engine authored as `System.Linq.Expressions.Expression<TDelegate>` data: rules written as
  inspectable expression trees, then compiled AND analysed/traversed/rewritten with an
  `ExpressionVisitor`, plus a parameter-rebinding compose and an inspection pass that reads referenced
  members the way a LINQ provider reads a tree. Pure BCL (`System.Linq.Expressions` is in-framework).
  CRITICAL DISTINCTION from the merged ch24: ch24's `TariffCompiler` only COMPILED a hand-rolled
  `TariffRule` AST into a delegate; ch26 is about real `Expression<TDelegate>` trees as inspectable
  data (building, VISITING, REWRITING, inspecting, compiling). Different namespace (`TariffExpressions`,
  not ch24's `TariffRules`) and all different type names (no `TariffRule`/`TariffCompiler`/`Shipment`).
  Cite Microsoft Learn (expression trees, `ExpressionVisitor`) + the language spec; GoF Visitor
  lightly (`ExpressionVisitor` is literally the Visitor shape), Interpreter not reused here.
- **ch29 static abstract members and generic math**
  (`_docs/29-static-abstract-members-and-generic-math.md`, Rating domain). `IRateable<TSelf>` with
  `static abstract` members over a `Money` value type, invoked through a generic type parameter, plus
  generic math using `System.Numerics` interfaces (`IAdditionOperators<,,>`, `IAdditiveIdentity<,>`,
  `IMultiplyOperators<,,>`, `IComparisonOperators<,,>`) so one generic aggregate works over both
  `Money` and the built-in numeric types that implement `INumber<T>`. Pure BCL (`System.Numerics` is
  in-framework). Cite Microsoft Learn (static abstract interface members, generic math) + the language
  spec; no GoF (not a GoF pattern). **MANDATORY REFRAME:** the stub's front-matter `description:`
  currently reads `"The managed-language answer to policy-based design, and the explicit bridge back to
  CRTP."` — CRTP and "policy-based design" are cross-language C++ concepts and VIOLATE the
  no-cross-language rule. Wave C MUST replace that line with a positive C#-only description (proposed:
  `"Constraining a generic over type-level operations with abstract static interface members and
  generic math over a custom value type."`). No CRTP, no "policy-based design", no cross-language
  anywhere in the final chapter.

## Confirmed facts

- Part slug `idiomatic-csharp` (all three stubs carry it in front matter; verified). Edit body in place.
- Branch `release/r12` already created off `main@8c2dfc2` (verified). SDK pin `10.0.113`, LangVersion
  `14.0`, Nullable on, warnings-as-errors, AnalysisLevel latest-recommended — all inherited from
  `examples/Directory.Build.props` (verified). Root `global.json` opts into `Microsoft.Testing.Platform`.
- Each stub's primary include is `{% include excalidraw.html name="<NN-...>" caption="Figure NN.1:
  TODO caption." %}`. Primary `name` MUST stay exactly `25-extension-members`, `26-expression-trees`,
  `29-static-abstract-members-and-generic-math` (verified). Replace ONLY the caption text, keeping the
  `Figure NN.1: ` prefix. `grep -c "TODO caption"` is 1 per stub today; MUST be 0 at the end.
- Each stub has a `## When not to use it` section; the single `> **AI sidebar.**` block lives there
  (exactly one per chapter, no more). Stubs also carry a leading `<!-- EXAMPLE ... -->` comment that
  already names the correct per-chapter example framing.
- `_data/diagrams.yml` currently has **48** entries; append from **#49** in chapter order. ONE diagram
  agent owns diagrams.yml this iteration. Amber accent; light-fill emphasis boxes with dark text; no
  baked-in chapter numbers; ~960x560 C4-style.
- Template example layout (verified from `examples/22-mediator-and-chain-of-responsibility/`): library
  `.csproj` with `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**</DefaultItemExcludes>`;
  nested `*.Tests/` xUnit v3 csproj pinning the four packages (xunit.v3 4.0.2,
  xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0), setting
  `IsTestProject` true + `IsPackable` false + `NoWarn $(NoWarn);CA1707`, and OMITTING
  `TestingPlatformDotnetTestSupport`; a top-level `.slnx` listing both projects; a README mirroring
  ch22's. All of net10.0/LangVersion/Nullable/ImplicitUsings/warnings-as-errors inherited from
  `examples/Directory.Build.props`.
- ch29's `description:` reframe (CRTP + "policy-based design" removal) is a DELIVERABLE, gated in Wave D.
- Namespace survey (verified via `grep -rh "^namespace" examples`): taken include `Consignments`,
  `ConsignmentRouting`, `ConsignmentTracking`, `ConsignmentAmendments`, `CarrierGateways`,
  `CarrierIntegration`, `CarrierProvisioning`, `RateCalculation`, `RateComposition`, `RateQuoting`,
  `RatingEngine`, `TariffRules` (ch24), `TariffTables` (ch17), `FreightValueTypes` (ch08, owns a `Money`),
  `BookingRequests`, `BookingAmendments`, `CustomsClearance`, `CustomsDocumentation`, `ManifestWriting`,
  `TrackingHistory` (ch23). The three fresh namespaces chosen below do NOT collide:
  `CarrierParcels` (ch25), `TariffExpressions` (ch26), `RateMath` (ch29). ch29 defines its OWN `Money`
  inside `RateMath` (independent of ch08's `FreightValueTypes.Money`; separate namespace, no clash).
- Dependency survey: all three are PURE BCL. C# 14 extension members are a language feature (no
  package); `System.Linq.Expressions` is in-framework; `System.Numerics` generic-math interfaces are
  in-framework. No runtime package for any of the three. Test csprojs pin the four packages only. If a
  genuine runtime package need surfaces during Wave A (it should not), survey upstream latest stable
  and justify before adding; default is NOT to add one.

## FEASIBILITY GATE (ch25, runs FIRST inside Wave A before anything else)

C# 14 "extension members" (the new `extension(Receiver)` block with extension properties, static
extension members, and extension operators) is a GA C# 14 feature on .NET 10, not pre-release. Before
building the full ch25 example, the ch25 Wave-A agent writes a throwaway spike inside the ch25 example
dir (or a scratch build target that is deleted before handback) that exercises, on the pinned SDK
10.0.113 / LangVersion 14.0: (1) an instance extension property, (2) a static extension member
(property + method), and (3) an extension operator (`operator +`) in an `extension(ReferenceType)`
block. Run `dotnet build`.
- If all three compile: build ch25 as specified below (full feature set).
- If a specific sub-feature does NOT compile on the pin: FALL BACK to the sub-features that do (classic
  `this`-parameter extension methods + whatever `extension`-block forms the pinned compiler accepts),
  drop the unsupported form from both the code and the prose, and document the pin honestly in the
  README and "Build, run, observe" ("extension operators require <...>; this chapter uses <...> on SDK
  10.0.113"). Do NOT claim a feature that does not build on the pin. Record the spike outcome in the
  Wave A handback and the Wave D log. Operator constraint to honour per the spec: the receiver is a
  reference type, so the extension-operator receiver parameter is a value parameter (not `ref`), and
  `Nullable<T>` is not a valid operand type.

## LOCKED type names (Wave A builds verbatim; Wave B diagrams copy from the green code)

### ch25 `examples/25-extension-members/` (namespace `CarrierParcels`, pure BCL)
Project `CarrierParcels.csproj`; tests `CarrierParcels.Tests`; `CarrierParcels.slnx`; README.
- Simulated UNOWNED carrier SDK type (the type we cannot modify; place in a nested
  `namespace CarrierParcels.Vendor` with a file comment stating it stands in for a third-party SDK
  assembly): `public sealed class CarrierShipment` with constructor-set, read-only properties
  `int WeightGrams`, `string DestinationCode`, `int DeclaredValueMinorUnits`. No methods we add; it is
  "sealed and owned by someone else".
- `public static class CarrierShipmentExtensions` (top-level, non-generic, static — required for
  extension blocks):
  - `extension(CarrierShipment shipment)` instance block:
    - extension property `public decimal WeightKilograms => shipment.WeightGrams / 1000m;`
    - extension property `public bool IsHeavy => shipment.WeightGrams > 30_000;`
    - extension method `public CarrierShipment WithAddedWeight(int grams)` returning a NEW
      `CarrierShipment` (adapts/decorates without mutating the unowned instance).
  - `extension(CarrierShipment)` static block:
    - static extension property `public static CarrierShipment Empty => new(0, "", 0);`
    - static extension method `public static CarrierShipment FromKilograms(decimal kilograms,
      string destinationCode)` (converts to grams, rounds).
  - extension operator (in an `extension(CarrierShipment shipment)` block; value-parameter receiver):
    `public static CarrierShipment operator +(CarrierShipment left, CarrierShipment right)` combining
    the two shipments' weights and declared values, destination taken from `left`.
  - classic coexisting form (same static class, outside the block): `public static string
    NormalisedDestination(this CarrierShipment shipment)` (upper-case, trimmed) — demonstrates the
    `this`-parameter method living alongside the new block form, both compiling to the same IL.
- **Tests** (`CarrierParcels.Tests`, split `ExtensionMemberTests.cs` + `ExtensionOperatorTests.cs`), ~8:
  1. `WeightKilograms_converts_grams_to_kilograms`.
  2. `IsHeavy_is_true_above_the_threshold_and_false_below` ([Theory]/TheoryData around 30_000 g).
  3. `WithAddedWeight_returns_an_adapted_shipment_and_leaves_the_original_unchanged` (immutability of
     the unowned instance).
  4. `Empty_static_member_has_zero_weight`.
  5. `FromKilograms_builds_a_shipment_with_the_expected_grams`.
  6. `Operator_plus_combines_two_shipments_weights_and_values`.
  7. `NormalisedDestination_classic_extension_method_upper_cases_and_trims` (proves classic form
     coexists with the block form).
  8. `Extension_members_resolve_on_the_unowned_sealed_type` (a behaviour test that calls an instance
     extension property + the static member + operator on `CarrierShipment` to prove decoration of a
     type we never modified). If the FEASIBILITY GATE fell back, drop test 6 and any operator mention
     and record the fallback.

### ch26 `examples/26-expression-trees/` (namespace `TariffExpressions`, pure BCL)
Project `TariffExpressions.csproj`; tests `TariffExpressions.Tests`; `TariffExpressions.slnx`; README.
DISTINCT from ch24 in every name (no `TariffRule`, `TariffCompiler`, `TariffEvaluator`, `Shipment`).
- Context: `public sealed record RateRequest(decimal WeightKilograms, decimal DeclaredValue,
  string Destination, bool Hazardous)`. Amounts `decimal` so `Expression` arithmetic binds to the
  decimal operators cleanly.
- `public static class TariffExpressionLibrary` — sample rules AUTHORED AS EXPRESSION DATA (the "code
  as data" headline): e.g. `public static Expression<Func<RateRequest, decimal>> WeightRate => r =>
  r.WeightKilograms * 2.50m;` and `public static Expression<Func<RateRequest, decimal>> ValueRate =>
  r => r.DeclaredValue * 0.015m;` and a predicate `public static Expression<Func<RateRequest, bool>>
  IsHeavy => r => r.WeightKilograms > 20m;`.
- `public sealed class RateDescriber : ExpressionVisitor` — walks a rule and builds a readable string
  (overrides `VisitBinary`, `VisitMember`, `VisitConstant`, `VisitParameter`); public entry
  `public string Describe(Expression<Func<RateRequest, decimal>> rule)`. This is the "read the tree"
  lesson.
- `public sealed class ReferencedPropertyCollector : ExpressionVisitor` — overrides `VisitMember` to
  gather which `RateRequest` properties a rule touches; public
  `public IReadOnlyCollection<string> Collect(Expression rule)`. This IS how a LINQ provider inspects
  a tree without executing it (translate-not-run).
- `public sealed class RateInflator : ExpressionVisitor` — overrides `VisitConstant` to scale every
  `decimal` constant by a factor (leaving non-`decimal` constants untouched); public
  `public Expression<Func<RateRequest, decimal>> Inflate(Expression<Func<RateRequest, decimal>> rule,
  decimal factor)` returning a REWRITTEN tree to compile. This is the "rewrite the tree" lesson.
- `public sealed class ParameterReplacer : ExpressionVisitor` — rebinds one `ParameterExpression` to
  another (overrides `VisitParameter`); used by the composer below. The classic parameter-unification
  problem that LINQ composition must solve.
- `public static class PredicateComposer` — `public static Expression<Func<RateRequest, bool>> And(
  Expression<Func<RateRequest, bool>> left, Expression<Func<RateRequest, bool>> right)` builds a new
  tree with `Expression.AndAlso`, using `ParameterReplacer` to unify the two lambdas onto one
  parameter, then `Expression.Lambda<Func<RateRequest, bool>>(...)`. This is the "build a tree with the
  factory API" lesson.
- Compilation is via `rule.Compile()` at the call sites / in tests (every tree is both inspectable and
  executable). No custom compiler type (that was ch24; here the framework's `Expression<T>.Compile()`
  is the point).
- **Tests** (`TariffExpressions.Tests`, split `DescribeAndInspectTests.cs` + `RewriteAndComposeTests.cs`),
  ~10:
  1. `Authored_rule_compiles_and_computes_the_rate` (baseline: `WeightRate.Compile()(request)`).
  2. `Describe_renders_a_readable_string_for_a_weight_rule`.
  3. `Describe_handles_a_composed_binary_tree`.
  4. `ReferencedPropertyCollector_finds_the_properties_a_rule_touches` (e.g. `WeightKilograms`).
  5. `ReferencedPropertyCollector_reads_without_executing` (collects from a rule it never compiles).
  6. `Inflate_scales_decimal_constants_and_the_rewritten_tree_compiles_to_the_scaled_result`.
  7. `Inflate_leaves_non_decimal_constants_untouched` (a rule mixing a `decimal` rate and an `int`/`bool`).
  8. `ParameterReplacer_rebinds_a_lambda_onto_a_new_parameter`.
  9. `And_composes_two_predicates_over_one_parameter` (compiled result honours both conditions).
  10. `And_composition_requires_both_conditions` ([Theory]/TheoryData true/true, true/false, etc.).

### ch29 `examples/29-static-abstract-members-and-generic-math/` (namespace `RateMath`, pure BCL)
Project `RateMath.csproj`; tests `RateMath.Tests`; `RateMath.slnx`; README.
- `public readonly record struct Money(decimal Amount)` — single implicit currency kept out to keep
  generic-math arithmetic clean (state the single-currency simplification in prose), implementing the
  `System.Numerics` generic-math interfaces it needs:
  - `IAdditionOperators<Money, Money, Money>` (`operator +`)
  - `IAdditiveIdentity<Money, Money>` (`static Money AdditiveIdentity => new(0m);`)
  - `IMultiplyOperators<Money, decimal, Money>` (`operator *` by a `decimal` factor)
  - `IComparisonOperators<Money, Money, bool>` (`< <= > >=`; records already supply `==`/`!=`, add
    `IComparisonOperators` members and `IComparable<Money>` as the analyzer requires).
  - Provide CA2225 friendly-named alternatives (`Add`, `Multiply`) for the operators, or scope-suppress
    CA2225 in the file with a justified `SuppressMessage` if named alternatives are noise — follow the
    analyzer, do not fight warnings-as-errors.
- `public interface IRateable<TSelf> where TSelf : IRateable<TSelf>` — the static-abstract contract:
  - `static abstract Money BaseRate { get; }` (static abstract property)
  - `static abstract Money RateOf(TSelf shipment);` (static abstract method)
- Concrete shippable types each implementing `IRateable<Self>` with their OWN static members:
  - `public sealed record ParcelShipment(decimal WeightKilograms) : IRateable<ParcelShipment>`
  - `public sealed record FreightPallet(decimal WeightKilograms, int PalletCount) :
    IRateable<FreightPallet>`
  - `public sealed record DocumentEnvelope(int PageCount) : IRateable<DocumentEnvelope>`
  Each supplies `static Money BaseRate => ...;` and `static Money RateOf(Self s) => ...;`.
- `public static class RateBook` — generic dispatch through the type parameter (the headline):
  - `public static Money Quote<T>(T shipment) where T : IRateable<T> => T.RateOf(shipment);`
    (calls a static abstract member via `T.RateOf`).
  - `public static Money BaseRateOf<T>() where T : IRateable<T> => T.BaseRate;`
- `public static class Totals` — generic math over any additive type:
  - `public static T Sum<T>(IEnumerable<T> values) where T : IAdditionOperators<T, T, T>,
    IAdditiveIdentity<T, T>` folding from `T.AdditiveIdentity`. Works over `Money` AND built-in numeric
    types (`decimal`, `int`) that implement `INumber<T>` — the generic-math proof.
- **Tests** (`RateMath.Tests`, split `MoneyGenericMathTests.cs` + `StaticAbstractDispatchTests.cs`), ~10:
  1. `Money_adds_through_the_addition_operator`.
  2. `Money_additive_identity_is_zero`.
  3. `Money_scales_by_a_decimal_factor`.
  4. `Money_comparison_operators_order_amounts` ([Theory]).
  5. `Sum_totals_a_sequence_of_money` (generic math over `Money`).
  6. `Sum_totals_a_sequence_of_decimals` (same generic `Sum<T>` over a built-in `INumber<T>` — proves
     the aggregate is not Money-specific).
  7. `Quote_dispatches_to_the_concrete_types_static_RateOf` (ParcelShipment).
  8. `Quote_uses_each_types_own_base_rate` ([Theory]/TheoryData over the three shipment types via a
     small generic helper, or three explicit cases).
  9. `BaseRateOf_reads_a_static_abstract_member_through_the_type_parameter`.
  10. `Total_quote_over_a_set_sums_each_shipments_rate` (combines `RateBook.Quote` + `Totals.Sum` over
      a collection of one shipment type).

## Waves

- **Wave A (code, 3 agents parallel — one per chapter):** build each example green.
  - ch25 agent runs the FEASIBILITY GATE first (above); then builds per the LOCKED names, honouring the
    fallback rule if any sub-feature fails on the pin.
  - Library csproj mirrors the template:
    `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**</DefaultItemExcludes>`; nested `*.Tests/`
    csproj pins the four packages (xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1,
    Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0), sets `IsTestProject` true + `IsPackable` false
    + `NoWarn $(NoWarn);CA1707`, and OMITS `TestingPlatformDotnetTestSupport`; each example its own
    `.slnx`; each ships a README mirroring ch22's.
  - No runtime package for any of the three (pure BCL; do not add `System.Linq.Async`, `System.Reactive`,
    or any CodeAnalysis package — those are out of scope).
  - Tests EXACTLY as listed (ch25 ~8, ch26 ~10, ch29 ~10); behaviour-asserting, never build-exits-0.
    Tests use `TestContext.Current.CancellationToken` only where an API takes a token (none of these
    three are async, so this is unlikely; note it only if an analyzer demands it).
  - Run `dotnet test` per example and CAPTURE the real summary, path-sanitized (0 `/home/`, 0 username)
    for the "Build, run, observe" quote. Wrap any fenced `{{`/`}}` in `{% raw %}` if it reaches a `.md`
    (interpolated strings, collection expressions, `Expression.Lambda<...>` double-brace risk).
  - Each agent reports: the real `dotnet test` summary, the final LOCKED names actually built (so Wave B
    copies truth), and for ch25 the feasibility-gate outcome (full feature set vs fallback).

- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml from #49):** copy LOCKED real type
  names from the compiled examples (r06 name-drift lesson; r07/r08 proved diagrams-after-green-code
  stops drift). Generate paired SVG + `.excalidraw` per figure, amber accent, light-fill emphasis boxes
  with dark text, no baked-in chapter numbers, ~960x560 C4-style. Append to `_data/diagrams.yml` in
  chapter order from #49. Two primary+secondary required per chapter; add the third (density) figure
  for any chapter whose body passes ~2500 words (confirm final count with Wave C before finalizing):
  - **#49 `25-extension-members`** (primary; name MUST match the ch25 stub include): the unowned sealed
    `CarrierShipment` decorated from outside by `CarrierShipmentExtensions` — instance extension
    properties (`WeightKilograms`, `IsHeavy`), static extension members (`Empty`, `FromKilograms`), and
    the `operator +` (or the fallback set if the gate fell back).
  - **#50 `25-extension-block-and-classic-form`** (secondary): the `extension(CarrierShipment)` block
    members beside the classic `this`-parameter `NormalisedDestination`, both resolving on one type.
  - **(#51 `25-adapting-an-unowned-type`, density if ch25 > ~2500):** adapt/decorate a type you cannot
    modify, originals left unchanged by `WithAddedWeight`.
  - **#52 `26-expression-trees`** (primary; name MUST match the ch26 stub include): a `RateRequest`
    rule authored as `Expression<Func<RateRequest, decimal>>` read two ways — inspected/traversed by an
    `ExpressionVisitor` and compiled by `Expression<T>.Compile()`.
  - **#53 `26-expressionvisitor-rewrite`** (secondary): `RateInflator`/`ReferencedPropertyCollector`
    walking the tree; `PredicateComposer.And` building a new tree via `ParameterReplacer` + `AndAlso`.
  - **(#54 `26-inspecting-a-tree`, density if ch26 > ~2500):** read-without-execute inspection (the
    LINQ-provider angle) vs compile-and-run.
  - **#55 `29-static-abstract-members-and-generic-math`** (primary; name MUST match the ch29 stub
    include): `IRateable<TSelf>` static abstract `BaseRate`/`RateOf`, `RateBook.Quote<T>` dispatching
    through the type parameter to each concrete type's static member.
  - **#56 `29-generic-math-over-money`** (secondary): `Money` implementing the `System.Numerics`
    operator interfaces; `Totals.Sum<T>` folding from `AdditiveIdentity` over both `Money` and a
    built-in `INumber<T>`.
  - **(#57 `29-irateable-static-dispatch`, density if ch29 > ~2500):** the three shipment types and
    their own `BaseRate`/`RateOf`, aggregated by `RateBook` + `Totals`.
  Real captions describing only types that exist in the green code; no cross-language tokens anywhere in
  captions or SVG labels; no CRTP / "policy-based design".

- **Wave C (prose, 3 agents, after code + figures — one per chapter):** write the full spine and replace
  the TODO caption text (keep the `Figure NN.1: ` prefix). Spine order: Learning objectives -> Why this
  matters -> primary FIGURE (after "Why this matters") -> body (secondary/density figures placed where
  density demands) -> `## Build, run, observe` (quotes the REAL path-sanitized `dotnet test` summary,
  0 `/home/`, 0 username) -> `## When not to use it` (exactly ONE `> **AI sidebar.**`) ->
  `## For deeper coverage` (Microsoft Learn + the C# language spec as the PRIMARY citations since these
  are language-feature chapters; GoF via the Pearson publisher link ONLY where a GoF pattern is
  genuinely in play — ch26 may cite GoF Visitor lightly for `ExpressionVisitor`; ch25 and ch29 do not
  force a GoF cite).
  - ch25: describe extension members POSITIVELY — instance/static extension properties, static
    extension members, extension operators, the classic `this`-form coexisting, and decorating/adapting
    an unowned sealed type. If the feasibility gate fell back, describe only what shipped and note the
    pin honestly.
  - ch26: describe `Expression<TDelegate>` as inspectable data, `ExpressionVisitor` traversal/rewrite,
    building trees with the factory API + parameter unification, reading a tree the way a LINQ provider
    does, and `Compile()`. Re-explain any needed concept in place; do NOT lean on the merged
    Visitor/Interpreter chapter even by topic.
  - ch29: describe static abstract interface members, invoking them through a generic type parameter
    (`T.RateOf`, `T.BaseRate`), and generic math over `System.Numerics` operator interfaces so one
    aggregate serves `Money` and the built-in numerics. **REPLACE the front-matter `description:`** with
    the positive C#-only line (proposed above). No CRTP, no "policy-based design", no cross-language.
  - ALL THREE: zero em-dash (docs + any prose); no rule-of-three; 0 ban-tier; idiomatic C# 14 only; NO
    cross-language; cross-chapter refs light; wrap `{{`/`}}` fenced blocks in `{% raw %}`. Body >= 2000
    words HARD floor, 2500 target, no ceiling; add the density figure (coordinate with the diagram
    agent's reserved #51/#54/#57 names) if a body runs past ~2500.
  - **Voice self-gate (r11 LESSON — mandatory):** each prose agent MUST run
    `lgtm-professional-voice/scripts/scan.sh` on its OWN chapter file and reach 0 ban-tier BEFORE
    handing back. The executors' own em-dash/cross-language greps are NOT sufficient (r11 regressed with
    13 ban-tier words). Explicit avoid-list to check for and remove: "genuinely", "deliberately",
    "earns its keep"/"earns its place"/"earns the", "for free", "plainly", and the filler "actually";
    plus editorializing kickers. Hand back only at 0 ban-tier.

- **Wave D (validate):**
  - `lgtm-idiomatic-review` — 0 hits on all three examples.
  - `lgtm-professional-voice` scanner — 0 ban-tier on all three docs (grep the editorializing families
    explicitly; confirm the r11 avoid-list words are absent).
  - Em-dash == 0 across `_docs` (the 3 chapters) + all `.cs` + all README.
  - `grep -c "TODO caption"` across the 3 docs == 0.
  - Diagram name-drift gate: every SVG label + every caption token present in the matching example
    source (regenerated after green code with locked names).
  - NO-CROSS-LANGUAGE gate: grep the 3 docs, all 3 example trees (.cs + README), the new diagrams.yml
    captions (#49 onward) and the new SVGs for
    `Java|JVM|erasure|C\+\+|CRTP|Spring|Smalltalk|policy-based` (case-insensitive) == 0.
  - **ch29 reframe gate:** confirm the ch29 front-matter `description:` no longer contains "CRTP" or
    "policy-based design" and reads the positive C#-only line.
  - CROSS-CHAPTER gate: no numeric cross-refs, no "the X chapter"/title/topic pointers; ch26 does not
    lean on the merged Visitor/Interpreter chapter.
  - `dotnet test` per example (expect ch25 ~8, ch26 ~10, ch29 ~10, or the ch25 count minus the dropped
    operator test if the feasibility gate fell back) + `scripts/verify.sh` (all three auto-discovered,
    all green alongside the rest).
  - `bundle exec jekyll build` clean (all new figures render).
  - Word counts recorded for all three bodies.
  - Confirm diagrams.yml appended from #49 in chapter order; all three test csprojs omit
    `TestingPlatformDotnetTestSupport` and pin the four packages (IsTestProject, IsPackable false,
    NoWarn CA1707); each example its own `.slnx`; all three pure BCL (no runtime package).
  - Write the verification log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if > ~2500); full spine with the primary
figure AFTER "Why this matters"; figure pair(s) on disk + registered in diagrams.yml with matching
`name` and REAL captions (no TODO); example `dotnet test` green and included by `scripts/verify.sh`;
tests assert behaviour; "Build, run, observe" quotes real path-sanitized output (0 `/home/`, 0
username); exactly one AI sidebar in "When not to use it"; zero em-dash across docs + .cs + README; no
rule-of-three; 0 ban-tier; idiomatic C# 14 only; NO cross-language (incl. no CRTP, no "policy-based
design"); cross-chapter refs light; no numeric cross-refs; every figure/caption type exists in the
example; primary citations Microsoft Learn + C# language spec (GoF only where genuinely in play).
- ch25 proves: instance extension property conversion + threshold; static extension member(s); extension
  operator (or the documented fallback set); classic `this`-form coexisting; adaptation of an unowned
  sealed type without mutating the original. Feasibility-gate outcome recorded.
- ch26 proves: an authored `Expression<Func<...>>` compiles and computes; `ExpressionVisitor` describes,
  inspects (reads referenced members without executing), and rewrites (scales decimal constants,
  leaving others); `ParameterReplacer` rebinds a lambda; `PredicateComposer.And` builds a composed tree
  honouring both conditions. DISTINCT from the merged ch24 (different namespace + all different names;
  works with `Expression<TDelegate>` as data, not a hand-rolled AST compiler).
- ch29 proves: `Money` adds, scales, orders, and has an additive identity via `System.Numerics`
  interfaces; one generic `Sum<T>` totals both `Money` and a built-in numeric; `RateBook.Quote<T>` and
  `BaseRateOf<T>` dispatch to each concrete type's static abstract members through the type parameter;
  a total-quote over a set combines dispatch + generic sum. The `description:` reframe landed (no CRTP,
  no "policy-based design").

Iteration: jekyll clean; diagrams.yml appended from #49 in chapter order; all three test csprojs omit
`TestingPlatformDotnetTestSupport` and pin the four packages; each example own `.slnx`; all three pure
BCL (no runtime package); no cross-language token anywhere; ch25 primary figure name stays
`25-extension-members`, ch26 `26-expression-trees`, ch29
`29-static-abstract-members-and-generic-math`; ch27/28 remain untouched (deferred to r13).

## Risks

- **ch25 C# 14 extension-member feasibility on the pin (HIGH):** the FEASIBILITY GATE runs first; if a
  sub-feature (most likely extension operators) does not compile on SDK 10.0.113, fall back to the
  supported forms and document the pin honestly — never claim a feature that does not build. Operator
  receiver on a reference type is a value parameter (not `ref`); `Nullable<T>` is not a valid operand.
  Extension blocks require a top-level, non-generic, static class. HIGH cross-language-slip risk at
  "extend a type you don't own" (stay positive: "C# lets you add members to a type you do not own by…").
- **ch26 duplication with the merged ch24 (HIGH):** ch24 hand-rolled a `TariffRule` AST and compiled it
  with a custom `TariffCompiler`. ch26 MUST use real `Expression<TDelegate>` as inspectable data with
  `ExpressionVisitor` (traverse/rewrite/inspect), a factory-API builder, and `Expression<T>.Compile()`.
  Enforced by a distinct namespace (`TariffExpressions`) and entirely different type names; Wave D
  cross-chapter gate confirms ch26 does not lean on the Visitor/Interpreter chapter even by topic. Keep
  arithmetic all-`decimal` so `VisitConstant`/factory nodes bind to decimal operators; parameter
  unification (one `ParameterExpression` across composed lambdas) is the classic trap — `ParameterReplacer`
  is the guard, tested directly.
- **ch29 cross-language slip (HIGH) + analyzer traps:** the stub description ships CRTP + "policy-based
  design" — both MUST be removed (gated). Generic math / static abstract is the most tempting place to
  reach for a cross-language contrast; describe everything positively and temporally. Analyzer traps:
  implementing `IAdditionOperators`/`IAdditiveIdentity`/`IMultiplyOperators`/`IComparisonOperators`
  requires exact member signatures; records supply `==`/`!=` but `IComparisonOperators` still needs
  `< <= > >=` and `IComparable<Money>`; CA2225 wants friendly-named alternatives for operators (add
  `Add`/`Multiply` or justified scope-suppress); the `where TSelf : IRateable<TSelf>` self-constraint
  and calling `T.RateOf`/`T.BaseRate` through the type parameter must compile under warnings-as-errors.
  Single-currency `Money` simplification stated in prose.
- **Expression-tree build/visit correctness (ch26):** rewritten trees must compile and produce the
  scaled result (test guards); `VisitConstant` must scale only `decimal` constants (the
  non-decimal-untouched test guards); inspection must read without executing (the read-without-execute
  test guards).
- **Diagram name drift:** generated AFTER green code with locked names; Wave D gate verifies every SVG
  label + caption token exists in the example source (r06/r07/r08 lesson).
- **TODO-caption trap:** the primary-figure include caption is owned by neither the diagram nor the code
  agent by default; Wave C explicitly replaces it (keeping `Figure NN.1: `), Wave D greps `TODO caption`
  == 0 (r05 lesson).
- **Voice regression (r11 lesson):** executor greps are not enough; each Wave-C prose agent runs the
  voice scanner to 0 ban-tier before handing back, checking the explicit avoid-list
  (genuinely/deliberately/earns-its-keep/for-free/plainly/filler "actually") and editorializing kickers.
- **Word floor:** 2000-word hard floor per chapter; add the reserved density figure (#51/#54/#57) if a
  body passes ~2500 rather than trimming substance.
- **Standing mechanics:** Liquid `{{ }}` in fenced blocks (`Expression.Lambda<...>`, interpolated
  strings, collection expressions) wrapped in `{% raw %}`; `.slnx`/`global.json` wiring; CA1707
  test-only; path leaks in quoted output; all three pure BCL (no accidental package add).

## r12 verification log

Opus-validated 2026-10-31 (branch `release/r12`). Overall: PASS (PASS-WITH-NITS at validation; both
nits fixed in a repair pass: ch29 "earns its cost" -> "is worth its complexity"; the three density
diagrams.yml entries reordered into strict chapter order beside their siblings).

**Build / test (all green, 0 warnings — warnings-as-errors):**
- `examples/25-extension-members` `dotnet test`: 10/10.
- `examples/26-expression-trees` `dotnet test`: 13/13.
- `examples/29-static-abstract-members-and-generic-math` `dotnet test`: 12/12.
- `scripts/verify.sh`: all three auto-discovered and green alongside the rest (24 suites, 0 failures).
- `bundle exec jekyll build`: clean, all nine r12 figures render.

**Content:** three chapters full spine in order (primary figure after "Why this matters"); "Build,
run, observe" quotes real counts (10/13/12), path-sanitized (0 `/home/`, 0 username); exactly one AI
sidebar each in "When not to use it"; body prose 3072 (ch25) / 3184 (ch26) / 2866 (ch29) per the
validator's count, each with a density figure; 0 em-dash; primary citations learn.microsoft.com + C#
language spec (ch26 adds a light GoF Visitor pearson cite for ExpressionVisitor).

**ch25 feasibility gate:** FULL feature set shipped — instance extension properties (WeightKilograms,
IsHeavy), static extension members (Empty, FromKilograms), and the extension operator + all compile and
test green on SDK 10.0.113 / LangVersion 14.0. No fallback needed. CA1708 scope-suppressed with a
justified SuppressMessage (analyzer false positive on paired instance/static extension blocks); CA2225
satisfied with the Add named alternate; classic this-form NormalisedDestination coexists.

**ch26 distinctness from merged ch24:** namespace TariffExpressions (ch24 was TariffRules); no
TariffRule/TariffCompiler/TariffEvaluator/Shipment/Memento reuse. Real System.Linq.Expressions trees:
TariffExpressionLibrary authored as Expression<Func<...>> data; RateDescriber/ReferencedPropertyCollector
/RateInflator/ParameterReplacer derive from ExpressionVisitor; ReferencedPropertyCollector reads without
executing (divide-by-zero rule collects cleanly, throws only on Compile); RateInflator returns a new tree
scaling only decimal constants; PredicateComposer.And unifies parameters via ParameterReplacer + AndAlso.

**ch29 reframe gate:** front-matter description no longer contains CRTP or "policy-based design"; reads
the positive C#-only line. Money implements IAdditionOperators/IAdditiveIdentity/IMultiplyOperators/
IComparisonOperators/IComparable<Money> from System.Numerics with CA2225 Add/Multiply alternates;
IRateable<TSelf> self-constraint; RateBook.Quote<T>/BaseRateOf<T> dispatch through the type parameter;
Totals.Sum<T> folds from T.AdditiveIdentity over both Money and built-in decimal.

**NO CROSS-LANGUAGE:** grep for Java/JVM/erasure/C++/CRTP/Spring/Smalltalk/policy-based (case-insensitive)
across the three docs, three example trees (.cs + README), the new diagrams.yml captions, and the nine
SVGs = ZERO.

**CROSS-CHAPTER REFS LIGHT:** no numeric cross-refs; no title/topic pointers; all "chapter" hits are
self-references. ch26 does not lean on the merged Visitor/Interpreter chapter (Visitor appears only as
the BCL ExpressionVisitor type and a single allowed GoF cite).

**Diagrams (name-drift gate):** nine figures each paired on disk (.svg + .excalidraw), registered in
diagrams.yml (now 57 entries, r12 block in strict chapter order after the repair), included in the right
chapter by name; every SVG label + caption token verified present in the example source.

**Code idiom:** `lgtm-idiomatic-review` 0 hits on all three. All three test csprojs set IsTestProject +
IsPackable false + NoWarn CA1707, pin the four packages, omit TestingPlatformDotnetTestSupport; each
example its own .slnx; all three libraries pure BCL (no runtime package). Em-dash across all .cs +
README = 0.

**Voice:** `lgtm-professional-voice` 0 ban-tier on all three (watch-tier left as-is). `grep -c
"TODO caption"` = 0. The one avoid-list phrase the validator flagged ("earns its cost") was reworded.

**Part 9 pure-BCL subset (ch25, ch26, ch29) COMPLETE; ch27 (source generators) + ch28 (analyzers)
deferred to r13 as planned.**
