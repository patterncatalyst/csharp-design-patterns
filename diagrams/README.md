# Diagrams

Paired diagram assets for the book. Each figure is two files that share a basename:

- `diagrams/<name>.svg` — the rendered diagram, embedded inline by the
  `excalidraw.html` include and shown in the gallery at `/diagrams/`.
- `diagrams/<name>.excalidraw` — the editable source. Open it on
  [excalidraw.com](https://excalidraw.com) to edit, then re-export the SVG.

## Conventions

- House style: Red Hat fonts, the single `.NET` purple accent, C4-trending.
- Emphasis and ink boxes are light fill with dark text, never dark boxes.
- One figure per chapter minimum. A chapter embeds its figure with:

  ```liquid
  {% include excalidraw.html name="NN-slug" caption="Figure N.1: ..." %}
  ```

- Register each figure in `_data/diagrams.yml` so it appears in the gallery.

Until a figure's two files exist, the include and the gallery render a
placeholder, so the page layout never breaks. This file is excluded from the
built site.
