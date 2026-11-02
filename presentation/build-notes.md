# Companion decks — build notes

Red Hat-branded 16:9 decks, built with pptxgenjs (lgtm-presentation house style).
Accent is Red Hat red #EE0000 (series parity); diagrams are amber.

| Deck source | Output |
|-------------|--------|
| `deck-101.js` | `csharp-design-patterns-101-patterns-and-solid.pptx` |

(201 idiomatic C#, 301 testing/legacy/AI, and 401 concurrency decks follow, one at a time.)

## Rebuild
```sh
cd presentation
export NODE_PATH=$(npm root -g)
npm install pptxgenjs          # first time only (node_modules is gitignored)
# regenerate figure PNGs from the site diagrams:
for n in 00-prerequisites 01-patterns-as-decisions-under-forces 01-forces-first-vs-pattern-first \
         02-what-1994-assumed-and-csharp-invalidated 03-the-divergence-map 03-reification-pipeline \
         04-naming-patterns-for-teams-and-ai 05-srp-ocp-lsp 06-isp-dip-and-built-in-di 06-singleton-fanout-registration; do
  python3 -c "import cairosvg; cairosvg.svg2png(url='../diagrams/$n.svg', write_to='png/$n.png', output_width=1600)"
done
node deck-101.js
```

## QA
```sh
mkdir -p qa
soffice --headless --convert-to pdf --outdir qa csharp-design-patterns-101-patterns-and-solid.pptx
pdftoppm -jpeg -r 110 qa/csharp-design-patterns-101-patterns-and-solid.pdf qa/pg
# eyeball qa/pg-*.jpg: logo bottom-right every content slide, no bullet/caption overlap,
# code captions one line below the dark box, diagrams centered, QR slide scannable.
```

Figures in `png/` are rendered from `../diagrams/*.svg`. The two QR codes in `assets/`
are generated with the `qrcode` Python package (site = the book Pages URL, hub = the
workshops-and-tutorials list). Speaker notes match slides 1:1.

Conventions: Red Hat fonts, logo on every content slide, shipping-domain examples,
ideas conveyed directly (no chapter or slide references on slides), zero em-dash, zero
cross-language comparison (temporal framing only: modern C# 14 against the 1994 GoF-era
assumptions).
