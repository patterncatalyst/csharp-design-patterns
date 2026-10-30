---
title: "r07 plan — Part 5 finish (ch13-14)"
output: false
render_with_liquid: false
---

# r07 — author ch13-14 (Part 5: Selecting and varying behaviour, finish)

Opus-planned 2026-10-30. Branch `release/r07` (based on main@c1cafd0). Both chapters
hands-on. Mirrors the r05/r06 four-wave shape, BUT Wave B (diagrams) runs AFTER Wave A is
green and is handed the LOCKED type names from the compiled code (r06 lesson: parallel
diagram agents invented class names). Finishes Part 5.

## Scope

- **ch13 State and finite state machines** (`_docs/13-state-and-finite-state-machines.md`,
  Tracking domain). Consignment lifecycle FSM Booked -> Loaded -> InTransit -> Cleared ->
  Delivered, driven by handling events, illegal transitions rejected; PLUS a probabilistic
  transit-delay variant with INJECTED (seeded) randomness. Modern angle: a switch expression
  over `(status, event)` reshapes GoF State. GoF + MS Learn pattern-matching.
- **ch14 Command, undo, Task-returning commands**
  (`_docs/14-command-undo-and-task-returning-commands.md`, Billing/booking domain). Booking
  amendments as reversible, queued commands: `IBookingCommand` Execute/Undo + `DelegateCommand`;
  an `AmendmentQueue` (FIFO run, LIFO undo history); Task-returning commands awaiting an
  injected `IAmendmentGateway`. GoF + MS Learn async.

## Confirmed facts

- Both stubs exist with primary-figure include `caption="Figure 13.1/14.1: TODO caption."`.
  Replace ONLY the `TODO caption.` text (keep the `Figure N.1:` prefix) - Wave C deliverable
  + Wave D grep gate. Both stubs HAVE a `## When not to use it` section; AI sidebar lives there.
- `_data/diagrams.yml` has 22 entries; append from #23 in chapter order.
- Amber accent (`#e8870c`/`#b8650a`/`#fff8ef`/`#111111`); emphasis boxes light fill + dark text.
- Example template = `examples/11-...` + `examples/10-...`. Both r07 libraries carry NO runtime
  NuGet dependency (pure BCL: Task, Random, Queue/Stack) - no MEDI pin. ch14 async mirrors
  ch10's `ShipmentRatingEngine.cs` (ConfigureAwait(false) + TestContext.Current.CancellationToken).

## LOCKED type names (Wave A uses verbatim; Wave B copies from green code)

- **ch13 `examples/13-state-and-finite-state-machines/` (project `ConsignmentTracking`):**
  enum `ConsignmentStatus {Booked,Loaded,InTransit,Cleared,Delivered,Delayed}`; enum
  `HandlingEventType {Load,Depart,ClearCustoms,Deliver}`; record `HandlingEvent`; static
  `Transitions.Next(status, evt)` (switch over the tuple, `_ =>` throws); `InvalidTransitionException`
  (CA1032 three ctors); `ConsignmentStateMachine` (CurrentStatus, Apply, History);
  `TrackingHistory`+`HandlingRecord`; `IRandomSource.NextDouble()` + `SeededRandomSource`;
  `ProbabilisticTransitMachine(IRandomSource, double delayProbability)`.
- **ch14 `examples/14-command-undo-and-task-returning-commands/` (project `BookingAmendments`):**
  `Money` (readonly record struct); `LineItem`; `Booking` (mutable receiver); `IBookingCommand`
  (Execute/Undo); `ChangeServiceLevelCommand`/`ChangeWeightCommand`/`AddLineItemCommand` (each
  snapshots prior value); `DelegateCommand(Action execute, Action undo)`; `AmendmentQueue`
  (Enqueue/Run FIFO/Undo LIFO/Redo); `IAmendmentGateway` (ConfirmAsync/RevertAsync);
  `IAsyncBookingCommand` (ExecuteAsync/UndoAsync); `ConfirmAmendmentCommand` (async);
  `AsyncAmendmentQueue` (RunAsync/UndoAsync, ConfigureAwait(false)).

