---
title: "r22 plan — Part 11 (ch37, ch38)"
output: false
render_with_liquid: false
---

# r22 — author ch37, ch38 (Part 11: Concurrency patterns, batch 3 — FINAL)

Opus-planned 2026-11-01. Branch `release/r22` (based on main@191eccf, r21 merge).
Part 11 batch 3 of 2+2+2. **This iteration FINISHES Part 11 and all 47 chapters (0-46).**
DETERMINISM is the #1 gate (carried from r20/r21): every test deterministic + fast;
ZERO Thread.Sleep / Task.Delay / wall-clock / Task.WhenAny-with-timeout. Prove scarcity
via a channel that blocks + `!Task.IsCompleted` on a KNOWN-exhausted pool; drain
`await foreach` via Writer.Complete() + WhenAll over known counts; prove
Observer-across-threads via Barrier/Interlocked + exact counts; library "I/O" suspends
with `Task.Yield()`. Token-accepting test calls pass `TestContext.Current.CancellationToken`
(no xUnit1051 suppression). ZERO suppressions beyond CA1707 — design around analyzers,
never suppress.

## Confirmed facts
- Part slug `concurrency-patterns`. Primary include names LOCKED: `37-pools-object-connection-arraypool`, `38-classic-gof-under-concurrency`. Both stubs have EMPTY `## When not to use it` (fill, one AI-sidebar each) + `## Build, run, observe` + `## For deeper coverage`.
- diagrams.yml = 106; append labels **#108-#113** chapter order → 112 entries.
- **POSA3 is NOT a bibliography row yet** (only a TODO comment ~line 113). ch37 Wave C ADDS the POSA3 row (Kircher & Jain, Wiley 2004) + deletes the TODO line. Only new row this batch (GoF + POSA2 already live).
- Pure BCL both (ArrayPool<T> / System.Collections.Immutable / Channels / Interlocked / Barrier / TCS all in-framework). NO MEDI. Four test pins; omit TestingPlatformDotnetTestSupport; .slnx each; InternalsVisibleTo via csproj.

## LOCKED type names (Wave A builds verbatim)

### ch37 `examples/37-pools-object-connection-arraypool/` — ns `GatewayPooling`, pure BCL library
Project `GatewayPooling.csproj`; tests `GatewayPooling.Tests`; `.slnx`; README. csproj: no runtime PackageReference; DefaultItemExcludes for Tests; InternalsVisibleTo.
Connection pool:
- `public interface IGatewayConnection { int Id { get; } Task<string> ExchangeAsync(string manifest, CancellationToken ct); }`
- `public sealed class GatewayConnection : IGatewayConnection` — ctor `(int id)`; `ExchangeAsync`: `ct.ThrowIfCancellationRequested(); await Task.Yield(); return $"ACK:{manifest.Length}";`
- `public sealed class GatewayConnectionPool : IAsyncDisposable` — ctor `(int size, Func<int, IGatewayConnection> connectionFactory)` prefills a `Channel<IGatewayConnection>` with `size` connections; `int Size`; `int Available => _available.Reader.Count`; `ValueTask<IGatewayConnection> RentAsync(CancellationToken ct=default) => _available.Reader.ReadAsync(ct)` (blocks when empty); `void Return(IGatewayConnection c) => _available.Writer.TryWrite(c)`; `ValueTask<GatewayLease> RentLeaseAsync(ct=default)`; `DisposeAsync` completes writer + disposes any IDisposable connections (ch35 precedent, CA1816-clean).
- `public readonly record struct GatewayLease(GatewayConnectionPool Pool, IGatewayConnection Connection) : IDisposable` — `Dispose() => Pool.Return(Connection);`
ArrayPool buffer reuse:
- `public readonly record struct ManifestLine(string Code, string Value);`
- `public sealed class ManifestWriter` — ctor `(ArrayPool<byte> pool)`; `public byte[] Serialize(IReadOnlyList<ManifestLine> lines)` rents scratch (deterministic upper-bound), encodes UTF-8, returns `scratch.AsSpan(0,written).ToArray()`, `finally _pool.Return(scratch, clearArray: true)`; `public static IReadOnlyList<ManifestLine> Parse(ReadOnlySpan<byte> encoded)`.
- Test double `RecordingArrayPool : ArrayPool<byte>` wrapping `ArrayPool<byte>.Create()`; records rented/returned arrays + clearArray flag + counts.

