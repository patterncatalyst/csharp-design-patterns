---
title: "r21 plan — Part 11 (ch35, ch36)"
output: false
render_with_liquid: false
---

# r21 — author ch35, ch36 (Part 11: Concurrency patterns, batch 2)

Opus-planned 2026-11-01. Branch `release/r21` (based on main@bc9ea56, r20 merge).
Part 11 batch 2 of 2+2+2 (r20=ch33+34 shipped; r21=ch35+36; r22=ch37+38).
**DETERMINISM is the #1 gate (carried from r20):** every test deterministic + fast,
ZERO Thread.Sleep / Task.Delay / wall-clock / Task.WhenAny-with-Delay; use TCS gates
(RunContinuationsAsynchronously), Writer.Complete drains, Task.WhenAll over known
counts, `!Task.IsCompleted` on a KNOWN-FULL bounded channel, pre-cancelled tokens,
Interlocked, Barrier. Library "I/O" suspends with `Task.Yield()`, never Task.Delay.
Token-accepting test calls pass `TestContext.Current.CancellationToken` (do NOT
suppress xUnit1051; the spike suppressed it, the book must not).

## Confirmed facts
- Part slug `concurrency-patterns`. Primary include names LOCKED: `35-monitor-object-and-active-object`, `36-producer-consumer-and-pipelines`. Both stubs have an EMPTY `## When not to use it` (fill it, one AI-sidebar each) + `## Build, run, observe` + `## For deeper coverage`.
- diagrams.yml = 100 entries (highest label #101); append labels **#102-#107** chapter order → 106 entries.
- POSA2 bibliography row LIVE (Wiley 2000, Schmidt et al.). No new row this batch (POSA3 is r22/ch37).
- **CS1996 re-verified on SDK 10.0.113** (BreakProof probe): `error CS1996` fires on await-inside-lock, uniform for `object` and `System.Threading.Lock`; CS9217 does NOT fire. Teach CS1996, NEVER CS9217 (MS Learn lock-semantics page is stale).
- Pure BCL: System.Threading.Channels / SemaphoreSlim / System.Threading.Lock / Task / TCS all in-framework on net10.0, no PackageReference. NO MEDI. Four test pins; omit TestingPlatformDotnetTestSupport; .slnx each; InternalsVisibleTo via csproj.

## ch35 design decisions (LOCKED)
- 4-stage progression over one shared non-thread-safe servant `StowagePlan`: Stage 1 `SynchronizedStowagePlan` (Monitor, `System.Threading.Lock`, SYNC, correct) → Stage 2 the CS1996 wall → Stage 3 `SemaphoreStowagePlan` (`SemaphoreSlim(1,1)`, compiles, serializes, but holds the lock across I/O, no FIFO/backpressure) → Stage 4 `StowageActiveObject` on bounded `Channel<T>` + TCS futures.
- **Stage 2 CS1996 = PROSE + ONE labelled non-compiling fenced snippet** (`// does not compile: CS1996`), NOT a .cs file. Wave A ships only compiling code.
- **Stage 3 SemaphoreSlim = SHIP as a real class** (awaits are inside WaitAsync/try/finally, not a lock statement → compiles). Two tests (serializes + cancellation); the FIFO/backpressure shortfall is PROSE only, never a flaky "it reorders" test.
- Library ledger `InMemoryStowageLedger.AppendAsync` uses `await Task.Yield()` (NOT the spike's Task.Delay(1)). Backpressure proven via internal `EnqueueAsync` seam + `!IsCompleted` on a known-full capacity-1 channel (NOT the spike's Task.WhenAny(Task.Delay(200))).
- POSA2 six-participant map (Proxy=ReserveAsync, MethodRequest=record, ActivationList=Channel, Scheduler=await-foreach, Servant=StowagePlan, Future=Task<T> via TCS); thesis = Future + Scheduler DISSOLVE into the language. When-not-to-use may name Orleans/Akka.NET/Dapr actors (all .NET — name, don't teach, NOT cross-language).

## LOCKED type names (Wave A builds verbatim)

### ch35 `examples/35-monitor-object-and-active-object/` — ns `StowageCoordination`, pure BCL library
Project `StowageCoordination.csproj`; tests `StowageCoordination.Tests`; `.slnx`; README. csproj: no runtime PackageReference; DefaultItemExcludes for Tests; `<InternalsVisibleTo Include="StowageCoordination.Tests" />`.
- `public readonly record struct SlotId(int Bay, int Row, int Tier);`
- `public readonly record struct ConsignmentId(string Value);`
- `public sealed record SlotRequirements(bool Reefer, bool Hazardous, int WeightKg);`
- `public sealed record SlotAssignment(ConsignmentId Consignment, SlotId Slot);`
- `public interface IStowageLedger { Task AppendAsync(SlotAssignment assignment, CancellationToken ct); }`
- `public sealed class InMemoryStowageLedger : IStowageLedger` — `AppendAsync`: `ct.ThrowIfCancellationRequested(); await Task.Yield(); lock(_entries){ _entries.Add(assignment); }`; `IReadOnlyList<SlotAssignment> Entries`.
- `public sealed class StowagePlan` (SERVANT, not thread-safe) — ctor `(IReadOnlyList<SlotId> slots)`; `SlotAssignment? TryReserve(ConsignmentId id, SlotRequirements req)` (non-atomic RMW over Dictionary; reefers only Tier==1); `int Count`.
- `public sealed class SynchronizedStowagePlan` (Stage 1) — `System.Threading.Lock _gate`; `SlotAssignment? Reserve(...)` => `lock(_gate){ return _plan.TryReserve(...); }` (sync).
- `public sealed class SemaphoreStowagePlan : IDisposable` (Stage 3) — `SemaphoreSlim(1,1)`; `async Task<SlotAssignment?> ReserveAsync(id, req, ct=default)` => WaitAsync/try{ TryReserve; if(a) await ledger.AppendAsync; return a }finally{ Release }.
- `public sealed class StowageActiveObject : IAsyncDisposable` (Stage 4) — private `record ReserveRequest(Consignment, Requirements, TaskCompletionSource<SlotAssignment?> Completion, CancellationToken Ct)`; ctor `(StowagePlan servant, IStowageLedger ledger, int capacity)` → bounded Channel (FullMode=Wait) + `Task.Run(RunAsync)`. `public async Task<SlotAssignment?> ReserveAsync(id,req,ct=default) => await await EnqueueAsync(...);` `internal async Task<Task<SlotAssignment?>> EnqueueAsync(id,req,ct=default)` (TEST SEAM: creates TCS, awaits Writer.WriteAsync, returns tcs.Task — await resolves when the WRITE lands). `private async Task RunAsync()` await-foreach ReadAllAsync: cancelled→TrySetCanceled; else TryReserve, if assignment await ledger.AppendAsync, TrySetResult; catch OCE→TrySetCanceled; catch Exception→TrySetException (loop survives). `DisposeAsync` → Writer.Complete(); await pump.

**Tests** (`StowageCoordination.Tests`: MonitorObjectTests.cs + SemaphoreStowagePlanTests.cs + ActiveObjectTests.cs + doubles GatedStowageLedger.cs [Entered TCS + _release TCS] + ThrowOnceStowageLedger.cs), **8**, token calls pass TestContext.Current.CancellationToken:
1. Synchronized_plan_hands_out_every_slot_at_most_once_under_concurrent_load (Barrier(16) + 16 Task.Run + WhenAll; distinct slots AND Count==returns)
2. Unsynchronized_servant_reserves_distinct_slots_in_sequence (sequential, correct single-threaded; race hazard is prose/figure NOT asserted)
3. Semaphore_plan_serializes_reservations_across_the_async_ledger_write (WhenAll over 500 vs InMemory ledger; distinct AND Count==returns)
4. Semaphore_plan_honours_a_cancelled_reservation_token (pre-cancelled CTS; ThrowsAnyAsync<OCE>)
5. Active_object_serializes_a_thousand_reservations_without_a_slot_twice (capacity 64; WhenAll over 1000; 1000 results, slots distinct)
6. A_full_bounded_channel_applies_backpressure_until_the_scheduler_drains (capacity 1, GatedStowageLedger; await EnqueueAsync(A); await Entered; await EnqueueAsync(B) [channel now FULL]; var pendingC=EnqueueAsync(C) NOT awaited; **assert !pendingC.IsCompleted**; Release; await pendingC; non-null)
7. A_queued_reservation_honours_a_cancelled_token (gate holds scheduler on A; EnqueueAsync(B,cts.Token); cts.Cancel(); Release; ThrowsAnyAsync<OCE> on B)
8. A_ledger_failure_surfaces_to_the_caller_and_the_scheduler_survives (ThrowOnceStowageLedger; first ReserveAsync ThrowsAsync<InvalidOperationException>; second returns non-null — loop survived)

### ch36 `examples/36-producer-consumer-and-pipelines/` — ns `HandlingEventIngest`, pure BCL library
Project `HandlingEventIngest.csproj`; tests `HandlingEventIngest.Tests`; `.slnx`; README. Same csproj conventions.
- `public readonly record struct TrackingId(string Value);`
- `public enum HandlingKind { Receive, Load, Unload, Customs, Claim }`
- `public sealed record HandlingEvent(TrackingId Tracking, HandlingKind Kind, int Sequence);`
- `public sealed record IngestReport(int Processed, IReadOnlyList<HandlingEvent> Observed, IReadOnlyDictionary<HandlingKind,int> ByKind);`
- `public sealed class HandlingEventPipeline` — ctor `(int capacity, int consumerCount, Func<HandlingEvent,CancellationToken,ValueTask>? onEvent=null)` → bounded Channel (FullMode=Wait), default onEvent `static async (_,_) => await Task.Yield()`, starts consumerCount ConsumeAsync tasks. `private async Task ConsumeAsync()` await-foreach ReadAllAsync: await _onEvent; _observed.Enqueue(e); Interlocked.Increment(_processed). `public ValueTask SubmitAsync(HandlingEvent e, CancellationToken ct) => _channel.Writer.WriteAsync(e, ct);` `public async Task<IngestReport> CompleteAndDrainAsync()` → Writer.Complete(); await Task.WhenAll(_consumers); build IngestReport.

**Tests** (`HandlingEventIngest.Tests`: PipelineTests.cs [1,2,4,5] + BackpressureTests.cs [3]), **5**, token calls pass TestContext.Current.CancellationToken:
1. Every_event_produced_is_consumed_exactly_once (capacity 8, consumers 4; 4 producers each submit N via WhenAll; CompleteAndDrainAsync; Processed==4N AND observed multiset==produced)
2. A_single_producer_order_is_preserved_by_a_single_consumer (capacity 4, consumers 1; submit Sequence 0..N-1 in order; Observed sequences == 0..N-1)
3. A_full_bounded_channel_applies_backpressure_to_the_producer (capacity 1, consumers 1, gated onEvent [Entered TCS + release TCS]; await SubmitAsync(A); await Entered; await SubmitAsync(B) [FULL]; var pendingC=SubmitAsync(C,ct).AsTask() NOT awaited; **assert !pendingC.IsCompleted**; release; await pendingC; CompleteAndDrain; 3 processed)
4. Completion_is_signalled_so_consumers_stop_after_the_last_event (capacity 8, consumers 3; submit K; CompleteAndDrainAsync returns [no hang]; Processed==K)
5. A_cancelled_submit_token_is_honoured (pre-cancelled CTS; ThrowsAnyAsync<OCE>)

**Determinism (both):** TCS gates, Barrier(N)/known counts, WhenAll over known sets, Writer.Complete+ReadAllAsync drains, `!IsCompleted` on known-full channel (replaces ALL wall-clock timeouts), pre-cancelled tokens, Interlocked. Task.Yield only in library defaults. ZERO Thread.Sleep/Task.Delay/DateTime/Stopwatch/WhenAny.

## Diagrams (Wave B, 1 agent owns diagrams.yml, #102 chapter order)
Amber; light-fill emphasis; ~960x560 C4; no baked-in numbers; short captions (no angle-bracket generics — "a bounded channel of requests" not Channel<T>); no cross-language/capstone; 0 em-dash in SVG text + captions.
- #102 `35-monitor-object-and-active-object` (ch35 PRIMARY): the four stages over one shared servant — Monitor lock (sync, correct), await-in-lock rejected at compile time, a semaphore async lock (serializes but holds across I/O), the Active Object where the bounded queue is the synchronization (destination, light-filled).
- #103 `35-active-object-participants` (ch35 secondary): POSA2's six participants mapped onto the code; Future + Scheduler greyed as dissolved into the language.
- #104 `35-channel-backpressure` (ch35 density): one-slot queue full, scheduler parked on the ledger, a third reservation's task stays incomplete until the queue drains, then completes.
- #105 `36-producer-consumer-and-pipelines` (ch36 PRIMARY): producers writing handling events into one bounded channel, several consumers draining concurrently, producer slows when full, completion signalled once.
- #106 `36-exactly-once-drain` (ch36 secondary): a known count flows in, writer completed, wait-all over consumers finishes, every event recorded exactly once; one producer's order preserved by a single consumer.
- #107 `36-pipeline-stages` (ch36 density): the pipeline generalized — receive/validate/record stages chained over bounded channels, backpressure propagating upstream from a slow stage.

## Waves
- **Wave A (code, 2 parallel):** A1 ch35 (Stages 1/3/4 compiling; Stage 2 NO code; 8/8), A2 ch36 (5/5). Each runs its suite 3x confirming green (determinism gate). Token calls use TestContext.Current.CancellationToken; no xUnit1051 suppression. No .cs contains await-inside-lock. flaky-primitive grep 0. Path-sanitized summaries.
- **Wave B (diagrams):** six figures #102-107 chapter order; short captions; no generics/cross-language/capstone; 0 em-dash in SVG text.
- **Wave C (prose, 2):** full spine; REPLACE TODO body; FILL `## When not to use it` (ONE AI-sidebar each); replace TODO captions; embed secondary+density figures; ch35 writes CS1996 as prose + one labelled non-compiling snippet (uniform for object + System.Threading.Lock), teaches CS1996 NEVER CS9217, notes Stage-3 semaphore shortfall, maps POSA2 six participants + dissolve thesis, names Orleans/Akka.NET/Dapr (.NET); ch36 Producer-Consumer-as-pattern/Channels-as-mechanism + pipelines; voice self-gate 0 ban-tier; >=2000 words.
- **Wave D (validate, Opus):** determinism 3x (ch35 8/8, ch36 5/5) + read backpressure test bodies; CS9217 grep 0 + CS1996 snippet labelled + no await-in-lock .cs + build.sh clean; no-wall-clock grep 0; AI-sidebar ==1 each; spine order + When-not-to-use + Build-run-observe + For-deeper-coverage present; idiomatic 0; voice 0 ban-tier; em-dash 0 (docs/.cs/README/SVGs/captions); capstone 0; no-cross-language 0; cross-chapter light; caption gate; TODO-caption 0; name-drift (ch35 #102-104, ch36 #105-107, yml 106); build.sh+verify.sh 0W/0E; jekyll clean; word floors; toolchain pins; path-sanitized. Write verification log.

## Acceptance criteria
Per chapter: body >=2000; full spine w/ primary figure after Why; filled When-not-to-use (one AI-sidebar) before For-deeper-coverage; deterministic tests 3x (ch35 8/8, ch36 5/5); pure BCL; path-sanitized Build-run-observe. ch35 proves the 4-stage progression (Monitor serializes; servant correct single-threaded; semaphore serializes across async ledger + honours cancel; Active Object serializes 1000 w/ no slot twice, deterministic backpressure, cancelled queued token, survives ledger exception); CS1996 prose+snippet, CS9217 absent; POSA2 map + dissolve thesis; Orleans/Akka/Dapr named. ch36 proves exactly-once drain, single-producer order, deterministic backpressure, completion terminates ReadAllAsync, cancelled submit honoured; pattern/mechanism framing; distinct from ch35.
Iteration: POSA2 via /bibliography/ (no new row); jekyll clean; diagrams.yml 106 #102-107 chapter order; .slnx each; four pins + omit TestingPlatformDotnetTestSupport; pure BCL/no MEDI; InternalsVisibleTo; no cross-language; names unchanged; captions short; net10.0/C#14; AI-sidebar ==1 each; no-wall-clock 0; CS9217 0.

## Risks
- **Determinism / timeout-free backpressure (HIGH):** the internal EnqueueAsync/SubmitAsync seam must resolve when the WRITE lands (so the capacity-1 channel is provably full + gated consumer provably parked before asserting !IsCompleted). If the seam awaits completion instead of the write, it deadlocks. Wave A implements as specified; Wave D reads both test bodies + runs 3x. No WhenAny/Delay fallback.
- **CS1996 compile-error presentation (MED):** Stage 2 = prose + labelled non-compiling snippet only; any await-in-lock .cs breaks build.sh. Wave D greps tree + build output; CS9217 grep 0.
- **Channel drain correctness (MED, ch36):** CompleteAndDrainAsync must Writer.Complete() then WhenAll consumers; missing Complete() hangs ReadAllAsync. Wave D confirms test 4 returns under 3x.
- **xUnit1051 (MED):** pass TestContext.Current.CancellationToken; do NOT suppress.
- **Stage-3 scope creep (LOW):** ship SemaphoreStowagePlan with 2 tests; shortfall prose only.
- **Task.Yield not Task.Delay (LOW):** library defaults Task.Yield only; Wave D grep.
- **Standing mechanics:** `{% raw %}` for `{{`/`}}`; .slnx/global.json; CA1707 test-only (not xUnit1051); path leaks; InternalsVisibleTo csproj; #102 order; TODO-caption 0; word floor; AI-sidebar ==1 each; captions no generics; em-dash 0.

## Deferred (r22)
- ch37 Pools (connection pool rent/return/exhaustion-then-release via TCS gate + ArrayPool<T> reuse); ADD POSA3 bibliography row (Wiley 2004).
- ch38 Classic GoF under concurrency (Observer across threads, Iterator→IAsyncEnumerable, Decorator over async, Command as Task, immutability as strategy).

## r21 verification log

Validated 2026-11-01 (Opus, Wave D, adversarial — files read + checks re-run, no prior self-report trusted).

### Determinism (#1 gate)
- MET — ch35 `StowageCoordination.slnx` x3: total 8, failed 0 every run (858/779/756 ms).
- MET — ch36 `HandlingEventIngest.slnx` x3: total 5, failed 0 every run (856/743/734 ms).
- MET — flaky-primitive grep over both .cs trees (excl. obj/): no executable use of Thread.Sleep / Task.Delay / DateTime.Now / DateTime.UtcNow / Stopwatch / Task.WhenAny / ThreadPool.SetMinThreads / ModuleInitializer. Only hits are doc-comment text in PipelineTests.cs (lines 11-12) stating those primitives are NOT used (NIT, not a real primitive). Task.Yield only in InMemoryStowageLedger.AppendAsync and HandlingEventPipeline onEvent default (both allowed).
- MET — both backpressure test bodies read: ch35 `A_full_bounded_channel_applies_backpressure_until_the_scheduler_drains` and ch36 `A_full_bounded_channel_applies_backpressure_to_the_producer` each gate on a TCS Entered signal (WaitAsync with token only, NO timeout), fill a capacity-1 channel, assert `!pendingC.IsCompleted`, then Release and drain. GatedStowageLedger uses Entered+release TCS, no wall clock.
- MET — ch35 tests 6/7/8 (backpressure, cancelled-queued-token via `cts.Cancel()` then ThrowsAny<OCE>, ThrowOnce ledger with loop-survives second success) and ch36 tests 1-5 all read and match the locked design.

### ch35 CS1996 + Stage-2-ships-no-code
- MET — `grep -ci cs9217` over _docs/35 == 0. CS1996 taught (4 mentions) with a clearly-labelled non-compiling snippet (`// does not compile: CS1996 ... // CS1996 here`).
- MET — no shipped .cs in examples/35 has await inside a lock statement (awk brace-scan clean). Only lock bodies are synchronous: SynchronizedStowagePlan.Reserve and InMemoryStowageLedger's lock around _entries.Add AFTER `await Task.Yield()`. build.sh clean (would fail CS1996 otherwise).
- MET — body maps POSA2's six Active Object participants (Proxy/Method Request/Activation List/Scheduler/Servant/Future) and states the Scheduler+Future dissolve thesis; names Orleans, Akka.NET, Dapr actors as heavier .NET options (all .NET, not cross-language).

### Build / test / site
- MET — `scripts/build.sh`: Build succeeded, 0 Warning(s), 0 Error(s) across all examples. No pragma/SuppressMessage in either new example; only NoWarn is CA1707 in the two test csprojs.
- MET — `scripts/verify.sh`: exit 0 under `set -eu`; glob `*/*.Tests/*.csproj` includes both new examples; all suites green.
- MET — `bundle exec jekyll build`: clean, done in 0.475s, no Liquid errors (gem-extension warnings only, non-fatal).

### Structure
- MET — spine order both docs: Learning objectives → Why this matters → primary FIGURE (after Why) → body → `## Build, run, observe` → `## When not to use it` → `## For deeper coverage`.
- MET — `<blockquote class="ai-sidebar"` count == 1 each; no inline `**AI sidebar.**`; no `{: .ai-sidebar}` IAL. `## When not to use it` present, filled, immediately before `## For deeper coverage`; exactly one `## For deeper coverage` each.
- MET — `TODO caption` == 0 both; `excalidraw.html` == 3 each. Captions keep `Figure NN.N:` prefix, 28-43 words, no angle-bracket generics, 0 em-dash.
- MET — figure name resolution: all six name= values resolve to diagrams/<name>.svg + .excalidraw + a diagrams.yml entry. ch35 embeds 102/103/104 (35-monitor-object-and-active-object, 35-active-object-participants, 35-channel-backpressure); ch36 embeds 105/106/107 (36-producer-consumer-and-pipelines, 36-exactly-once-drain, 36-pipeline-stages). diagrams.yml == 106 entries, 6 new in chapter order, sections "Ch. 35"/"Ch. 36". Six new SVGs: 0 em-dash, 0 angle-bracket generics in drawn text.

### Idiomatic
- MET — lgtm-idiomatic-review scan.sh: 0 matching lines each. No `#pragma warning disable`, no `[SuppressMessage]` in either example.

### Voice + prose
- MET — lgtm-professional-voice scan: 0 ban-tier each. Watch-tier (non-blocking): ch35 3 lines (hedging x2, intensifiers x1); ch36 6 lines (quietly/silently x3, deliberate x1, hedging x1, simply/obviously x1).
- MET — `grep -ci '\bactually\b'` == 0 both.
- MET — em-dash == 0 across both _docs, all new .cs, both READMEs, the six new SVGs, and the six new diagrams.yml captions.
- MET — `grep -ci capstone` == 0 everywhere (docs, examples, SVGs).
- MET — no cross-language: java / c++ / jvm / spring / rust / goroutine / golang / bare-go / virtual-by-default all 0 across docs, .cs, READMEs, SVGs, yml captions (41-seams pre-existing name excluded per instructions).
- MET — cross-chapter: neither doc references another by number or by title/topic.

### Word counts
- MET — ch35 ≈ 2880 words (floor 2000), ch36 ≈ 3214 words (floor 2000). Both carried by 3 figures.

### NITS (non-blocking)
1. PipelineTests.cs lines 11-12 name Thread.Sleep / Task.Delay / Task.WhenAny inside a `///` doc-comment that documents the determinism guarantee. Not executable; grep intent (no real flaky primitive) is satisfied. Left as-is (editing explanatory test docs is a content call, not a mechanical fix).
2. ch35 front-matter title/description and ch36 front-matter description contain `Channel<T>`/`Task<T>` angle generics. The caption/SVG generics ban does not cover front matter; rendered as page title/meta, not a figure caption. No action.

### Verdict: PASS-WITH-NITS (two cosmetic nits above; zero blockers).