## Waves

- **Wave A (code, two agents parallel):** build both examples to green. ch13 SYNCHRONOUS
  (no tokens). ch14 async mirrors ch10. Each: library csproj with `<DefaultItemExcludes>...;
  <TestDir>/**` and NO runtime package; nested `*.Tests/` (xunit.v3 4.0.2 / runner 4.0.1 /
  Test.Sdk 18.11.0 / NSubstitute 6.2.0; IsTestProject, IsPackable false, NoWarn CA1707; NO
  TestingPlatformDotnetTestSupport); `.slnx`; README. Capture real path-sanitized output.
  ch13 tests: legal transition advances; illegal throws `InvalidTransitionException`; full
  lifecycle reaches Delivered with History; seeded determinism (same seed -> same sequence);
  scripted draw forces Delayed vs Cleared. ch14 tests: execute+undo restores; queue FIFO
  order; undo walks LIFO; DelegateCommand parity; value-snapshot correctness; async run/undo
  via NSubstitute `IAmendmentGateway` (Received(1) ConfirmAsync/RevertAsync), token threaded.
- **Wave B (diagrams, ONE agent, AFTER Wave A green, owns diagrams.yml):** given the locked
  names copied from green code. #23 `13-state-and-finite-state-machines` (lifecycle FSM);
  #24 `13-probabilistic-transit-model` (stochastic branch, density if body >~2500);
  #25 `14-command-undo-and-task-returning-commands` (IBookingCommand/DelegateCommand + queue);
  #26 `14-async-command-undo` (RunAsync/UndoAsync via gateway, density if body >~2500). Real
  captions, names only from the green code.
- **Wave C (prose, two agents, after code+figures):** full spine; replace TODO caption; one
  AI sidebar in "When not to use it"; ch13 teaches switch-expression-over-(status,event)
  reshaping GoF State, exhaustiveness + `_ =>`, illegal-transition handling, injected seeded
  randomness; ch14 teaches delegates-as-commands + interface form, FIFO/LIFO queue, the
  value-snapshot rule, Task-returning commands. Cite GoF + MS Learn. Zero em-dash, 0 ban-tier
  (avoid "earns its place"/genuinely/for free/honest/worth naming in prose AND headings),
  idiomatic C# 14 only, no numeric cross-refs. Wrap `{{`-containing fenced blocks in raw.
- **Wave D (validate):** idiomatic-review both; voice 0 ban-tier (grep the earns-its-place
  family); em-dash 0 across docs + .cs + README; `grep -c "TODO caption"` == 0; DIAGRAM
  NAME-DRIFT GATE (grep every new SVG + caption for types absent from the example);
  `dotnet test` per example + verify.sh; jekyll build; word counts; log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target, no ceiling; density figure if >~2500); full
spine (figure AFTER "Why this matters"); figure pair(s) on disk + registered with matching
`name` and a REAL caption (no TODO); example `dotnet test` green + in verify.sh; tests assert
behaviour; "Build, run, observe" quotes real path-sanitized output; exactly one AI sidebar in
"When not to use it"; zero em-dash (docs + .cs + README); no rule-of-three; 0 ban-tier;
idiomatic C# 14 only, idiomatic-review clean; no intra-book numeric cross-refs; every type in
every figure/caption exists in the example.
ch13 proves: legal advance; illegal throws; full lifecycle to Delivered with path; seeded
determinism; scripted Delayed vs Cleared. Cites GoF (State) + MS Learn pattern matching.
ch14 proves: execute+undo restores; FIFO queue order; LIFO undo walk; DelegateCommand parity;
value-snapshot; async run/undo order with token. Cites GoF (Command) + MS Learn async.
Iteration: jekyll build clean; diagrams.yml appended from #23 in order; new test csprojs omit
TestingPlatformDotnetTestSupport + pin the four packages; each example has its own `.slnx`;
neither library pins a runtime package; Part 5 complete.

