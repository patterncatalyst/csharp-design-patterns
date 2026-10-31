---
title: "r14 plan — Part 10 (ch30, ch31, ch32)"
output: false
render_with_liquid: false
---

# r14 — author ch30, ch31, ch32 (Part 10: Patterns the Platform Defines)

Opus-planned 2026-10-31. Branch `release/r14` (based on main@848ca89). Three platform-convention
chapters: the Options pattern, the Tester-Doer / Result debate, and the Task-based Asynchronous
Pattern. This part RETURNS to the standard net10.0 library + xUnit v3 toolchain (ch0-26/29), NOT the
netstandard2.0 Roslyn toolchain of ch27/28. Research-heavy for the platform conventions: the authority
is Microsoft Learn (Framework Design Guidelines, the Options pattern, the Task-based Asynchronous
Pattern) plus the .NET docs, NOT blog posts. Four-wave shape (A code, 3 parallel agents / B diagrams
AFTER green code with LOCKED names / C prose, 3 agents each voice-self-gating to 0 ban-tier / D
validate). Enforces: NO cross-language comparison (ch31 `Result<T>` and ch32 async conventions are the
two mild temptation spots; describe both POSITIVELY and temporally, never against Rust/Java/C++/JVM/
functional languages/Spring); keep cross-chapter refs light (each chapter stands alone; ch32 may name
Part 11 topics only as "deferred to the concurrency material" at most, light, no number/title).

## Scope + example assignments (shipping domain, from the Notion per-chapter map)

- **ch30 The Options pattern** (`_docs/30-the-options-pattern.md`, Carrier domain). Carrier gateway
  configuration (endpoints, timeouts, retry budgets) bound and consumed via `IOptions<T>` /
  `IOptionsSnapshot<T>` / `IOptionsMonitor<T>`, including change notification (`IOptionsMonitor.OnChange`)
  on config that really does change, and `ValidateOnStart` / `IValidateOptions<T>`. Uses MEDI +
  `Microsoft.Extensions.Options[.ConfigurationExtensions]` + `Microsoft.Extensions.Configuration`
  (in-memory config source only; NO file I/O, NO Hosting package). Cite Microsoft Learn (Options
  pattern in .NET; Options pattern guidance; options validation) + the .NET DI docs. No GoF.
- **ch31 Tester-Doer and the Result debate** (`_docs/31-tester-doer-and-the-result-debate.md`,
  Documentation domain). Parsing an EDI message / validating a customs declaration where failure is
  EXPECTED. Teaches BOTH the Framework Design Guidelines Tester-Doer + Try-Parse conventions (`TryParse`
  bool + `out`, no exception for expected failure) AND a community `Result<T>` type, SIDE BY SIDE,
  STATING THE TRADEOFF rather than picking a side (the stub front matter is explicit about this). Pure
  BCL. Cite the Framework Design Guidelines (Exceptions and Performance: the Tester-Doer Pattern and
  the Try-Parse Pattern) via Microsoft Learn + the book's bibliography entry. No GoF.
- **ch32 TAP and the async conventions** (`_docs/32-tap-and-async-conventions.md`, Carrier domain). The
  carrier gateway's async API surface with the Task-based Asynchronous Pattern NAMED: method naming
  (`...Async`), `CancellationToken` acceptance, `IProgress<T>` progress reporting, `ConfigureAwait(false)`
  in library code, `ValueTask` where appropriate. Mechanics of concurrency are deferred to the
  concurrency material (light pointer at most); this chapter is about the CONVENTIONS. Pure BCL
  (Task/ValueTask/IProgress/CancellationToken are in-framework). Cite Microsoft Learn (Task-based
  Asynchronous Pattern; TAP naming conventions; ConfigureAwait guidance). No GoF.

## Confirmed facts

- Part slug `platform-patterns` (all three stubs carry it in front matter; verified). Edit body in place.
- Branch `release/r14` already created off `main@848ca89` (verified HEAD). SDK pin 10.0.113, LangVersion
  14.0, Nullable on, ImplicitUsings on, TreatWarningsAsErrors, AnalysisLevel latest-recommended,
  Deterministic — all inherited from `examples/Directory.Build.props` (verified). Root `global.json`
  opts into `Microsoft.Testing.Platform`.
- Each stub's primary include is `{% include excalidraw.html name="<NN-...>" caption="Figure NN.1:
  TODO caption." %}`. Primary `name` MUST stay exactly `30-the-options-pattern`,
  `31-tester-doer-and-the-result-debate`, `32-tap-and-async-conventions` (verified). Replace ONLY the
  caption text, keeping the `Figure NN.1: ` prefix. `grep -c "TODO caption"` is 1 per stub today; MUST
  be 0 at the end.
- Each stub has a `## When not to use it` section; the single `> **AI sidebar.**` block lives there
  (exactly one per chapter). Each stub also carries a leading `<!-- EXAMPLE (shipping domain): ... -->`
  comment naming the per-chapter example framing.
- `_data/diagrams.yml` currently has **62** entries (counted via `grep -c "^- name:"`); append from
  **#63** in chapter order. ONE diagram agent owns diagrams.yml this iteration. Amber accent; light-fill
  emphasis boxes with dark text; no baked-in chapter numbers; ~960x560 C4-style. Entries carry
  `section: "Ch. 30/31/32"`, never a figure number in the art.