**Tests** (`GatewayPooling.Tests`: ConnectionPoolTests.cs + ManifestBufferTests.cs + RecordingArrayPool.cs), **9**, token calls pass TestContext.Current.CancellationToken:
1. A_rented_connection_comes_from_the_pool (size 3; RentAsync; non-null, Id 0..2, Available==2)
2. Every_pooled_connection_has_a_distinct_identity (rent all N; Ids == {0..N-1})
3. An_exhausted_pool_blocks_the_renter_until_a_connection_is_returned (size 2; rent A,B [Available==0]; var pendingC=RentAsync().AsTask(); **assert !pendingC.IsCompleted**; Return(A); await pendingC non-null)
4. A_returned_connection_is_handed_to_the_next_waiting_renter (as 3; awaited pendingC Assert.Same returned A)
5. A_cancelled_rent_token_is_honoured (exhaust; var cts; pending=RentAsync(cts.Token).AsTask(); assert !pending.IsCompleted; cts.Cancel(); ThrowsAnyAsync<OCE>)
6. A_disposed_lease_returns_its_connection_to_the_pool (size 1; using lease → Available 0; after → Available 1)
7. A_pooled_connection_exchanges_a_manifest (ExchangeAsync("MANIFEST-XYZ") == "ACK:11")
8. Serializing_a_manifest_returns_the_rented_buffer_to_the_pool_and_clears_it (RecordingArrayPool; Rent 1, Return 1, clearArray true)
9. A_returned_buffer_is_reused_by_the_next_serialization (RecordingArrayPool over Create(); Serialize twice identical; Assert.Same(firstReturned, secondRented) + Parse round-trip)

### ch38 `examples/38-classic-gof-under-concurrency/` — ns `ConcurrentHandling`, pure BCL library
Project `ConcurrentHandling.csproj`; tests `ConcurrentHandling.Tests`; `.slnx`; README. Same csproj conventions.
Shared (immutability-as-strategy): `public readonly record struct TrackingId(string Value);` `public enum HandlingKind { Receive, Load, Unload, Customs, Claim }` `public sealed record HandlingEvent(TrackingId Tracking, HandlingKind Kind, int Sequence);` `public sealed record HandlingReceipt(TrackingId Tracking, int Sequence);`
Observer across threads: `public interface IHandlingObserver { void OnHandling(HandlingEvent e); }` `public sealed class HandlingEventHub` — `private ImmutableArray<IHandlingObserver> _observers = [];` `Subscribe` via `ImmutableInterlocked.Update(ref _observers, (l,o)=>l.Add(o), observer)` returns an IDisposable Subscription (Remove on Dispose); `void Raise(HandlingEvent e)` → `var snapshot=_observers; foreach(var o in snapshot) o.OnHandling(e);` `int ObserverCount`. (NOT the `event` keyword — avoids CA1003.)
Iterator as IAsyncEnumerable: `public sealed class HandlingEventFeed` — unbounded Channel; `ValueTask PublishAsync(HandlingEvent e, CancellationToken ct)`; `void Complete()`; `public async IAsyncEnumerable<HandlingEvent> StreamAsync([EnumeratorCancellation] CancellationToken ct=default) { await foreach (var e in _channel.Reader.ReadAllAsync(ct)) yield return e; }`
Decorator over async: `public interface IManifestValidator { Task<bool> ValidateAsync(HandlingEvent e, CancellationToken ct); }` `ManifestValidator` (`await Task.Yield(); return e.Sequence>=0;`); `public sealed class CountingValidatorDecorator(IManifestValidator inner) : IManifestValidator` — Interlocked `Calls`; `await inner.ValidateAsync`.
Command as Task: `public interface IHandlingLedger { Task<int> AppendAsync(HandlingEvent e, CancellationToken ct); }` `InMemoryHandlingLedger` (Task.Yield + Interlocked seq + lock(_entries); `IReadOnlyList<HandlingEvent> Entries` snapshot copy); `public interface IHandlingCommand { Task<HandlingReceipt> ExecuteAsync(CancellationToken ct); }` `RecordHandlingCommand(HandlingEvent evt, IHandlingLedger ledger)`; `public sealed class CommandInvoker { Task<IReadOnlyList<HandlingReceipt>> RunAllAsync(IReadOnlyCollection<IHandlingCommand> commands, CancellationToken ct) }` (Select→WhenAll).

