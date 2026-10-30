---
title: "r10 plan — Part 8 (ch21-22)"
output: false
render_with_liquid: false
---

# r10 — author ch21-22 (Part 8: Coordinating and traversing)

Opus-planned 2026-10-30. Branch `release/r10` (based on main@cbb03f5). Both chapters hands-on;
ch23-24 are r11. Four-wave shape (A code / B diagrams AFTER green code with LOCKED names / C prose
/ D validate). Enforces: NO cross-language comparison (describe `event`/`IObservable<T>`/middleware
positively); keep cross-chapter refs light.

## Scope + example assignments (shipping domain, from Notion map)

- **ch21 Observer: the event keyword** (`_docs/21-observer-the-event-keyword.md`, Tracking domain).
  Handling events raised as cargo moves via the C# `event` keyword (the clearest Tier-1 dissolve),
  then when to reach past a plain event to `IObservable<T>`. ch21 pins `System.Reactive` (Subject<T>
  for the observable half; do NOT hand-roll a Subject). Synchronous.
- **ch22 Mediator + Chain of Responsibility**
  (`_docs/22-mediator-and-chain-of-responsibility.md`, Customs domain). Customs clearance as an
  ordered chain (document check -> duty -> inspection -> release) where any stage halts downstream;
  a Mediator coordinating colleagues that never reference each other. Kestrel middleware mentioned
  ONCE as illustration (no code, no hosting). Pure BCL.

## Confirmed facts

- Part slug (from stubs); edit body in place only; primary include has `caption="Figure NN.1: TODO
  caption."` (replace text, keep prefix); each stub has `## When not to use it` (AI sidebar there).
- `_data/diagrams.yml` has 38 entries; append from #39.
- Template `examples/11-...`. Keep both examples synchronous (Rx Subject.OnNext is synchronous).

## LOCKED type names (Wave A verbatim; Wave B copies from green code)

- **ch21 `examples/21-observer-the-event-keyword/` (ConsignmentTracking, System.Reactive pinned):**
  enum `ConsignmentStatus {Received,Loaded,InTransit,CustomsHold,Cleared,Delivered}`; `HandlingEvent`
  record; `HandlingReportedEventArgs : EventArgs` (Handling); `ConsignmentTracker` (`event
  EventHandler<HandlingReportedEventArgs>? HandlingReported`, `Report(HandlingEvent)` null-conditional
  raise, `History`); subscribers `CustomerNotifier`/`AuditLog` (OnHandlingReported handlers);
  `HandlingEventStream` (wraps `Subject<HandlingEvent>`, `IObservable<HandlingEvent> Events`,
  `Publish`/`Complete`, IDisposable); `RecordingObserver : IObserver<HandlingEvent>`
  (Received/Completed/Error).
- **ch22 `examples/22-mediator-and-chain-of-responsibility/` (CustomsClearance, pure BCL):**
  `CustomsDeclaration` record; mutable `ClearanceContext` (Declaration, StagesRun, Duty);
  `ClearanceResult` record (Cleared, HaltedStage, Reason, Duty); abstract `ClearanceStage`
  (SetNext fluent, Handle records+halts-or-delegates, protected abstract Process); `DocumentCheckStage`/
  `DutyCalculationStage`/`InspectionStage`/`ReleaseStage`; `CustomsClearancePipeline.Standard()`;
  `IClearanceMediator.Send`; abstract `ClearanceColleague` (Mediator, Inbox, Receive, Send);
  `DeclarationDesk`/`DutyDesk`; `CustomsMediator : IClearanceMediator` (routes to the OTHER colleague,
  no echo).

## Waves