- Template example layout (verified from `examples/22-...` + `examples/17-...`): library `.csproj` with
  `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**</DefaultItemExcludes>`; the runtime
  PackageReference(s) live in the LIBRARY csproj and flow transitively to the test project (ch17 idiom);
  nested `*.Tests/` xUnit v3 csproj pinning the four standard packages (xunit.v3 4.0.2,
  xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0), setting
  `IsTestProject` true + `IsPackable` false + `NoWarn $(NoWarn);CA1707`, and OMITTING
  `TestingPlatformDotnetTestSupport`; a top-level `.slnx` listing both projects; a README mirroring
  ch22's.
- MEDI idiom: `public static class ServiceCollectionExtensions` with an `Add...` extension on
  `IServiceCollection` returning `services` (ch17 `AddTariffTables` shape).
- Async idiom: `.ConfigureAwait(false)` on EVERY library await (ch14); tests pass
  `TestContext.Current.CancellationToken` for happy paths and an explicit `CancellationTokenSource` for
  cancellation (ch14 / ch23).
- Dependency survey (surveyed 2026-10-31 on SDK 10.0.113 with `dotnet package search --exact-match`):
  - ch30: `Microsoft.Extensions.DependencyInjection` **10.0.14**, `Microsoft.Extensions.Options`
    **10.0.14**, `Microsoft.Extensions.Options.ConfigurationExtensions` **10.0.14** (the `OptionsBuilder.Bind`
    overload + change-token source), `Microsoft.Extensions.Configuration` **10.0.14** (ConfigurationBuilder +
    `AddInMemoryCollection`). All four latest-10.0.x, moving together with the established MEDI pin. NO
    Hosting package: `IValidateOptions<T>` / `ValidateOnStart()` / `IStartupValidator` all live in
    `Microsoft.Extensions.Options`. Config binding uses an in-memory `ConfigurationBuilder`
    (`AddInMemoryCollection`) only: no file I/O, no containers, test-friendly, reloadable via
    `IConfigurationRoot.Reload()`.
  - ch31: PURE BCL (hand-rolled `Result<T>` + `TryParse`-style methods). No package.
  - ch32: PURE BCL (Task/ValueTask/IProgress/CancellationToken in-framework). No package.
- Namespace survey (verified via `grep -rho "^namespace"`): taken include Consignments, ConsignmentRouting,
  ConsignmentTracking, ConsignmentAmendments, CarrierGateways, CarrierIntegration, CarrierProvisioning,
  CarrierParcels, CarrierManifest(.Generation), RateCalculation, RateComposition, RateQuoting, RatingEngine,
  RateMath, TariffRules, TariffExpressions, TariffTables, FreightValueTypes, BookingRequests,
  BookingAmendments, CustomsClearance, CustomsDocumentation, ManifestWriting, TrackingHistory,
  WireContracts(.Analysis). The three fresh namespaces below do NOT collide: `CarrierConfiguration`
  (ch30), `DeclarationParsing` (ch31), `CarrierDispatch` (ch32).

## LOCKED type names (Wave A builds verbatim; Wave B diagrams copy from the green code)

### ch30 `examples/30-the-options-pattern/` (namespace `CarrierConfiguration`; MEDI + Options + Configuration, all 10.0.14)
Project `CarrierConfiguration.csproj`; tests `CarrierConfiguration.Tests`; `CarrierConfiguration.slnx`; README.
Library csproj pins the four runtime packages above (library-level, transitive to tests, ch17 idiom).
- `public sealed class CarrierGatewayOptions` — the options POCO (mutable, public parameterless ctor so
  the binder can populate it; records bind but the binder idiom is a settable class):
  - `public const string SectionName = "CarrierGateway";`
  - `public string Endpoint { get; set; } = "";` (the gateway base address)
  - `public int TimeoutSeconds { get; set; }` (request timeout)
  - `public int RetryBudget { get; set; }` (max retries)
  State the "settable class, not a record" binder reason in prose.
- `public sealed class CarrierGatewayOptionsValidator : IValidateOptions<CarrierGatewayOptions>` —
  `public ValidateOptionsResult Validate(string? name, CarrierGatewayOptions options)` returning
  `ValidateOptionsResult.Fail(reasons)` when `Endpoint` is blank / not an absolute URI, `TimeoutSeconds`
  is <= 0, or `RetryBudget` is < 0; `ValidateOptionsResult.Success` otherwise. Collect ALL failure
  reasons (not fail-fast on the first) so the fail-fast test sees a complete message.
- `public sealed class RetryBudgetWatcher` — the MONITOR-DRIVEN CONSUMER:
  - ctor takes `IOptionsMonitor<CarrierGatewayOptions>`, reads `CurrentValue` into a field, and
    subscribes `monitor.OnChange(updated => ...)` recording each observed value into a private
    `List<CarrierGatewayOptions>` (exposed as `IReadOnlyList<CarrierGatewayOptions> Observed`), keeping
    the `IDisposable` the subscription returns.
  - `public int CurrentRetryBudget => _current.RetryBudget;`
  - implements `IDisposable` to dispose the OnChange registration.
