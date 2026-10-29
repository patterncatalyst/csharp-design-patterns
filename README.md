# Design Patterns with .NET / C#

The Gang of Four patterns and the SOLID principles, the patterns the .NET
platform defines in its own guidelines, and the concurrency patterns, taught as
decisions under forces rather than recipes to apply by reflex. Every chapter
leads with where C# diverges from Java and where a pattern dissolves into a
language feature, with new, real-domain **C# 14** code on **.NET 10** behind it
and an **xUnit** and **NSubstitute** suite proving each example does what the
chapter claims.

This is a Jekyll site published to GitHub Pages, plus runnable example projects
under `examples/`.

> Third book in the patterncatalyst design-patterns series:
> [`cpp-design-patterns`](https://github.com/patterncatalyst/cpp-design-patterns) ✅ ·
> [`design-patterns-with-java`](https://github.com/patterncatalyst/design-patterns-with-java) ✅ ·
> **`csharp-design-patterns`** (this repo) · Python (planned).

## What makes the C# book different

- **Lead with divergence, not similarity.** C# is the closest of the four
  languages to Java, which is the risk. Patterns like Observer, Strategy, and
  Iterator dissolve into `event`, delegates, and `yield`/LINQ.
- **Patterns the platform defines** — Dispose, Options, Tester-Doer, TAP — a part
  with no equivalent in the C++ or Java books.
- **Concurrency patterns** — POSA2's Active Object and Monitor Object
  re-expressed on `async` and `Channel<T>`, mapped to modern C# for the first
  time.
- **Idiomatic C#** — extension members, expression trees, source generators,
  Roslyn analyzers, and `static abstract` members.

## Prerequisites

Local tools only, **no containers**, Linux-first (Fedora is the reference
platform). You need the **.NET 10 SDK**:

```sh
./scripts/check-prereqs.sh
```

See the prerequisites chapter (`_docs/00-prerequisites.md`) for the install
steps and the pinned, dated version matrix.

## Build the site locally

```sh
bundle install
bundle exec jekyll serve --baseurl ""
```

## Build and test the examples

```sh
./scripts/build.sh     # dotnet build, every example
./scripts/verify.sh    # dotnet test, every example
```

Each example is also standalone-testable from its own directory with
`dotnet test`.

## Repository layout

| Path | What it holds |
| --- | --- |
| `_docs/` | Chapter and appendix content (Markdown) |
| `_parts/` | Part landing pages (two-level nav) |
| `_plans/` | Build plans and status (not published to the site) |
| `examples/` | Runnable per-chapter demos (`examples/NN-pattern/`) |
| `diagrams/` | Paired `.svg` + `.excalidraw` figures |
| `assets/`, `_layouts/`, `_includes/` | Site theme and chrome |
| `scripts/` | `check-prereqs.sh`, `build.sh`, `verify.sh` |

## Contributing

Every change goes branch → PR → squash-merge; never commit directly to `main`.
See `CLAUDE.md` for the full working conventions.

## License

Tutorial content is [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
Sample code is [Apache 2.0](LICENSE).
