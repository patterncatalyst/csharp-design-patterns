---
title: "r28 plan — Companion deck 401 (Concurrency Patterns)"
output: false
render_with_liquid: false
---

# r28 — Companion deck 401 "Concurrency Patterns" (FINAL deck)

Fourth of four decks (101 r25, 201 r26, 301 r27 shipped). Red Hat red #EE0000.
pptxgenjs, `presentation/deck-401.js` -> `csharp-design-patterns-401-concurrency-patterns.pptx`.
~35 slides. Mines Part 11 ch33-38. No C++ sibling template — arc designed fresh.

## Section arc (~35 slides, 6 sections)
- Cover + Agenda
- S01 Suspension without a thread (ch33) — await=heap state machine not a parked thread; 2022 green-threads prototype (.NET-vs-own, dotnet/runtimelab, dated); 1000 bookings one thread; ValueTask skips the machine
- S02 Build it once, share it everywhere (ch34) — Lazy<T> ExecutionAndPublication, System.Threading.Lock + ??=, static init, exception-caching trap
- S03 Coordinating one shared servant (ch35) — 4 stages: Monitor/Lock -> CS1996 await-in-lock wall -> SemaphoreSlim -> Active Object on bounded Channel<T>+TCS; POSA2 six participants (Scheduler->await foreach, Future->Task)
- S04 Handing work across a queue (ch36) — bounded Channel<T> producer-consumer, exactly-once, backpressure, Writer.Complete()+WhenAll drain
- S05 Rationing a scarce resource (ch37) — connection pool over Channel<T> (rent=ReadAsync/return=TryWrite), GatewayLease using, ArrayPool<byte> clearArray:true
- S06 Classic GoF under concurrency (ch38) — Observer via ImmutableArray+ImmutableInterlocked (not event, avoids CA1003), Iterator as IAsyncEnumerable (concurrency framing only), Decorator over async, Command as Task, immutability-as-strategy
- Close (two-col) + refTable "the reach-for ladder" + QR

## Diagram PNGs (all 18 ch33-38 VERIFIED to exist; render all for swap flexibility)
33-why-dotnet-chose-stackless-async, 33-stackless-vs-stackful, 33-suspended-without-a-thread,
34-thread-safe-creation, 34-lazy-vs-lock-vs-static, 34-exception-caching,
35-monitor-object-and-active-object, 35-active-object-participants, 35-channel-backpressure,
36-producer-consumer-and-pipelines, 36-exactly-once-drain, 36-pipeline-stages,
37-pools-object-connection-arraypool (DENSE-FLAG, avoid as primary), 37-connection-pool-exhaustion, 37-arraypool-buffer-reuse,
38-classic-gof-under-concurrency, 38-observer-immutable-snapshot, 38-async-iterator-feed

## Collision/scope watch
- Channel<T> appears 4x with different bounds/roles (ch35 bounded Active-Object, ch36 bounded pipeline, ch37 unbounded pool wait-queue, ch38 unbounded feed) — each slide names its exact CreateBounded/Unbounded + role.
- ch35 StowagePlan has 4 variants (StowagePlan servant / Synchronized / Semaphore / ActiveObject) — keep servant distinct from wrappers.
- 201-boundary: 401 = mechanics (state-machine heap, ValueTask alloc, channel coordination); do NOT re-teach [EnumeratorCancellation]/deferred-execution (201's surface).
- ch33 green-threads = .NET-vs-own-2022-prototype only, never cross-language.

## Gates (same as prior decks + new editorializing patterns now in scanner)
Red Hat fonts+logo every content slide; notes 1:1 (pptx unzip: ppt/slides==ppt/notesSlides);
shipping domain; NO chapter/slide refs in visible text; ZERO em-dash/capstone/cross-language;
punchy bullets; long code via per-call addCodeSlide fontSize/y/h BUT drop y/h override when title
is 2 lines (r27 lesson: box-top collides with 2nd title line); run lgtm-professional-voice to 0
ban-tier incl the new X-not-Y/standing-in-the-way/in-one-pass patterns; QA soffice->pdftoppm every slide.

## Steps
1. [DONE setup] render 18 PNGs -> png/
2. [Sonnet] write deck-401.js (clone 301 prologue/wrappers/cover/QR; author 35 slides; own addNotes per inline S())
3. [main] build: node deck-401.js
4. [Opus validate] QA render every slide + grep gates + collision spot-check + voice scan + unzip notes-count
5. update build-notes.md with the 401 row (and drop the "401 follows" parenthetical)
