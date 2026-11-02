---
title: "r26 plan — Companion deck 201 (Idiomatic C#)"
output: false
render_with_liquid: false
---

# r26 — Companion deck 201 "Idiomatic C#"

Second of four decks (101 shipped r25). Red Hat red #EE0000 (locked). pptxgenjs,
`presentation/deck-201.js` -> `csharp-design-patterns-201-idiomatic-csharp.pptx`.
~34 slides. Mines idiom chapters ONLY; excludes 301 (ch10/Part13/ch46) and 401
(Part 11 ch33-38) material.

## Section arc (~34 slides)
- Cover
- S0 "Value, immutability, and lifetime" — ch8 value types/Span, ch9 records/init/required/with, ch7 Dispose/IAsyncDisposable
- S1 "Sequences and async, the idiomatic shapes" — ch23 yield/LINQ/IAsyncEnumerable, ch32 TAP conventions (convention shape only, NOT concurrency)
- S2 "Typed outcomes and typed configuration" — ch31 Try-Parse/Tester-Doer/Result, ch30 Options pattern
- S3 "C# as data: extend, inspect, generate, enforce" (Part 9 core) — ch25 extension members, ch26 expression trees, ch27 source generators, ch28 analyzers, ch29 static abstract/generic math
- Closing (toolkit two-col) + refTable (idiom -> what C#14/.NET10 enables)
- QR slide (mirror 101 block exactly)

## Diagram PNGs to render (all verified to exist in diagrams/)
08-value-types-struct-span, 09-nrts-records-init-required-with, 23-deferred-execution,
32-tap-and-async-conventions, 31-try-parse-vs-result, 25-extension-members,
26-expression-trees, 27-incremental-generator-pipeline, 27-generated-registry-vs-reflection,
28-roslyn-analyzers, 29-irateable-static-dispatch, 29-generic-math-over-money

## Collision watch (critical, from planner)
- ICarrierGateway defined 3 ways (ch27 Code/DisplayName/Endpoint; ch30 options-consuming; ch32 DispatchAsync/DispatchBatchAsync/IsReachableAsync) + deck-101's (Quote/Book/Track). Each slide's ICarrierGateway must match ONLY its own chapter.
- Money: ch8 (Amount+Currency, Money.Zero) vs ch29 (single decimal Amount + numerics interfaces). Value-types slide = ch8 Money; generic-math slide = ch29 Money. Never mix.
- Roslyn/generic-math identifiers must match the book verbatim (IIncrementalGenerator, EquatableArray<T>, RegisterSourceOutput, MoneyAsDecimalAnalyzer, SHIP0001, [WireContract], RS2008, IRateable<TSelf>, RateBook.Quote<T>, Totals.Sum<T>, System.Numerics interfaces).

## Gates (same as 101)
Red Hat fonts+logo every content slide; notes 1:1 (trace wrapper defs, not grep -c);
shipping domain; NO chapter/slide refs in visible text; ZERO em-dash/capstone/cross-language
(temporal framing only; idiom slides are the cross-language-temptation hotspot); punchy
1-2 line bullets (101 overflow lesson); QA soffice->pdftoppm every slide.

## Steps
1. [DONE setup] render 12 PNGs -> png/
2. [Sonnet] write deck-201.js (copy 101 prologue/wrappers/cover/QR verbatim; author 4 sections + closing + QR; own addNotes per inline S() block)
3. [main] build: node deck-201.js
4. [Opus validate] QA render every slide + grep gates + collision/name-drift spot-check + voice scan
5. update build-notes.md with the 201 row
