---
title: "Voice + structure rollout plan: chapters 01-11"
output: false
---

# Voice + structure rollout, chapters 01-11

Apply the chapter-12 readability+structure treatment (encoded in
`lgtm-professional-voice`) to chapters 01-11. ch12 is the merged exemplar.
Documentation voice/pedagogy pass only: no code, commands, {% raw %}`{% include %}`{% endraw %},
captions, recorded output, or `<!-- EXAMPLE -->` comments change.

## Baseline (measured)
- 01-11: 0 ban-tier, 0 em-dash. Hold both.
- Median sentence length: 01=23 02=27 03=31 04=31 05=33 06=37 07=51 08=44 09=42 10=41 11=41.
- No chapter has an objective-payoff closer; all need `## Where this leaves you` before `## For deeper coverage`.
- Sidebar: 0 on 01-04, 1 on 05-11 (all prose shape, no sample prompt). Reshape 05-11.
- Build/run/observe: 05-11 only (dumped paragraph). Reshape 05-11.
- "When not to use it": 06,07,08,09,11 have it; 05 and 10 do NOT — do not add.
- 07-11 flagged `stiff: few contractions`.

## Treatment (apply only where the section exists)
1. Break run-ons toward ~20-word median, short/long mix.
2. Intent before mechanism (what it is / forces / value, then mechanics).
3. Explain a subtle mechanism by example first, then the rule.
4. Code callouts (#A/#B) instead of post-listing re-narration.
5. Build/run/observe: lead with what the run proves + command + verbatim output + per-claim bullets.
6. AI sidebar: problem + nested `>` ready-to-run prompt + one review line (CRC ch05 model).
7. `## Where this leaves you` closer: one item per learning objective, capability + artifact.
8. Contract negations on 07-11. Relax the word floor (cut re-narration, never pad).

## Batches (one branch -> PR -> squash-merge each)
- Batch 1: 01-04 (conceptual; closers + run-ons; NO sidebar/BRO).  <-- IN PROGRESS
- Batch 2: 05-06 (sidebar + BRO + callouts + closer).
- Batch 3: 07-08 (heaviest medians; contractions; ref-struct list; explain-by-example).
- Batch 4: 09-11 (3x sidebar/BRO/closer; run per-chapter if context tight).

Per-chapter work items and the full acceptance-criteria list live in the relay
plan (Opus, 2026-11-04). Key per-chapter notes:
- 02: soften "this book's Strategy chapter covers..." to in-place, no chapter number.
- 03: keep the feature table + "Developed in" column + forward-pointer function; only shorten prose.
- 04: conceptual AI discussion but NO ai-sidebar added (keep count 0); lifecycle table untouched.
- 06: BRO opener drop "the prerequisites chapter installs" cross-ref.
- 11: leave the `codetabs` include + both fenced blocks untouched.

## Acceptance criteria (per chapter)
0 ban-tier; median at/under per-chapter target; 07-11 not `stiff`; closer present
mapping each objective; sidebar count ==1 (05-11) / ==0 (01-04) with nested `>`
prompt; BRO leads-with-what-it-proves + per-claim list; git diff shows no change
inside any fence/inline-code/include/caption/recorded-output/EXAMPLE comment; no
new cross-chapter-number or cross-language refs; no em-dash/`--`; jekyll build
passes per batch; no padding to the floor.

## Status
- [x] Batch 1 (01-04) — merged (PR #39)
- [x] Batch 2 (05-06) — merged
- [x] Batch 3 (07-08) — merged
- [x] Batch 4 (09-11) — merged (ROLLOUT COMPLETE: ch01-12 all treated)

## Note on callouts (learned in Batch 2)
Use `// #A` in-code markers only where they anchor to lines PRESENT in the shown
block (ch12/ch06 style). Where a block shows interfaces/definitions and the
explanation is about types not shown, use tight prose, not `#A` labels (ch05).
