---
title: "r20 plan — Part 11 (ch33, ch34)"
output: false
render_with_liquid: false
---

# r20 — author ch33, ch34 (Part 11: Concurrency patterns, batch 1)

Opus-planned 2026-11-01. Branch `release/r20` (based on main@bed9772, r19 merge).
Part 11 is the final content block, six chapters, most research-heavy + most
flake-prone. **Split: 2+2+2 — r20 = ch33+ch34, r21 = ch35+ch36, r22 = ch37+ch38.**
Determinism is the #1 risk: every test deterministic + fast, NO Thread.Sleep /
Task.Delay / wall-clock assertions; use TCS gates, Barrier, Interlocked,
Task.WhenAll over known sets, ValueTask.IsCompletedSuccessfully, Assert.Same.
Every token-accepting call in tests passes `TestContext.Current.CancellationToken`
(the ch32 convention; do NOT suppress xUnit1051).

## Confirmed facts
- Part slug `concurrency-patterns` (all six stubs + `_parts/11-concurrency-patterns.md`).
- Primary include names LOCKED: `33-why-dotnet-chose-stackless-async`, `34-thread-safe-creation`.
- ch33 stub LACKS `## When not to use it` (framing signal) → ADD it (one AI-sidebar; "when not to reach for async": CPU-bound, async-over-sync, fire-and-forget). ch34 has the empty heading → fill it (one AI-sidebar). Gate: both == 1.
- diagrams.yml = 94; append from **#96** chapter order → 101 after this batch.
- Bibliography POSA2 is LIVE (serves ch34 DCL note). No new row needed this batch. (r22/ch37 must add POSA3.)
- Both examples pure BCL (Task/ValueTask/TCS/Interlocked/Volatile/Barrier/Lazy<T>/System.Threading.Lock all in-framework on net10.0). NO MEDI. Standard four test pins; omit TestingPlatformDotnetTestSupport; .slnx each; InternalsVisibleTo via csproj.

## LOCKED type names (Wave A builds verbatim)

### ch33 `examples/33-why-dotnet-chose-stackless-async/` — ns `SailingBookings`, pure BCL
Project `SailingBookings.csproj`; tests `SailingBookings.Tests`; `.slnx`; README. csproj: no runtime PackageReference; DefaultItemExcludes for Tests; `<InternalsVisibleTo Include="SailingBookings.Tests" />`.
- `public readonly record struct BookingRequest(string ConsignmentId);`
- `public sealed record BookingReceipt(string ConsignmentId, int LedgerSequence);`
- `public interface IBookingLedger { Task<int> ConfirmAsync(string consignmentId, CancellationToken ct); }`
- `public sealed class InMemoryBookingLedger : IBookingLedger` — `ConfirmAsync`: `ct.ThrowIfCancellationRequested(); await Task.Yield(); return Interlocked.Increment(ref _sequence);` (private int _sequence). Deterministic, no wall-clock (Task.Yield, never Task.Delay).
- `public sealed class SailingBookingDesk(IBookingLedger ledger, int sailingCapacity)`:
  - `public async Task<BookingReceipt> AcceptAsync(BookingRequest request, CancellationToken ct = default)` — seq via ledger, Interlocked.Increment(_accepted), return receipt.
  - `public async Task<IReadOnlyList<BookingReceipt>> AcceptAllAsync(IReadOnlyCollection<BookingRequest> requests, CancellationToken ct = default)` — `requests.Select(r => AcceptAsync(r, ct)).ToList()` then `await Task.WhenAll`.
  - `public ValueTask<bool> HasCapacityAsync(CancellationToken ct = default) => new(Volatile.Read(ref _accepted) < sailingCapacity);`
- Test double `GatedBookingLedger : IBookingLedger` — TCS `_release` (RunContinuationsAsynchronously) + Interlocked arrival counter + TCS `_allArrived` (completed at target) + ConcurrentDictionary recording `Environment.CurrentManagedThreadId` in the PRE-await portion only; `Release()` sets `_release`.

**Tests** (split BookingDeskTests.cs + StacklessModelTests.cs), **6**, all token-accepting calls pass `TestContext.Current.CancellationToken`:
1. Accepting_a_booking_returns_a_receipt_from_the_ledger (InMemory; LedgerSequence>=1)
2. A_thousand_bookings_suspend_concurrently_without_a_thread_each (Gated target 1000; build 1000 tasks via .Select().ToList() on test thread; await _allArrived; assert counter==1000, distinct pre-await thread count==1, none completed; Release(); WhenAll; 1000 receipts)
3. Every_booking_receives_a_distinct_ledger_sequence (AcceptAllAsync over 1000; sequence set == {1..1000})
4. A_capacity_check_completes_synchronously_without_allocating_a_task (vt.IsCompletedSuccessfully && vt.Result)
5. A_cancelled_booking_token_is_honoured (pre-cancelled CTS; ThrowsAnyAsync<OperationCanceledException>)
6. A_booking_resumes_and_completes_after_the_awaited_confirmation (Gated; not complete before Release(); receipt after)

