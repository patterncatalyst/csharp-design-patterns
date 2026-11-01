---
title: "r15 plan — quality/comprehensibility cleanup pass (2 tracks, net-no-code)"
output: false
render_with_liquid: false
---

# r15 — cleanup pass: AI-sidebar callout rollout + caption readability sweep

Opus-planned 2026-11-01. Branch `release/r15` (off latest `main`). NO new chapters, NO code,
NO SVG/`.excalidraw`/figure-`name:` changes. A user-requested polish of the existing book
(ch0-32) along two independent tracks, both prose/markdown only. `dotnet`/examples are untouched,
so `scripts/verify.sh` stays green trivially; the real gates are a clean `bundle exec jekyll build`,
the sidebar/caption greps, and a voice re-scan of edited files.

## Confirmed facts (verified this plan)
- Site is **kramdown with `input: GFM`**. IAL (`{: .ai-sidebar}`) breaks blockquote parsing
  (renders literal `&gt;`) — DO NOT use it. The **proven** mechanism is an explicit HTML wrapper
  with `markdown="1"` (ch21 is the committed reference, renders `<blockquote class="ai-sidebar"><p>Asked to make...`).
- CSS is already shipped and working: `assets/css/site.css` has `.tutorial__main blockquote.ai-sidebar`
  plus a `::before` header strip `content: "🤖 AI sidebar"`. DO NOT touch CSS.
- ch21 sidebar is the only one converted. 27 chapters still carry the old `> **AI sidebar.**`
  blockquote: **ch05-20 and ch22-32**. ch00-04 and ch21 need no TRACK 1 work.
- `_data/diagrams.yml` has 71 entries. The last 9 (names starting `30-`/`31-`/`32-`, lines ~809-880)
  are already short — SKIP. The 62 entries before them (ch00-29, which in file order includes the
  out-of-order `29-`, `27-`, `28-` blocks) are the TRACK 2 scope. Boundary is by **name prefix**, not
  line order.
- ch30-32 chapter-include captions are already short (SKIP TRACK 2 for them) but ch30-32 sidebars
  still need TRACK 1 conversion. ch21 is the inverse: sidebar done, but its two figure captions
  (Figure 21.1/21.2, the 70-word block the user flagged) still need TRACK 2.

## TRACK 1 — AI-sidebar conversion recipe (per chapter, exactly once)
Locate the single `> **AI sidebar.** ...` blockquote. Replace it with:

```
<blockquote class="ai-sidebar" markdown="1">
<first line of the quoted prose, lead-in removed>
...rest of the prose, flowing...
</blockquote>
```

Mechanical steps:
1. Open the wrapper line `<blockquote class="ai-sidebar" markdown="1">` immediately before the content,
   and close with `</blockquote>` immediately after. Both on their own lines.
2. Strip the leading `> ` (and bare `>`) from every quoted line; keep the inner text as plain flowing
   markdown. Backticked code (`event EventHandler<T>?`, `Subject<T>`) stays as-is; kramdown parses the
   inner body because of `markdown="1"`.
3. **Remove the inline `**AI sidebar.** ` lead-in** — the header strip now carries that label. The
   first body sentence starts the prose directly (ch21: "Asked to make a status change...").
4. Leave a blank line before the opening wrapper and after the closing wrapper (normal block spacing).
5. If a sidebar is multi-paragraph (was separated by bare `>` lines), keep a genuine blank line
   between paragraphs INSIDE the wrapper so kramdown emits separate `<p>` elements. If any sidebar
   contains a fenced code block, keep blank lines around the fence inside the wrapper and verify it
   renders (flag in validation). Most sidebars in this book are a single paragraph.

Reference: `_docs/21-observer-the-event-keyword.md` (already converted + verified).
Unconverted example to transform: `_docs/18-adapter-facade-bridge.md`.

## TRACK 2 — caption readability recipe (both surfaces, per figure)
Rewrite each ch00-29 caption from the dense identifier-inventory style to a SHORT human-readable gloss:
one to two sentences, ~20-45 words, idea-not-inventory, keep the `Figure NN.x: ` prefix, name at most
one or two key types for orientation, and keep **angle-bracket generics out of the caption text** (they
live in the figure labels). Both surfaces must change and both must be accurate + name-drift-clean:
- (a) chapter include: the `caption="Figure NN.x: <LONG>"` argument in the `{% include excalidraw.html %}`
  line in `_docs/NN-*.md`.
- (b) `_data/diagrams.yml`: the `caption:` field for the same `name:`.
They need not be byte-identical but both must be short, accurate, and free of name drift. DO NOT change
`name:`, `title:`, `section:`, SVG, or `.excalidraw` files — captions only.

Long->short contrast for calibration: `_data/diagrams.yml` ch18/19 entries (6-9 dense lines each) vs the
ch30-32 entries (2-3 lines, ~30-40 words) which are the target style.

## Fan-out (avoid file clobber)
`_data/diagrams.yml` is a SINGLE file -> exactly ONE agent edits it. `_docs/*.md` files are partitioned
into disjoint ranges; each range-agent does BOTH its TRACK 1 sidebar conversions AND its TRACK 2
chapter-include caption rewrites for the chapters it owns. No two agents touch the same `_docs` file.

### Wave 1 — 6 agents in parallel
- **Agent D (diagrams.yml):** rewrite the `caption:` field for ALL ch00-29 entries (62 entries, every
  entry whose `name:` prefix is `00-` through `29-`, i.e. everything except the last 9 `30-`/`31-`/`32-`).
  Owns ONLY `_data/diagrams.yml`.
