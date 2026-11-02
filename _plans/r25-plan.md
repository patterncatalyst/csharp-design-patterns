---
title: "r25 plan — Companion deck 101 (Patterns and SOLID)"
output: false
render_with_liquid: false
---

# r25 — Companion deck 101 "Patterns and SOLID"

First of four companion decks (101/201/301/401), built one at a time per the
decks-last convention. Red Hat-branded 16:9 .pptx via pptxgenjs (lgtm-presentation
house style), mined from the finished book. **Accent locked: Red Hat red #EE0000**
(series/cpp parity; user decision 2026-11-01). Diagrams stay amber.

## Scope
Draws ONLY from chapters 0-6 (Parts 0-3) + appendices E/F/G. The GoF catalogue
sections from the cpp template are dropped (owned by 201/301/401). Focus = the
temporal thesis (modern C# 14 dissolves 1994 forces) + deeper SOLID + built-in DI.

## Section arc (~35 slides, all with thick notes)
1. Cover
2. Why Patterns — decision under forces (rating problem; forces local per stage; proven in code)
3. What Modern C# Changed — Observer->event, Strategy->delegate, Null Object->NRTs, divergence map, did-the-force-move test, naming for teams+AI
4. SOLID — SRP/OCP/LSP/ISP/DIP with shipping-domain code + behavioral [Theory] proofs
5. Dependency Injection, built in — MEDI, singleton fan-out (Assert.Same), lifetimes + captive-dependency trap, swap-the-registration
6. Choosing Well — forces test, when not to apply, reviewing AI output
7. Reference — SOLID table, patterns-that-dissolved table, principles beyond SOLID (App F), C#15/.NET11 horizon (App G), reading list, QR slide

## Diagram PNGs (from diagrams/*.svg via cairosvg @1600w)
00-prerequisites, 01-patterns-as-decisions-under-forces, 01-forces-first-vs-pattern-first,
02-what-1994-assumed-and-csharp-invalidated, 03-the-divergence-map, 03-reification-pipeline (reserve),
04-naming-patterns-for-teams-and-ai, 05-srp-ocp-lsp, 06-isp-dip-and-built-in-di, 06-singleton-fanout-registration

## QR URLs (decode-verified against ebpf deck convention)
- site: https://patterncatalyst.github.io/csharp-design-patterns/
- hub:  https://patterncatalyst.github.io/patterncatalyst-workshops-tutorials-list/

## Steps
1. [DONE in setup] scaffold presentation/ (copy deck-helpers.js, assets/, package.json; npm install pptxgenjs)
2. [DONE in setup] render 10 diagram PNGs -> presentation/png/
3. [DONE in setup] generate qr-csharp-site.png + qr-hub.png -> presentation/assets/
4. [Sonnet] write deck-101.js — port cpp scaffolding (S/divider/patternSlide/refTable/cover), author 6 sections + QR, thick C# notes
5. [main] build: node deck-101.js
6. [Opus validate] QA: soffice->pdf->pdftoppm every slide; grep gates; decode QR
7. write build-notes.md

## Acceptance criteria
- node deck-101.js writes the pptx; notesCount == slideCount (~35)
- grep "—" deck-101.js == 0 (no em-dash)
- grep -i "capstone" deck-101.js == 0
- grep -iE "chapter [0-9]|slide [0-9]|as we saw|earlier we" deck-101.js == 0 in slide-visible text
- grep -iE "\bjava\b|c\+\+|smalltalk|unlike" deck-101.js == 0 (no cross-language)
- every content slide via S() => logo bottom-right (pdftoppm confirms)
- shipping domain only (no Foo/Bar)
- QR decode: site + hub URLs correct
- pdftoppm renders all pages; ref-table first columns no wrap; code captions below box; figures centered

## Gates (non-negotiable)
Red Hat fonts; logo every content slide; thick notes every slide; no chapter/slide refs
in visible text; zero em-dash/capstone/cross-language; no editorializing kickers; QA render.