- **Wave A (code, 2 agents parallel):** build each green; library csproj with
  `<DefaultItemExcludes>...;<TestDir>/**`; nested `*.Tests/` (4 pins, IsTestProject, IsPackable false,
  NoWarn CA1707, NO TestingPlatformDotnetTestSupport); `.slnx`; README. ch21 pins System.Reactive
  (survey version); ch22 no runtime package. Capture real path-sanitized output. Tests:
  - ch21: both subscribers receive events in order; `-=` unsubscribe stops delivery; Report records
    history; no-subscriber raise does not throw; RecordingObserver gets OnNext in order + OnCompleted;
    late subscriber misses an earlier publish (hot stream).
  - ch22: chain runs all 4 stages in order + releases a complete declaration; missing-docs / failed-
    inspection halts downstream (StagesRun excludes later stages, HaltedStage set); duty calculated;
    mediator routes between colleagues + reply back, no echo to sender.
- **Wave B (diagrams, 1 agent, AFTER green code, owns diagrams.yml):** locked names. #39
  `21-observer-the-event-keyword` (primary) + `21-event-and-iobservable` (secondary) + optional ch21
  density if >~2500; `22-mediator-and-chain-of-responsibility` (primary) + `22-mediator-colleagues`
  (secondary) + optional ch22 density. Real captions; amber.
- **Wave C (prose, 2 agents, after code+figures):** full spine; replace TODO caption; one AI sidebar;
  cite GoF + MS Learn. ch21 event + IObservable positive (EventArgs subclass, null-conditional raise,
  -= unsubscribe + leak note, when to reach for IObservable). ch22 chain (ordered/halting) + mediator
  (decoupled), Kestrel middleware mentioned ONCE. Zero em-dash; 0 ban-tier; idiomatic C#14 only; NO
  cross-language; cross-chapter refs light. Wrap `{{`/`}}` fenced blocks in raw.
- **Wave D (validate):** idiomatic-review (0); voice 0 ban-tier (grep editorializing families);
  em-dash 0 across docs + .cs + README; `grep -c "TODO caption"` == 0; diagram name-drift gate;
  NO-CROSS-LANGUAGE gate; CROSS-CHAPTER gate; dotnet test per example + verify.sh; jekyll build; word
  counts; log below.

## Acceptance criteria

Per chapter: body >= 2000 words (2500 target; density figure if >~2500); full spine (figure AFTER
"Why this matters"); figure pair(s) on disk + registered, matching `name`, REAL caption (no TODO);
example `dotnet test` green + in verify.sh; tests assert behaviour; "Build, run, observe" quotes real
path-sanitized output; exactly one AI sidebar in "When not to use it"; zero em-dash (docs + .cs +
README); no rule-of-three; 0 ban-tier; idiomatic C#14 only; NO cross-language; cross-chapter refs
light; no numeric cross-refs; every figure/caption type exists in the example; GoF + MS Learn.
ch21 proves: ordered delivery to both subscribers; `-=` stops delivery; history recorded; no-subscriber
raise safe; RecordingObserver OnNext order + OnCompleted; late-subscriber-misses hot stream.
ch22 proves: 4 stages in order + release; halt stops downstream + reports stage; duty calculated;
mediator routes + reply, no self-echo.
Iteration: jekyll clean; diagrams.yml from #39 in order; test csprojs omit
TestingPlatformDotnetTestSupport + pin 4 packages; each example own `.slnx`; ch21 System.Reactive
pinned, ch22 no runtime package; no cross-language token anywhere; ch21-22 shipped (ch23-24 for r11).

## Risks

ch21: CA1003 (generic EventHandler<T>); CA1030 (raise method named Report, not Raise*); EventArgs
subclass; null-conditional raise (no-subscriber-safe test); `-=` same-delegate unsubscribe + leak
note; Rx Subject<T> not hand-rolled (idiomatic-review + OnNext/OnCompleted tests); System.Reactive
version drift (survey). ch22: stage ordering/halt (non-cleared result returns before next; StagesRun
test); CA1031 (halt via result, no broad catch); mediator decoupling (colleagues hold
IClearanceMediator only, wired after construction; no-echo test); middleware mention illustration-only.
All: diagram name drift (diagrams after code + gate); TODO-caption trap; no-cross-language slip;
cross-chapter refs; Liquid `{{ }}`; .slnx/global.json; CA1707 test-only; CA1032 3 ctors; path leaks;
word floor.

## r10 verification log

_(filled in Wave D)_