### ch34 `examples/34-thread-safe-creation/` — ns `ThreadSafeTariffs`, pure BCL
Project `ThreadSafeTariffs.csproj`; tests `ThreadSafeTariffs.Tests`; `.slnx`; README. csproj: no runtime PackageReference; DefaultItemExcludes for Tests; `<InternalsVisibleTo Include="ThreadSafeTariffs.Tests" />`.
- `public sealed class TariffTable` — ctor `(IReadOnlyDictionary<string, decimal> ratesByLane)`; `public decimal RatePerKilogram(string laneCode)`. Reference type (Assert.Same unambiguous).
- `public interface ITariffBuilder { TariffTable Build(); }`; `StandardTariffBuilder` builds `{"STD":2.40m,"EXP":3.60m}`.
- `public sealed class CountingTariffBuilder(ITariffBuilder inner) : ITariffBuilder` — `public int BuildCount { get; }` (Interlocked); increments then delegates.
- `public sealed class LazyTariffProvider(ITariffBuilder builder)` — `Lazy<TariffTable>(builder.Build, LazyThreadSafetyMode.ExecutionAndPublication)`; `Value`; `IsValueCreated`.
- `public sealed class LockTariffProvider(ITariffBuilder builder)` — `System.Threading.Lock _gate`; `Value { get { lock(_gate) { return _table ??= builder.Build(); } } }` (SYNCHRONOUS, no await in body → compiles clean).
- `public static class TariffRegistry { static readonly TariffTable _instance = new StandardTariffBuilder().Build(); public static TariffTable Instance => _instance; }` (CLR type-initializer singleton).
- DCL: PROSE ONLY — a labelled historical fenced snippet in the body ("not written today; reach for Lazy<T>"). No DCL .cs ships (idiomatic-review stays clean; respects idiomatic-modern-C#-only outside Part 13).

**Tests** (split ThreadSafeCreationTests.cs + TariffTableTests.cs), **6**:
1. Lazy_provider_builds_the_tariff_table_exactly_once_under_concurrent_first_use (CountingBuilder + Barrier(64); BuildCount==1; all Assert.Same)
2. Lazy_provider_does_not_build_until_the_value_is_touched (IsValueCreated false/BuildCount 0 → read → true/1)
3. Lazy_provider_caches_a_build_failure_and_rethrows_it (builder throws; first throws; second Assert.Same cached exception)
4. Lock_provider_builds_the_tariff_table_exactly_once_under_concurrent_first_use (Barrier(64); BuildCount==1; Assert.Same)
5. Static_singleton_hands_every_caller_the_same_instance (64 concurrent reads via WhenAll; all Assert.Same; reference identity only, no process-global count)
6. Tariff_table_returns_the_expected_rate_for_a_lane (RatePerKilogram("STD")==2.40m)

**Determinism (both):** TCS gates (RunContinuationsAsynchronously), Barrier(N)/known counts, Interlocked, Task.WhenAll over known sets, ValueTask.IsCompletedSuccessfully, Assert.Same, cached-exception identity, pre-cancelled tokens. ZERO Thread.Sleep/Task.Delay/wall-clock. Task.Yield only in the library default ledger. Build-once assertions are invariant even if the race never manifests (Barrier only maximizes contention).

## Diagrams (Wave B, 1 agent owns diagrams.yml, append #96 chapter order)
Amber; light-fill emphasis; ~960x560 C4; no baked-in numbers; short captions; no cross-language/capstone; 0 em-dash in SVG text + captions.
- #96 `33-why-dotnet-chose-stackless-async` (ch33 PRIMARY): thousands of bookings awaiting one sailing's ledger, suspended as small state objects, served by a handful of pooled threads.
- #97 `33-stackless-vs-stackful` (ch33 secondary): stackful green threads (one stack per booking, memory grows — prototyped+rejected by the .NET team) vs stackless state machines (small heap state per await, thread returns to pool).
- #98 `33-suspended-without-a-thread` (ch33 density): the 1,000-bookings scenario — pre-await on one thread, all parked at the ledger await, zero threads held while suspended, release drains via one wait-all.
- #99 `34-thread-safe-creation` (ch34 PRIMARY): many agents hitting the tariff value at once, lazy ExecutionAndPublication building once, every caller handed the one instance.
- #100 `34-lazy-vs-lock-vs-static` (ch34 secondary): the three modern idioms (lazy, lock-guarded field, static type-initializer) each building once, DCL greyed out as the historical form they replace.
- #101 `34-exception-caching` (ch34 density): the caching gotcha — a failed build is remembered, every later read rethrows the same cached exception, vs a refreshing cache that retries.

## Waves
- **Wave A (code, 2 parallel):** A1 ch33 green (6/6), A2 ch34 green (6/6); per LOCKED names; each runs `dotnet test` 3x confirming 6/6 every run (determinism gate); token-accepting calls use TestContext.Current.CancellationToken (no xUnit1051 suppression); path-sanitized summaries.
- **Wave B (diagrams):** six figures #96-101 chapter order; short captions; no generics/cross-language/capstone; 0 em-dash in SVG text.
- **Wave C (prose, 2):** full spine; REPLACE TODO body; ADD/FILL `## When not to use it` (ONE AI-sidebar each); replace TODO caption; embed secondary+density figures; DATE the green-threads-rejected claim (ch33); DCL historical note (ch34); name patterns in place; voice self-gate 0 ban-tier; >=2000 words.
- **Wave D (validate, Opus):** AI-sidebar ==1 each; When-not-to-use + Build-run-observe present both; DETERMINISM gate (dotnet test 3x per example, 6/6 each); ch34 System.Threading.Lock compiles clean (sync, no await in body); idiomatic 0 on shipped .cs (no DCL .cs); voice 0 ban-tier; em-dash 0 (docs/.cs/README/SVGs/captions); TODO-caption 0; capstone 0; no-cross-language 0; cross-chapter light; caption gate; name-drift (ch33 #96-98, ch34 #99-101, yml 101); dotnet test 6/6 each + verify.sh + build.sh 0W/0E; jekyll clean; word counts; toolchain pins; path-sanitized. Write verification log.

## Acceptance criteria
Per chapter: body >=2000; full spine w/ primary figure after Why; When-not-to-use (one AI-sidebar) before For-deeper-coverage; 6/6 deterministic tests (3x); pure BCL; path-sanitized Build-run-observe. ch33 proves the stackless suspension payoff (1000 suspend on one pre-await thread, distinct sequences, synchronous ValueTask capacity check, cancellation, resume-after-await); green-threads rejection DATED. ch34 proves build-exactly-once under Barrier-aligned contention for Lazy + Lock, lazy-until-touched, cached-failure rethrow, static singleton identity, lane rate; DCL historical-prose-only.
Iteration: POSA2 via /bibliography/ + MS Learn (no new row); jekyll clean; diagrams.yml #96-101 chapter order (101); .slnx each; four test pins + omit TestingPlatformDotnetTestSupport; pure BCL/no MEDI; InternalsVisibleTo; no cross-language; names unchanged; captions short; net10.0/C#14; AI-sidebar ==1 each.

## Risks
- **Determinism (HIGH):** ch33 test2 thread-count==1 relies on synchronous .Select().ToList() enumeration + pre-await thread capture (no Task.Run/Yield before first await). ch34 build-once invariant under the race (Barrier only maximizes contention). No Sleep/Delay/wall-clock. Wave A+D run 3x.
- **xUnit1051 (MED):** pass TestContext.Current.CancellationToken; do NOT suppress.
- **ch34 DCL rule tension (MED):** DCL historical PROSE only, no .cs; idiomatic-review 0 on shipped code.
- **ch34 static-singleton (MED):** assert reference identity only (order-independent), not a process-global build count.
- **ch33 framing When-not-to-use (LOW):** adding it (one AI-sidebar) per plan; fallback = closing reflective section carrying the AI-sidebar (gate stays ==1).
- **System.Threading.Lock (LOW):** sync use only here; CS1996 await-in-lock teaching belongs to ch35 (r21), do NOT import into ch34.
- **Standing mechanics:** `{% raw %}` for `{{`/`}}`; .slnx/global.json; CA1707 test-only; path leaks; InternalsVisibleTo csproj; #96 order; TODO-caption 0; word floor; AI-sidebar ==1 each.

## Deferred (r21/r22 one-line sketches)
- r21 ch35 Monitor Object & Active Object on Channel<T> (ns `StowageCoordination`, rebuild GREEN spike): SynchronizedStowagePlan (Monitor, sync, correct) → CS1996 break on await-in-lock → SemaphoreSlim(1,1) (lock-across-I/O, no FIFO/backpressure) → StowageActiveObject on bounded Channel<T> with TCS futures; teach CS1996 NEVER CS9217 (spike-retracted, MS docs stale); When-not-to-use names Orleans/Akka.NET/Dapr (all .NET, name don't teach); no p99/wall-clock test.
- r21 ch36 Producer-Consumer & pipelines (bounded Channel<T>, drain via Writer.Complete()+ReadAllAsync + WhenAll over known counts).
- r22 ch37 Pools (connection pool rent/return/exhaustion-then-release via TCS gate + ArrayPool<T> buffer reuse); **ADD POSA3 bibliography row (Wiley 2004)**.
- r22 ch38 Classic GoF under concurrency (Observer across threads, Iterator→IAsyncEnumerable, Decorator over async, Command as Task, immutability as strategy); most novel.

## r20 verification log

<!-- Opus fills this in at Wave D. -->