## Risks

ch13: bare `new Random()` non-determinism (inject IRandomSource/seed); CA5394 insecure-random
under WAE (scoped NoWarn + justification: simulation not security); CS8509 non-exhaustive switch
(mandatory `_ =>` arm); CA1032 custom-exception ctors; enum/record name drift (Wave B/D gate).
ch14: async undo semantics (UndoAsync awaited, RevertAsync Received(1)); FIFO/LIFO ordering;
reference-vs-value capture (snapshot value not live ref - value-snapshot test); CA2007
(ConfigureAwait); xUnit1051 (token); CA1031 (no broad catch). Both: diagram name drift (r06
regression - diagrams after green code + Wave D grep gate); TODO-caption trap; "earns its
place" ban idiom; Liquid `{{ }}`; .slnx/global.json; CA1707 test-only; path leaks; word floor.

## r07 verification log

Opus-validated 2026-10-30 (branch `release/r07`). Overall: PASS (one nit-repair round, no must-fixes).

**Build / test (all green):**
- `examples/13-state-and-finite-state-machines` `dotnet test`: 9 total / 9 succeeded / 0 failed.
- `examples/14-command-undo-and-task-returning-commands` `dotnet test`: 12 total / 12 succeeded / 0 failed.
- `scripts/verify.sh`: both new examples auto-discovered and green alongside 00/05/06/07/08/09/10/11/12.
- `bundle exec jekyll build`: clean, all four r07 figures render.

**Content:** both chapters full spine in order (figure after "Why this matters"); "Build, run,
observe" quotes real counts (9 / 12), path-sanitized (0 `/home/`, 0 username); exactly one AI
sidebar each in "When not to use it"; body prose 3302 words (ch13) and 4590 words (ch14), both
above floor and each with a density figure; 0 em-dash; no intra-book numeric cross-refs;
citations learn.microsoft.com + GoF (pearson).

**NO CROSS-LANGUAGE COMPARISON (new rule):** grep for Java/C++/Spring/erasure/"no X analogue"
across both docs, both example trees (.cs + README), all four new diagrams.yml captions, and all
four SVGs = ZERO hits. Rule is now in CLAUDE.md.

**Diagrams (name-accuracy gate after r06 regression):** four figures
(`13-state-and-finite-state-machines`, `13-probabilistic-transit-model`,
`14-command-undo-and-task-returning-commands`, `14-async-command-undo`) each paired on disk,
registered in `_data/diagrams.yml` (entries 23-26, chapter order), included in the right chapter.
Diagrams were generated AFTER the code was green, fed the locked type names; validator extracted
every SVG label + caption token and confirmed each exists in the example. No drift.

**Code idiom:** `lgtm-idiomatic-review` scan 0 hits on both examples. ch13: switch `_ =>` arm,
`InvalidTransitionException` CA1032 ctors, CA5394 handled via scoped pragma (not global), injected
`IRandomSource`. ch14: ConfigureAwait(false) on library awaits, token threaded, CA1711 via scoped
`[SuppressMessage]` (not global). Both test csprojs set IsTestProject + IsPackable false + NoWarn
CA1707, pin the four packages, omit TestingPlatformDotnetTestSupport; each example has its own
`.slnx`; neither library pins a runtime NuGet package. Em-dash across both examples' .cs + README
= 0.

**Voice:** `lgtm-professional-voice` scan 0 ban-tier on both docs.

**Repairs this iteration:** two "earn(s) its own [class/section]" editorializing nits in ch14
reworded (one was also meta-narration about the chapter's own structure) - re-grep now clean.
The user-flagged ch10 kicker ("...not from hope") was also fixed in this branch, and that
editorializing pattern was added to the lgtm-professional-voice scanner (lgtm-skills PR #79).

**Part 5 is complete** (ch11-14 across r06 + r07).