**Tests** (`ConcurrentHandling.Tests`: ObserverTests + IteratorTests + DecoratorTests + CommandTests + ImmutabilityTests + doubles CountingObserver, UnsubscribingObserver), **10**, token calls pass TestContext.Current.CancellationToken:
1. Every_subscribed_observer_sees_every_raised_event
2. Raising_across_threads_delivers_every_event_exactly_once (Barrier(N) + Interlocked; Count==N, multiset==produced)
3. Unsubscribing_during_a_raise_leaves_the_in_flight_snapshot_intact (UnsubscribingObserver disposes B mid-raise; no exception, both notified that raise; second raise only A)
4. The_feed_streams_every_published_event_in_order (publish 0..K-1, Complete(), await foreach; == 0..K-1)
5. A_cancelled_stream_token_is_honoured (pre-cancelled CTS to StreamAsync; ThrowsAnyAsync<OCE>)
6. A_producer_and_a_consumer_run_concurrently_over_the_feed (producer publishes K + Complete(); consumer await foreach counts; WhenAll; K)
7. The_decorator_delegates_every_async_call_to_its_inner_validator (WhenAll over N ValidateAsync; Calls==N, all true)
8. A_handling_command_records_its_event_and_returns_a_receipt
9. The_invoker_runs_every_command_and_returns_a_receipt_each (N commands; N receipts; sequences {1..N})
10. An_immutable_handling_event_is_safe_to_share_across_threads (Barrier(N) readers + `with`-copies; original unchanged, value equality)

**Determinism (both):** channel blocking, `!IsCompleted` on known-exhausted pool, Barrier(N)+Interlocked+exact counts, WhenAll over known sets, Writer.Complete()+await-foreach drains, pre-cancelled tokens, Assert.Same/HashSet, single-threaded sequential ArrayPool reuse (Create(), NOT Shared). Task.Yield only in library I/O. ZERO Thread.Sleep/Task.Delay/DateTime/Stopwatch/WhenAny.

## Bibliography (ch37 Wave C): add the POSA3 row after the POSA2 `</tr>`, mirroring POSA2 markup
`Pattern-Oriented Software Architecture, Volume 3: Patterns for Resource Management` — Michael Kircher, Prashant Jain — Wiley — 2004 — category "Resource management"; Wiley publisher link (verify it resolves, ISBN 9780470845257); DELETE the POSA3 TODO comment line. Only new row this batch.

## Diagrams (Wave B, 1 agent owns diagrams.yml, #108 chapter order)
Amber; light-fill emphasis; ~960x560 C4; no baked-in numbers; short captions (no angle-bracket generics); no cross-language/capstone; 0 em-dash in SVG text + captions.
- #108 `37-pools-object-connection-arraypool` (ch37 PRIMARY): fixed-size carrier-gateway connection pool handing out + taking back connections, an exhausted pool parking a renter; beside it a scratch buffer rented from + returned to an array pool.
- #109 `37-connection-pool-exhaustion` (ch37 secondary): a 2-connection pool fully rented, a third rent parked/incomplete, a return waking it with the exact connection returned.
- #110 `37-arraypool-buffer-reuse` (ch37 density): rent/fill/return-cleared, next rent gets the same buffer, vs allocating a fresh array every time + the garbage.
- #111 `38-classic-gof-under-concurrency` (ch38 PRIMARY): the handling-events domain with five patterns re-expressed — observer from an immutable snapshot, feed read as an async sequence, async decorator, command returning a task, immutable event shared safely.
- #112 `38-observer-immutable-snapshot` (ch38 secondary): a raise reading one immutable snapshot while a concurrent subscribe/unsubscribe swaps in a new snapshot, in-flight notification undisturbed.
- #113 `38-async-iterator-feed` (ch38 density): producers publishing into the feed, a consumer draining with await-foreach, completion ending the sequence cleanly and a cancelled token ending it early.