- `public static class ServiceCollectionExtensions` — the composition root (ch17 shape):
  - `public static IServiceCollection AddCarrierGateway(this IServiceCollection services,
    IConfiguration configuration)`:
    - `services.AddOptions<CarrierGatewayOptions>().Bind(configuration.GetSection(CarrierGatewayOptions.SectionName)).ValidateOnStart();`
      (the `.Bind(section)` overload from Options.ConfigurationExtensions wires the change-token source so
      `IOptionsMonitor`/`IOptionsSnapshot` track reloads).
    - `services.AddSingleton<IValidateOptions<CarrierGatewayOptions>, CarrierGatewayOptionsValidator>();`
    - `services.AddSingleton<RetryBudgetWatcher>();`
    - `return services;`
  - A small test/consumer helper may build the in-memory configuration; provide
    `public static IConfigurationRoot BuildInMemoryConfiguration(IDictionary<string, string?> values)` that
    returns `new ConfigurationBuilder().AddInMemoryCollection(values).Build()` so tests reload via the
    returned root's `MemoryConfigurationProvider`.
- **Tests** (`CarrierConfiguration.Tests`, split `OptionsBindingTests.cs` + `OptionsChangeAndValidationTests.cs`), ~10:
  1. `Options_binds_endpoint_timeout_and_retry_budget_from_configuration` (`IOptions<T>.Value`).
  2. `IOptions_returns_the_same_singleton_instance` (two resolutions are reference-equal).
  3. `IOptionsSnapshot_reflects_configuration_within_a_scope` (resolve inside `provider.CreateScope()`).
  4. `IOptionsSnapshot_rebinds_in_a_new_scope_after_a_reload` (mutate the memory provider + `root.Reload()`,
     open a NEW scope, assert the new value; the old scope's snapshot is unchanged).
  5. `IOptionsMonitor_current_value_reflects_the_bound_configuration`.
  6. `IOptionsMonitor_OnChange_fires_with_the_new_value_after_a_reload` (the change-notification headline:
     mutate the provider, `root.Reload()`, assert `RetryBudgetWatcher.Observed` CONTAINS the new value;
     assert on "contains the new value", NOT an exact callback count, because the change token can fire
     more than once).
  7. `Validator_accepts_a_well_formed_configuration` (`ValidateOptionsResult.Succeeded`).
  8. `Validator_rejects_invalid_configuration` ([Theory]/TheoryData: blank endpoint, non-positive timeout,
     negative retry budget).
  9. `ValidateOnStart_throws_for_invalid_configuration_at_startup` (resolve `IStartupValidator` from the
     provider and call `Validate()`; expect `OptionsValidationException`. FALLBACK if `IStartupValidator`
     is not resolvable on the pin: assert the exception surfaces on first `IOptions<T>.Value` access; the
     Wave A agent confirms which during build and records it).
  10. `ValidateOnStart_passes_for_valid_configuration` (no throw).

### ch31 `examples/31-tester-doer-and-the-result-debate/` (namespace `DeclarationParsing`, pure BCL)
Project `DeclarationParsing.csproj`; tests `DeclarationParsing.Tests`; `DeclarationParsing.slnx`; README.
- Domain types:
  - `public sealed record EdiSegment(string Tag, IReadOnlyList<string> Elements);`
  - `public sealed record EdiMessage(string MessageType, IReadOnlyList<EdiSegment> Segments);`
  - `public sealed record CustomsDeclaration(string Reference, string CommodityCode, decimal DeclaredValue,
    string OriginCountry);`
- **Framework Design Guidelines side (Try-Parse + Tester-Doer):**
  - `public static class EdiTryParser`:
    - `public static bool TryParse(string raw, out EdiMessage? message)` — the Try-Parse convention:
      returns `false` + `message = null` for malformed input, NEVER throws on expected (malformed) input.
    - (optional helper) `public static bool TryParseSegment(string line, out EdiSegment? segment)`.
  - `public sealed class DeclarationProcessor` — the Tester-Doer pair:
    - `public bool CanProcess(CustomsDeclaration declaration)` (Tester: reference present, commodity code
      well-formed, declared value positive, origin country present).
    - `public ProcessedDeclaration Process(CustomsDeclaration declaration)` (Doer: assumes the tester
      passed; throws `InvalidOperationException` / `ArgumentException` if called on invalid input — the
      "test before you do" contract). State the FDG race-condition caveat (tester-doer is not atomic) in
      prose.
  - `public sealed record ProcessedDeclaration(string Reference, decimal DeclaredValue);`
- **Community `Result<T>` side:**
  - `public readonly record struct Result<T>` — success-or-failure carrier, NO exceptions for control flow:
    - `public bool IsSuccess { get; }`, `public T? Value { get; }`, `public string? Error { get; }`
    - `public static Result<T> Success(T value)`, `public static Result<T> Failure(string error)`
    - `public Result<TOut> Map<TOut>(Func<T, TOut> selector)` (transforms a success, short-circuits a
      failure — the composition point the `out` bool lacks).
  - `public static class EdiResultParser` — `public static Result<EdiMessage> Parse(string raw)`
    (Success carries the message; Failure carries the specific reason).
  - `public sealed class DeclarationValidator` — `public Result<CustomsDeclaration> Validate(
    CustomsDeclaration candidate)` (Success passes the declaration through; Failure names the first/most
    relevant validation reason).
  State the tradeoff in prose and do NOT pick a side: Try-Parse is BCL-idiomatic, allocation-light, and
  needs no shared type, but `out` results do not compose and carry only a bool; `Result<T>` composes
  (`Map`/chaining) and carries a reason, but is a non-BCL type each codebase reinvents, with interop
  friction at BCL boundaries.
- **Tests** (`DeclarationParsing.Tests`, split `TesterDoerTests.cs` + `ResultTests.cs`), ~12:
  1. `TryParse_returns_true_and_the_message_for_a_well_formed_edi_string`.
  2. `TryParse_returns_false_and_null_for_a_malformed_string`.
  3. `TryParse_does_not_throw_on_expected_failure` (`Record.Exception` is null).
  4. `CanProcess_is_true_for_a_valid_declaration`.
  5. `CanProcess_is_false_for_an_invalid_declaration` ([Theory] over invalid field cases).
  6. `Process_after_a_passing_tester_succeeds`.
  7. `Process_without_testing_throws_on_invalid_input` (the Tester-Doer contract).
  8. `Result_parse_succeeds_and_carries_the_message`.
  9. `Result_parse_fails_and_carries_a_reason` (the reason the bool could not carry).
  10. `Result_validate_reports_the_specific_validation_failure`.
  11. `Result_map_transforms_a_success_and_short_circuits_a_failure` (composition proof).
  12. `Both_styles_agree_on_whether_an_input_is_valid` ([Theory]/TheoryData of well-formed + malformed
      inputs: `EdiTryParser.TryParse(x, out _)` equals `EdiResultParser.Parse(x).IsSuccess` — the
      side-by-side equivalence proving the two styles encode the same decision differently).

### ch32 `examples/32-tap-and-async-conventions/` (namespace `CarrierDispatch`, pure BCL)
Project `CarrierDispatch.csproj`; tests `CarrierDispatch.Tests`; `CarrierDispatch.slnx`; README.
- Domain types:
  - `public sealed record DispatchRequest(string ConsignmentId, string Destination, decimal WeightKilograms);`
  - `public sealed record DispatchReceipt(string ConsignmentId, string TrackingNumber, DateTimeOffset AcceptedAt);`
  - `public sealed record DispatchProgress(int Completed, int Total);`
- `public interface ICarrierGateway` — the TAP-conventional async surface:
  - `Task<DispatchReceipt> DispatchAsync(DispatchRequest request, CancellationToken cancellationToken = default);`
    (`...Async` suffix + `CancellationToken` acceptance).
  - `Task<IReadOnlyList<DispatchReceipt>> DispatchBatchAsync(IReadOnlyList<DispatchRequest> requests,
    IProgress<DispatchProgress>? progress = null, CancellationToken cancellationToken = default);`
    (`IProgress<T>` progress reporting; optional progress).
  - `ValueTask<bool> IsReachableAsync(CancellationToken cancellationToken = default);`
    (`ValueTask` where appropriate: a cheap, usually-synchronous reachability check that completes without
    allocating a Task on the hot path).
- `public sealed class FakeCarrierGateway : ICarrierGateway` — library implementation demonstrating the
  conventions, DETERMINISTIC (no wall-clock; concurrency mechanics deferred to the concurrency material):
  - every await uses `.ConfigureAwait(false)`; use `await Task.Yield().ConfigureAwait(false)` or
    `await Task.Delay(0, cancellationToken).ConfigureAwait(false)` as the async suspension point.
  - honours cancellation via `cancellationToken.ThrowIfCancellationRequested()` at the top and between
    batch items.
  - `DispatchBatchAsync` processes requests in order, calling `progress?.Report(new DispatchProgress(i + 1,
    requests.Count))` after each, returning one `DispatchReceipt` per request (order preserved).
  - `IsReachableAsync` returns a completed `ValueTask<bool>` synchronously (`ValueTask.FromResult(true)` /
    `new ValueTask<bool>(true)`) to show the no-allocation fast path; honours the token first.
- **Tests** (`CarrierDispatch.Tests`, split `DispatchAsyncTests.cs` + `BatchProgressTests.cs` +
  `AsyncConventionTests.cs`), ~10. Happy paths pass `TestContext.Current.CancellationToken`; cancellation
  cases use an explicit `CancellationTokenSource`. Use a HAND-ROLLED synchronous `IProgress<DispatchProgress>`
  test double (a `RecordingProgress : IProgress<DispatchProgress>` that appends to a list on `Report`),
  NOT `System.Threading.Progress<T>` (which marshals asynchronously and would race):
  1. `DispatchAsync_returns_a_receipt_for_the_request`.
  2. `DispatchAsync_honours_a_cancelled_token` (pre-cancelled token -> `OperationCanceledException`).
  3. `DispatchBatchAsync_reports_progress_for_each_item` (final report has `Completed == Total`).
  4. `DispatchBatchAsync_without_a_progress_callback_still_completes` (null progress allowed).
  5. `DispatchBatchAsync_honours_cancellation_mid_batch` (cancel after N via the CTS; fewer than all
     receipts produced, `OperationCanceledException` thrown).
  6. `Progress_reports_are_monotonic` (`Completed` rises 1..Total).
  7. `DispatchBatchAsync_returns_a_receipt_per_request` (count + order preserved).
  8. `IsReachableAsync_completes_synchronously` (the returned `ValueTask<bool>.IsCompleted` is true).
  9. `Every_task_returning_member_uses_the_async_suffix` (reflection over `ICarrierGateway`: every method
     whose return type is `Task`/`Task<>`/`ValueTask`/`ValueTask<>` has a name ending in "Async" — the
     naming convention made executable).
  10. `Every_async_member_accepts_a_cancellation_token` (reflection: each such method has a
      `CancellationToken` parameter — the cancellation convention made executable).

## Waves

- **Wave A (code, 3 agents parallel — one per chapter):** build each example green.
  - Library csproj mirrors the template:
    `<DefaultItemExcludes>$(DefaultItemExcludes);<TestDir>/**</DefaultItemExcludes>`; ch30's library csproj
    pins the four runtime packages (MEDI/Options/Options.ConfigurationExtensions/Configuration all 10.0.14),
    ch31/ch32 add NO runtime package (pure BCL). Nested `*.Tests/` csproj pins the four standard packages
    (xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0),
    sets `IsTestProject` true + `IsPackable` false + `NoWarn $(NoWarn);CA1707`, and OMITS
    `TestingPlatformDotnetTestSupport`; each example its own `.slnx`; each ships a README mirroring ch22's.
  - ch30 specifics: wire Options through `ServiceCollectionExtensions.AddCarrierGateway`; build config
    with `ConfigurationBuilder().AddInMemoryCollection(...)` (NO file I/O, NO Hosting package); drive
    `OnChange` by mutating the `MemoryConfigurationProvider` and calling `IConfigurationRoot.Reload()`;
    CONFIRM during build whether `IStartupValidator` is resolvable for the `ValidateOnStart` test and
    record the outcome (full vs fallback). `IOptionsSnapshot` is resolved inside a DI scope.
  - ch32 specifics: `.ConfigureAwait(false)` on EVERY library await (CA2007); keep the fake deterministic
    (`Task.Yield()` / `Task.Delay(0, ct)` / completed `ValueTask`), no wall-clock flakiness; the test
    `IProgress<T>` double is synchronous.
  - Tests EXACTLY as listed (ch30 ~10, ch31 ~12, ch32 ~10); behaviour-asserting, never build-exits-0.
    Tests use `TestContext.Current.CancellationToken` wherever an API takes a token (ch32) and an explicit
    `CancellationTokenSource` for cancellation cases (ch32).
  - Run `dotnet test` per example and CAPTURE the real summary, path-sanitized (0 `/home/`, 0 username)
    for the "Build, run, observe" quote. Wrap any fenced `{{`/`}}` in `{% raw %}` if it reaches a `.md`
    (interpolated strings, collection expressions, generic `<T,T,T>` butting braces).
  - Each agent reports: the real `dotnet test` summary, the final LOCKED names actually built (so Wave B
    copies truth), and for ch30 the `ValidateOnStart`/`IStartupValidator` outcome (full vs fallback).

- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml from #63):** copy LOCKED real type
  names from the compiled examples (r06 name-drift lesson; r07/r08/r11/r12/r13 proved diagrams-after-green
  stops drift). Paired SVG + `.excalidraw` per figure, amber accent, light-fill emphasis boxes with dark
  text, no baked-in chapter numbers, ~960x560 C4-style. Append to `_data/diagrams.yml` in chapter order
  from #63. Two figures per chapter required; add the third (density) figure for any chapter whose body
  passes ~2500 words (confirm final count with Wave C before finalizing):
  - **#63 `30-the-options-pattern`** (primary; name MUST match the ch30 stub include): `CarrierGatewayOptions`
    bound from an in-memory `IConfiguration` section, consumed three ways — `IOptions<T>` (singleton),
    `IOptionsSnapshot<T>` (per-scope), `IOptionsMonitor<T>` (current value + OnChange).
  - **#64 `30-options-change-notification`** (secondary): config reload -> re-bind -> `IOptionsMonitor.OnChange`
    into `RetryBudgetWatcher`; `IValidateOptions<T>` + `ValidateOnStart` gating startup.
  - **(#65 `30-options-validation-on-start`, density if ch30 > ~2500):** `CarrierGatewayOptionsValidator`
    collecting failure reasons and the fail-fast path at startup.
  - **#66 `31-tester-doer-and-the-result-debate`** (primary; name MUST match the ch31 stub include): one
    parse/validate decision expressed two ways — `EdiTryParser.TryParse`/`DeclarationProcessor` (bool + out,
    test-then-do) beside `EdiResultParser.Parse`/`DeclarationValidator` (`Result<T>` Success/Failure + reason).
  - **#67 `31-try-parse-vs-result`** (secondary): the Try-Parse `out`/bool shape against `Result<T>`
    carrying a reason; the tradeoff laid out, no side picked.
  - **(#68 `31-result-composition`, density if ch31 > ~2500):** `Result<T>.Map` chaining / short-circuit
    against the non-composing `out` bool.
  - **#69 `32-tap-and-async-conventions`** (primary; name MUST match the ch32 stub include): the
    `ICarrierGateway` async surface — `...Async` naming, `CancellationToken`, `IProgress<DispatchProgress>`,
    `ValueTask`, `ConfigureAwait(false)` in library code.
  - **#70 `32-cancellation-and-progress`** (secondary): `DispatchBatchAsync` honouring a token mid-batch
    and reporting `DispatchProgress` to an `IProgress<T>` consumer.
  - **(#71 `32-task-valuetask-shape`, density if ch32 > ~2500):** `Task` vs `ValueTask` choice
    (`DispatchAsync` returns `Task<DispatchReceipt>`; `IsReachableAsync` returns a completed `ValueTask<bool>`).
  Real captions describing only types that exist in the green code; no cross-language tokens anywhere in
  captions or SVG labels.

- **Wave C (prose, 3 agents, after code + figures — one per chapter):** write the full spine and replace
  the TODO caption text (keep the `Figure NN.1: ` prefix). Spine order: Learning objectives -> Why this
  matters -> primary FIGURE (after "Why this matters") -> body (secondary/density figures placed where
  density demands) -> `## Build, run, observe` (quotes the REAL path-sanitized `dotnet test` summary, 0
  `/home/`, 0 username) -> `## When not to use it` (exactly ONE `> **AI sidebar.**`) -> `## For deeper
  coverage` (Microsoft Learn + the .NET docs as the PRIMARY citations; the Framework Design Guidelines
  via Microsoft Learn + the bibliography for ch31; NO GoF for any of the three — these are platform
  conventions, not GoF patterns).
  - ch30: describe the Options pattern POSITIVELY — binding a settable options class from configuration,
    the three consumption interfaces and their lifetimes (`IOptions<T>` singleton, `IOptionsSnapshot<T>`
    scoped, `IOptionsMonitor<T>` with `CurrentValue` + `OnChange`), change notification on config that
    really changes, and `IValidateOptions<T>` + `ValidateOnStart`. If the `ValidateOnStart` test fell
    back from `IStartupValidator`, describe what shipped honestly.
  - ch31: teach BOTH styles side by side and STATE THE TRADEOFF without picking a side — Try-Parse
    (bool + out, no exception for expected failure) and Tester-Doer (test then do, with the atomicity
    caveat) from the Framework Design Guidelines, and the community `Result<T>` (Success/Failure, a
    reason, `Map` composition). Stay POSITIVE: `Result<T>` is a C# community idiom; NEVER frame it as
    borrowed from Rust/functional languages or contrast it with another language.
  - ch32: name the Task-based Asynchronous Pattern and its conventions — `...Async` naming,
    `CancellationToken` acceptance, `IProgress<T>` reporting, `ConfigureAwait(false)` in library code,
    `ValueTask` where appropriate. Concurrency MECHANICS are out of scope: at most a light "deferred to
    the concurrency material" pointer, no chapter number/title. Stay POSITIVE and temporal; never
    contrast another language's async.
  - ALL THREE: zero em-dash (docs + any prose); no rule-of-three; 0 ban-tier; idiomatic C# 14 only; NO
    cross-language; cross-chapter refs light; wrap `{{`/`}}` fenced blocks in `{% raw %}`. Body >= 2000
    words HARD floor, 2500 target, no ceiling; add the density figure (coordinate with the diagram agent's
    reserved #65/#68/#71 names) if a body runs past ~2500.
  - **Voice self-gate (r11/r12/r13 LESSON — mandatory):** each prose agent MUST run
    `lgtm-professional-voice/scripts/scan.sh` on its OWN chapter file and reach 0 ban-tier BEFORE handing
    back. Explicit avoid-list to remove: "genuinely", "deliberately", "earns its keep"/"earns its place"/
    "earns the"/"earns its name"/"is worth its", "for free", "plainly", filler "actually", "on purpose",
    editorializing kickers, and "capstone" (use "Final Project"). Hand back only at 0 ban-tier.

- **Wave D (validate):**
  - `lgtm-idiomatic-review` — 0 hits on all three examples.
  - `lgtm-professional-voice` scanner — 0 ban-tier on all three docs (grep the editorializing families
    explicitly; confirm the avoid-list words incl. "capstone" are absent).
  - Em-dash == 0 across `_docs` (the 3 chapters) + all `.cs` + all README.
  - `grep -c "TODO caption"` across the 3 docs == 0.
  - `grep -ci "capstone"` across the 3 docs == 0.
  - Diagram name-drift gate: every SVG label + every caption token present in the matching example source
    (regenerated after green code with locked names).
  - NO-CROSS-LANGUAGE gate: grep the 3 docs, all 3 example trees (.cs + README), the new diagrams.yml
    captions (#63 onward) and the new SVGs for
    `Java|JVM|erasure|C\+\+|CRTP|Spring|Smalltalk|policy-based|Rust|Go\b|functional language` (case-insensitive)
    == 0.
  - CROSS-CHAPTER gate: no numeric cross-refs, no "the X chapter"/title/topic pointers; ch32's only
    forward reference is a light "deferred to the concurrency material" (no number/title).
  - `dotnet test` per example (expect ch30 ~10, ch31 ~12, ch32 ~10) + `scripts/verify.sh` (all three
    auto-discovered, all green alongside the rest).
  - `bundle exec jekyll build` clean (all new figures render).
  - Word counts recorded for all three bodies.
  - Confirm diagrams.yml appended from #63 in chapter order; all three test csprojs omit
    `TestingPlatformDotnetTestSupport` and pin the four packages (IsTestProject, IsPackable false, NoWarn
    CA1707); each example its own `.slnx`; ch30 pins the four runtime packages at 10.0.14; ch31/ch32 pure
    BCL (no runtime package). Confirm ch30's `ValidateOnStart` outcome (full vs documented fallback).
  - Write the verification log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if > ~2500); full spine with the primary
figure AFTER "Why this matters"; figure pair(s) on disk + registered in diagrams.yml with matching `name`
and REAL captions (no TODO); example `dotnet test` green and included by `scripts/verify.sh`; tests assert
behaviour; "Build, run, observe" quotes real path-sanitized output (0 `/home/`, 0 username); exactly one
AI sidebar in "When not to use it"; zero em-dash across docs + .cs + README; no rule-of-three; 0 ban-tier;
no "capstone"; idiomatic C# 14 only; NO cross-language; cross-chapter refs light; no numeric cross-refs;
every figure/caption type exists in the example; primary citations Microsoft Learn + .NET docs (FDG via
Learn + bibliography for ch31); no GoF.
- ch30 proves: binds endpoint/timeout/retry budget from an in-memory config section; `IOptions<T>`
  singleton value; `IOptionsSnapshot<T>` rebinds per scope after a reload; `IOptionsMonitor.CurrentValue`
  + `OnChange` fires with the new value after a reload; `CarrierGatewayOptionsValidator` accepts valid /
  rejects invalid; `ValidateOnStart` fails fast for invalid config and passes for valid (full
  `IStartupValidator` path or the documented fallback). File-free, Hosting-free, container-free.
- ch31 proves: `TryParse` returns true/message for well-formed and false/null for malformed WITHOUT
  throwing on expected failure; Tester-Doer `CanProcess`/`Process` contract (Process throws if used
  untested); `Result<T>` Parse/Validate carry a reason the bool cannot, and `Map` composes/short-circuits;
  both styles AGREE on validity over a shared input set. The tradeoff is stated, neither side is picked.
- ch32 proves: `DispatchAsync` returns a receipt and honours a cancelled token; `DispatchBatchAsync`
  reports progress per item (final Completed == Total), tolerates null progress, honours cancellation
  mid-batch, and returns one receipt per request in order; `IsReachableAsync` completes synchronously as a
  `ValueTask`; reflection confirms every Task/ValueTask member uses the `...Async` suffix and accepts a
  `CancellationToken`. `ConfigureAwait(false)` on every library await; deterministic (no wall-clock).

Iteration: jekyll clean; diagrams.yml appended from #63 in chapter order; all three test csprojs omit
`TestingPlatformDotnetTestSupport` and pin the four standard packages; each example own `.slnx`; ch30 pins
the four runtime packages at 10.0.14; ch31/ch32 pure BCL; no cross-language token anywhere; primary figure
names stay `30-the-options-pattern`, `31-tester-doer-and-the-result-debate`, `32-tap-and-async-conventions`;
standard net10.0 + xUnit v3 toolchain throughout (NOT the Roslyn netstandard2.0 toolchain).

## Risks

- **ch30 Options plumbing + OnChange determinism (HIGH):** `IOptionsMonitor.OnChange` depends on the
  change-token source wired by the `OptionsBuilder.Bind(section)` overload from Options.ConfigurationExtensions;
  drive change deterministically by mutating the `MemoryConfigurationProvider` and calling
  `IConfigurationRoot.Reload()` (no file watcher, no `Thread.Sleep`). The change token can fire more than
  once per reload, so the OnChange test asserts "Observed CONTAINS the new value", not an exact count.
  `IOptionsSnapshot<T>` is scoped: resolve it inside `provider.CreateScope()` or it throws. Keep it
  Hosting-free: `IValidateOptions<T>`/`ValidateOnStart()`/`IStartupValidator` are all in
  `Microsoft.Extensions.Options`. `ValidateOnStart` test: resolve `IStartupValidator` and call `Validate()`;
  if not resolvable on the pin, FALL BACK to asserting `OptionsValidationException` on first `.Value`
  access and document it. Settable options class (not a record) for the binder.
- **ch31 evenhandedness + Result idiom (HIGH voice risk):** present Try-Parse/Tester-Doer and `Result<T>`
  side by side and STATE THE TRADEOFF without picking a side (the stub is explicit). `Result<T>` must not
  use exceptions for control flow (it returns a value). HIGH cross-language-slip risk: `Result<T>` is
  strongly associated with other languages; describe it as a C# COMMUNITY idiom, POSITIVELY, never "like
  Rust", "from functional languages", or any contrast. Analyzer traps: `readonly record struct` with
  nullable `Value`/`Error`; `IReadOnlyList` on record members (CA1002/CA1819); the Tester-Doer atomicity
  caveat stated in prose.
- **ch32 async determinism + analyzers + IProgress threading (HIGH):** CA2007 requires `.ConfigureAwait(false)`
  on every library await; xUnit1051 -> pass `TestContext.Current.CancellationToken`; keep the fake
  deterministic (`Task.Yield()`/`Task.Delay(0, ct)`/completed `ValueTask`), no wall clock. IProgress
  threading: `System.Threading.Progress<T>` marshals its callbacks asynchronously (captured
  SynchronizationContext / thread pool) and would race the assertions, so the TEST uses a hand-rolled
  SYNCHRONOUS `IProgress<DispatchProgress>` double; the library calls `progress?.Report(...)` directly.
  `ValueTask` must be awaited at most once and not stored; the synchronous-completion test checks
  `.IsCompleted`. Cross-language slip at "async conventions": stay positive and temporal.
- **Diagram name drift:** generated AFTER green code with locked names; Wave D gate verifies every SVG
  label + caption token exists in the example source (r06..r13 lesson).
- **TODO-caption trap:** the primary-figure include caption is owned by neither the diagram nor the code
  agent by default; Wave C explicitly replaces it (keeping `Figure NN.1: `), Wave D greps `TODO caption`
  == 0 (r05 lesson).
- **Voice regression incl. "capstone" (r11/r12/r13 lesson):** executor greps are not enough; each Wave-C
  prose agent runs the voice scanner to 0 ban-tier before handing back, checking the explicit avoid-list
  (genuinely/deliberately/earns-*/for-free/plainly/filler "actually"/"on purpose"/editorializing kickers)
  and "capstone" (-> "Final Project"). Wave D re-greps.
- **Word floor:** 2000-word hard floor per chapter; add the reserved density figure (#65/#68/#71) if a body
  passes ~2500 rather than trimming substance.
- **Standing mechanics:** Liquid `{{ }}` in fenced blocks (interpolated strings, collection expressions,
  generic `<T,T,T>` butting braces) wrapped in `{% raw %}`; `.slnx`/`global.json` wiring; CA1707 test-only;
  path leaks in quoted output; ch30 pins exactly the four runtime packages at 10.0.14, ch31/ch32 add none;
  standard net10.0 + xUnit v3 toolchain (NOT the Roslyn netstandard2.0 toolchain of ch27/28).

## r14 verification log

Opus-validated 2026-10-31 (branch `release/r14`). Verdict: PASS (PASS-WITH-NITS at validation; both
caption nits fixed in a repair pass: diagrams.yml #63 "a singleton snapshot" -> "a long-lived singleton
value" to match the correct include caption; diagrams.yml #71 "awaits real network I/O" -> "awaits real
work" since the fake awaits `Task.Delay(0, ct)`; the 17 watch-tier voice lines left as-is per prior
iterations). Part 10 (ch30-32, "Patterns the Platform Defines") is COMPLETE. The NEW short-caption rule
was applied and holds: all 9 include captions (25-34 words, 0 angle-bracket generics) and all 9
diagrams.yml captions are short human glosses, not identifier inventories. This iteration RETURNED to
the standard net10.0 + xUnit v3 toolchain.

- Tests (live): examples/30-the-options-pattern 12 passed / 0 failed; examples/31-tester-doer-and-the-
  result-debate 23 passed / 0 failed; examples/32-tap-and-async-conventions 10 passed / 0 failed; zero
  warnings under warnings-as-errors. scripts/verify.sh green (all three auto-discovered via
  *.Tests/*.csproj, whole suite 29 projects green). scripts/build.sh 0 Warning(s) 0 Error(s). jekyll clean.
- Toolchain: standard net10.0 library + xUnit v3 (xunit.v3 4.0.2, xunit.runner.visualstudio 4.0.1,
  Microsoft.NET.Test.Sdk 18.11.0, NSubstitute 6.2.0). ch30 library pins the four Options runtime
  packages (Microsoft.Extensions.DependencyInjection / .Options / .Options.ConfigurationExtensions /
  .Configuration) all at 10.0.14; ch31 + ch32 are pure BCL (zero PackageReference in the library
  csproj). All three test csprojs omit TestingPlatformDotnetTestSupport, set IsTestProject +
  IsPackable=false + NoWarn;CA1707. Each example has its own .slnx.
- ch30 ValidateOnStart: shipped the FULL direct IStartupValidator path (not the documented fallback) -
  resolve IStartupValidator, Validate() throws OptionsValidationException naming Endpoint +
  TimeoutSeconds + RetryBudget for invalid config, throws nothing for valid. OnChange deterministic:
  mutate the MemoryConfigurationProvider + IConfigurationRoot.Reload(), assert watcher.Observed CONTAINS
  RetryBudget==9 (contains, not an exact callback count). No file watcher, no Thread.Sleep.
  IOptionsSnapshot resolved inside provider.CreateScope().
- ch31 idiom: Result<T> readonly record struct (Success/Failure factories; Map composes/short-circuits);
  CA1000 (Result<T>) and CA1822 (two instance services) suppressed WITH justification. Both styles proven
  to agree over a shared input set; the tradeoff is stated, no side picked; Result<T> framed as a C#
  community idiom, no "like Rust"/functional/monadic framing.
- ch32 idiom: FakeCarrierGateway deterministic (DateTimeOffset injected via ctor; tests pass UnixEpoch; no
  wall-clock), ConfigureAwait(false) on every library await, ValueTask<bool> on the synchronous
  IsReachableAsync fast path, synchronous RecordingProgress double (not Progress<T>). Reflection tests
  enforce the Async suffix + CancellationToken on every Task/ValueTask member.
- Content: full spine each (primary figure after "Why this matters"); ## Build, run, observe quotes real
  path-sanitized dotnet test output (0 /home/, 0 username, counts 12/23/10); exactly one
  > **AI sidebar.** per chapter in "When not to use it"; "For deeper coverage" cites Microsoft Learn (+
  the Framework Design Guidelines via Learn for ch31) as primary; no GoF. Body prose ~2587 / ~2633 /
  ~2590 words, each with a density figure. em-dash == 0 across docs + .cs + README; 0 ban-tier; 0
  "capstone"; no numeric cross-chapter refs (ch32's only forward pointer is the generic "concurrency
  material").
- Figures: 3 per chapter, all paired .svg + .excalidraw on disk, registered in _data/diagrams.yml
  #63-#71 in chapter order (now 71 entries). Name-drift gate clean; NO-CROSS-LANGUAGE gate 0 hits across
  docs + example trees + new captions + new SVGs.

**Part 10 is COMPLETE (ch30-32).**
