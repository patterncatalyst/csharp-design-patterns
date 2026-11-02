---
title: "r27 plan — Companion deck 301 (Testing, Legacy, and AI)"
output: false
render_with_liquid: false
---

# r27 — Companion deck 301 "Testing, Legacy, and AI"

Third of four decks (101 r25, 201 r26 shipped). Red Hat red #EE0000 (locked).
pptxgenjs, `presentation/deck-301.js` -> `csharp-design-patterns-301-testing-legacy-and-ai.pptx`.
~33 slides. Mines ch10 (testing), Part 13 ch41-44 (legacy/Feathers+Fowler), ch46 (AI).
Excludes 401 concurrency, 101/201 content, and any capstone/composition section.

## Section arc (~33 slides)
- Cover + Agenda
- S01 Proving it works: xUnit + NSubstitute (ch10) — four-phase, [Theory]/TheoryData, stub vs verify, Received/Arg, async+cancellation, NSubstitute-vs-Moq note
- S02 Changing inherited code (Part 13) — ch41 non-virtual-by-default + 4 seams (legacy before / refactored after), ch42 dependency-breaking catalogue (NSubstitute exposes the hidden audit call), ch43 Feathers-then-Fowler (one engine 3 forms, equivalence proof), ch44 refactor-or-rewrite criteria (IRateEngine behind both)
- S03 Working with AI (ch46) — throughline, single-implementation-Strategy smell, SHIP0002 analyzer (before/after), analyzer-as-the-gate (human owns it)
- Close (two-col) + refTable (test-double taxonomy, appendix D) + QR slide

## Diagram PNGs to render (all verified to exist)
Primary: 10-testing-with-xunit-and-nsubstitute (FLAG dense), 10-nsubstitute-stub-vs-verify,
41-seams-non-virtual-by-default, 41-c-sharp-seam-kinds, 42-feathers-catalogue-in-c-sharp,
43-feathers-then-fowler, 43-behaviour-preserved, 44-decision-criteria, 44-same-contract-two-paths,
46-working-with-ai-and-llm-tools, 46-single-implementation-strategy, 46-analyzer-as-the-gate.
Alternates rendered for swap-if-illegible: 42-fake-drives-the-engine, 43-fowler-vs-feathers,
44-refactor-or-rewrite, 42-dependency-breaking-techniques.

## Collision watch (Part-13 engine zoo — same class as 201's ICarrierGateway)
- ch41 vocab: ITariffSource, IClock, PeakSurcharge, PeakWindow (nested .Legacy RateEngine)
- ch42 vocab: ITariffBook, IAuditLog, IReferenceGenerator, LaneValidation, audit.Received(1)
- ch43 vocab: LegacyRateEngine -> SeamedRateEngine -> RateEngine, IServiceSurcharge, ServiceSurcharges.For, RateRequest, RateRules, EquivalenceTests
- ch44 vocab: IRateEngine, RefactoredRateEngine, RewrittenRateEngine, QuoteService
Each slide uses ONLY its own chapter's vocab; never blend. ch42 ITariffBook != ch41/43/44 ITariffSource.

## Gates (same as 101/201)
Red Hat fonts+logo every content slide; notes 1:1 (verify at pptx level: unzip, count
ppt/slides vs ppt/notesSlides); shipping domain; NO chapter/slide refs in visible text;
ZERO em-dash/capstone/cross-language (Part-13 before/after = temporal older-inherited-C#
vs refactored-modern-C#, never Java/C++); punchy bullets; long code via per-call
addCodeSlide fontSize/y/h opts; QA soffice->pdftoppm every slide, watch dense diagrams.

## Steps
1. [DONE setup] render PNGs -> png/
2. [Sonnet] write deck-301.js (copy 201 prologue/wrappers/cover/QR; author 33 slides; own addNotes per inline S() block)
3. [main] build: node deck-301.js
4. [Opus validate] QA render every slide + grep gates + collision/name-drift spot-check + voice scan (run lgtm-professional-voice scan.sh) + unzip notes-count
5. update build-notes.md with the 301 row