## Waves
- **Wave A (code, 2 parallel):** A1 ch37 (9/9), A2 ch38 (10/10). Each runs its suite 3x (determinism gate). Token calls TestContext.Current.CancellationToken; no xUnit1051 suppression. Pure BCL; flaky-primitive grep 0; Task.Yield only in library I/O; ZERO suppressions beyond CA1707 (design around CA1819/CA2012/CA1003/CA1051/CA1815/CA1816). Bibliography row NOT a Wave-A task. Path-sanitized summaries.
- **Wave B (diagrams):** six figures #108-113 chapter order; short captions; no generics/cross-language/capstone; 0 em-dash in SVG text.
- **Wave C (prose, 2):** full spine; REPLACE TODO body; FILL When-not-to-use (ONE AI-sidebar each); replace TODO captions; embed secondary+density figures; ch37 ADDS the POSA3 bibliography row + deletes the TODO line, names ObjectPool<T> in prose; ch38 weaves the five patterns, notes `event`'s thread-safe subscribe in prose (not cross-language); voice self-gate 0 ban-tier; >=2000 words.
- **Wave D (validate, Opus):** determinism 3x (ch37 9/9, ch38 10/10) + read key test bodies; no-wall-clock grep 0; POSA3 row present + TODO removed + Wiley link resolves; idiomatic 0 + ZERO suppressions beyond CA1707 (confirm CA1819/CA2012/CA1003/CA1051/CA1815/CA1816 designed-around, not suppressed); spine order + When-not-to-use + Build-run-observe + single For-deeper-coverage; AI-sidebar ==1 each; caption gate; TODO-caption 0; name-drift (ch37 #108-110, ch38 #111-113, yml 112); build.sh+verify.sh 0W/0E; jekyll clean; voice 0 ban-tier; em-dash/capstone/cross-language 0; word floors; path-sanitized. Write verification log. **Confirm this completes all 47 chapters.**

## Acceptance criteria
Per chapter: body >=2000; full spine w/ primary figure after Why; filled When-not-to-use (one AI-sidebar) before single For-deeper-coverage; deterministic 3x (ch37 9/9, ch38 10/10); pure BCL; path-sanitized Build-run-observe. ch37 proves rent-from-pool/distinct-identity/exhaustion-blocks-until-return (!IsCompleted)/returned-to-next-renter/cancelled-rent/lease-returns/manifest-exchange/buffer-returned-cleared/buffer-reused+roundtrip; POSA3 cited, ObjectPool named. ch38 proves the five patterns (observer all+cross-thread-exactly-once/snapshot-intact-on-unsubscribe/feed-in-order+cancel+concurrent/decorator-delegates/command+invoker receipts/immutable-shareable); CA1003 avoided via interface-observer. Iteration: POSA3 row added (only new) + TODO removed; jekyll clean; diagrams.yml 112 #108-113 chapter order; .slnx each; four pins + omit TestingPlatformDotnetTestSupport; pure BCL/no MEDI; InternalsVisibleTo; no cross-language; names unchanged; captions short; net10.0/C#14; AI-sidebar ==1 each; no-wall-clock 0; zero suppressions beyond CA1707. **COMPLETES all 47 chapters (0-46) + Part 11.**

## Risks
- **Determinism / timeout-free exhaustion (HIGH):** rent all N (each awaited) to a provably-empty pool before asserting !pendingC.IsCompleted; the empty channel ReadAsync blocks (no TCS gate needed); cancel the CTS while parked. No WhenAny/Delay.
- **Async-enumerable drain (HIGH, ch38):** feed tests Complete() the writer so await foreach terminates; producer/consumer WhenAll over the known pair.
- **ArrayPool reuse (MED):** use a private ArrayPool.Create() (NOT Shared — TLS caching non-deterministic) + single-threaded sequential rent-after-return + identical sizes.
- **Zero-suppressions (MED):** CA1819 (method-return byte[]/IReadOnlyList, no array property), CA2012 (await ValueTask once, .AsTask() once in tests), CA1003/CA1051 (interface-observer not `event`, Task/properties not public fields), CA1815 (record struct lease), CA1816 (ch35 DisposeAsync precedent). Fix idiomatically, never suppress.
- **ObjectPool scope (LOW):** named in prose only, no package ref.
- **Bibliography (LOW):** POSA3 after POSA2; verify Wiley publisher URL (never reseller); fall back to Wiley domain + ISBN 9780470845257.
- **Standing mechanics:** `{% raw %}` for `{{`/`}}`; .slnx/global.json; CA1707 test-only (not xUnit1051); path leaks; InternalsVisibleTo csproj; #108 order; TODO-caption 0; word floor; AI-sidebar ==1 each; captions no generics; em-dash 0.

## r22 verification log

<!-- Opus fills this in at Wave D. -->