- **Agent R1 — `_docs` ch00-07** (00,01,02,03,04,05,06,07): TRACK 2 captions on all; TRACK 1 sidebar on
  05,06,07 only (00-04 have no sidebar).
- **Agent R2 — `_docs` ch08-14** (08,09,10,11,12,13,14): TRACK 2 captions + TRACK 1 sidebar on all.
- **Agent R3 — `_docs` ch15-21** (15,16,17,18,19,20,21): TRACK 2 captions on all (incl. ch21 Fig 21.1/21.2);
  TRACK 1 sidebar on 15-20 only (ch21 sidebar already done — leave it).
- **Agent R4 — `_docs` ch22-27** (22,23,24,25,26,27): TRACK 2 captions + TRACK 1 sidebar on all.
- **Agent R5 — `_docs` ch28-32** (28,29,30,31,32): TRACK 1 sidebar on all five; TRACK 2 captions on 28,29
  only (ch30-32 captions already short — leave them).

Each range-agent self-gates voice (0 em-dash, 0 ban-tier, no "capstone", no cross-language) on the lines
it edits before handing back. Each agent also updates nothing outside its owned files.

### Wave 2 — CLAUDE.md spine bullet (1 small edit, owns only CLAUDE.md)
Update the chapter-spine bullet that says "exactly one **AI sidebar** box" to describe the new
convention: each pattern chapter has exactly one `<blockquote class="ai-sidebar" markdown="1">` callout
(the CSS header strip renders the "AI sidebar" label; no inline `**AI sidebar.**` lead-in). This is the
new identity gate — the old `grep "AI sidebar"` text no longer finds the inline label.

### Wave 3 — validate (single validator)
Run the Wave D gates below. If any sidebar fails to render or any caption is still long/has generics,
reopen the owning agent's files.

## Acceptance criteria (checkable)
1. Every TRACK 1 chapter (ch05-20, ch22-32) has **exactly one** `<blockquote class="ai-sidebar" markdown="1">`
   and **zero** inline `**AI sidebar.**` lead-ins. ch21 unchanged (already one). ch00-04 have none.
2. Every ch00-29 figure caption, BOTH surfaces, is <= ~45 words, one-to-two sentences, keeps the
   `Figure NN.x: ` prefix, and contains NO `<...>` angle-bracket generics in the caption text.
3. ch30-32 captions and ch21's two captions: ch30-32 unchanged; ch21's are now SHORT.
4. `bundle exec jekyll build` is clean; a spot-check of rendered HTML shows `<blockquote class="ai-sidebar">`
   wrappers producing `<p>` content (not literal `&gt;`).
5. 0 em-dash, 0 ban-tier, no "capstone", no cross-language comparison across all edited files.
6. Name-drift clean: every identifier left in a rewritten caption exists in that chapter's example source.
7. No `_data/diagrams.yml` `name:` changes, no SVG/`.excalidraw` edits, no `examples/`/code edits, no new
   chapters.
8. CLAUDE.md spine bullet updated to the new `<blockquote class="ai-sidebar" markdown="1">` convention.

## Wave D gates (commands)
- `grep -c '<blockquote class="ai-sidebar" markdown="1">' _docs/NN-*.md` -> 1 for each ch05-20,21,22-32;
  0 for ch00-04.
- `grep -rln '\*\*AI sidebar\.\*\*' _docs/*.md` -> empty (no inline lead-ins remain anywhere).
- Eyeball every ch00-29 `caption="Figure` line for length (<= ~45 words) and no `<...>`;
  `grep -n 'caption:' _data/diagrams.yml` for the 62 in-scope entries same.
- Angle-bracket check in captions: `grep -nE 'caption="Figure[^"]*<[A-Za-z]' _docs/*.md` and the diagrams.yml
  equivalent -> no hits in ch00-29.
- `git diff --name-only` shows only `_docs/*.md`, `_data/diagrams.yml`, `CLAUDE.md`, `_plans/r15-plan.md`;
  NO files under `diagrams/`, `examples/`, `assets/` (except the already-committed CSS pilot).
- `bundle exec jekyll build` exits 0.
- `lgtm-professional-voice` scan over edited `_docs` -> 0 ban-tier/em-dash/capstone/cross-language.
- Visual spot check: build the site and confirm one converted sidebar (e.g. ch18) renders the header
  strip + flowing paragraph, and ch18 Figure 18.1 caption renders short.

## Risks
- **kramdown `markdown="1"` edge cases:** a multi-paragraph sidebar needs a real blank line between
  paragraphs inside the wrapper, and a sidebar with a fenced code block needs blank lines around the fence;
  otherwise content collapses or escapes. Per-chapter check; most are single-paragraph.
- **Caption name-drift:** shorter captions are easier, but a kept identifier must still exist in the
  chapter's example source — verify against `examples/NN-*`.
- **Accidental figure-`name:` or SVG change:** forbidden; caught by the `git diff --name-only` gate.
- **Voice regression** when rewriting captions/trimming lead-ins — re-run the voice scanner on edited files.
- **Stale gate:** the old `grep -c "AI sidebar"` text check is retired; identity is now the
  `blockquote.ai-sidebar` class. Reviewers must use the class grep, not the text grep.
- **File clobber:** enforced by disjoint `_docs` ranges + single diagrams.yml owner + single CLAUDE.md
  owner; agents must not stray outside owned files.

## r15 verification log

<!-- Opus fills this in during Wave 3. -->
