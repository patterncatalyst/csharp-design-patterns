# CLAUDE.md — Design Patterns with .NET / C#

Ground rules for working in this repo. Read `_plans/r01-build-plan.md` for the
current iteration's status board and acceptance criteria before starting work;
it is the source of truth for scope and is never touched by content executors.

This is the **third** book in the series: `cpp-design-patterns` ✅ and
`design-patterns-with-java` ✅ are complete and are **reference-only** for process
and series conventions. They are **not a code source** — never port their C++ or
Java examples; every C# example is written fresh against its own shipping-domain
scenario.

## Platform and tools

- **Linux-first, Fedora is the reference platform. No Windows, no Visual
  Studio** (it does not run on Linux). Editors are VS Code with C# Dev Kit, or
  Rider. Tooling is the **`dotnet` CLI** only — no "press F5", no `.sln` rituals
  that only make sense in Visual Studio, no IDE screenshots. The file system is
  case-sensitive, paths use `/`, line endings are LF.
- **No containers** for building or testing examples. No Docker, no Kubernetes,
  no Blazor or any other UI or web front end. CI installs the same local
  toolchain directly on the runner via `actions/setup-dotnet` (see
  `.github/workflows/ci.yml`); that is not something a chapter ever tells a
  reader to do differently.
- Every example builds with the **.NET 10 SDK** and `dotnet` CLI, and is tested
  with **xUnit** and **NSubstitute** for collaborator substitution. A test must
  verify *behavior* (the pattern does what it claims), not just that the build
  exits 0.
- Pinned toolchain (see `_plans/r01-build-plan.md` for the verified versions):
  .NET SDK 10.0.113 (.NET 10 LTS), C# 14, nullable reference types enabled
  throughout, Roslyn analyzers with warnings as errors, xUnit (v3), NSubstitute.
  `scripts/check-prereqs.sh` verifies the floor version; `scripts/build.sh`
  builds every example; `scripts/verify.sh` runs the suites.
- Every example under `examples/` is standalone-testable on its own
  (`cd examples/<NN-pattern> && dotnet test`), in addition to being built by the
  repo-level `scripts/`.

## C# standards

- This book covers exactly **.NET 10 / C# 14**, the current LTS. Never treat a
  pre-release feature as settled. The one planned exception is **Appendix G**,
  the additive C# 15 / .NET 11 (STS) delta; the chapters themselves stay on
  C# 14.
- **Code is idiomatic modern C# 14 only.** Do NOT write classic- or older-C#
  code purely for contrast. Each sample is a single fenced ```csharp block in
  the current idiom. The ONE exception is the legacy-code / Feathers part
  (Part 13), where comparing inherited older code with its refactored form is
  the actual subject; there, a "before" snippet is the point.
- Nullable reference types are **on** everywhere, shared through
  `Directory.Build.props`. Treat warnings as errors.
- The `codetabs.html` include is for comparing genuinely different **modern**
  C# idioms side by side when a chapter benefits (a delegate form next to an
  interface form, a record next to a struct), relabeled to the idioms compared.
  The divergence chapters (Part 2) also use a `Java | C#` pair to show where the
  two languages part ways. Never a modern form against a retired one. Use the
  bare include `{% include codetabs.html langs="..." %}` on its own line, one
  fenced ```csharp block per label, and never wrap it in raw tags (that stops
  Liquid running it). Default to a single C# block; reach for a codetab only for
  a real idiom-to-idiom comparison.

## Example code

- Every example is **new**, real-domain C# written for this book, in the
  **shipping domain** (consignments, carriers, rate quotes, routing, tracking,
  documentation, billing), never `Foo`/`Bar`/`Baz`, shapes, or animals. The
  seven domain areas and the per-chapter example are fixed in
  `_plans/r01-build-plan.md`; do not invent a new domain area.
- References for the testing idiom: the official xUnit and NSubstitute docs, and
  the *xUnit Test Patterns* bibliography entry for the four-phase test and
  test-double taxonomy. The chapter may mention Moq for readers on existing Moq
  codebases, but never tells a team to migrate a working Moq suite.
- Primary sources first: for the areas with no book coverage (source generators,
  analyzers, keyed services, extension members, the POSA2-on-async mapping), the
  authority is **Microsoft Learn and the language spec**, not blog posts. A claim
  in a chapter cites the docs or a runnable experiment.
- **Liquid gotcha:** Jekyll parses `{{ ... }}` as a Liquid tag even inside a
  fenced code block. If a C# snippet contains a literal `{{` sequence (for
  example an interpolated string or a collection expression butting against
  another brace), wrap that fenced block in `{% raw %}` / `{% endraw %}`, or the
  site build fails. Verify with a real `bundle exec jekyll build`, not just a
  standalone `dotnet build`.

## Diagrams

- Diagrams are heavy — most chapters get at least one — and follow a
  **C4-style** standard: context and component framing, not class diagrams.
- Every diagram ships as a paired `diagrams/<name>.svg` +
  `diagrams/<name>.excalidraw`, generated with `lgtm-diagram-generator`, and is
  registered in `_data/diagrams.yml`.
- Emphasis/ink boxes are **light fill with dark text**, never a dark-filled box
  with light text.

## Chapter spine

Every chapter (`_docs/NN-*.md`) follows the same shape:

1. **Learning objectives** near the top.
2. **Why this matters**, near the top, before the mechanics.
3. **A diagram** (required for every new chapter), placed after "Why this
   matters" and before the body, matching the sibling books.
4. The body.
5. **"Build, run, observe"** on every hands-on chapter, pointing at the matching
   `examples/` demo.
6. **"When not to use it"** on every pattern chapter, with exactly one **AI
   sidebar** box (no more than one per chapter).
7. **"For deeper coverage"** at the end, citing the hub's `books.yml` and this
   book's own `/bibliography/`.

- Prose target: body prose (excluding code and diagrams) has a **2,000-word hard
  floor** and a **2,500-word target that is not a ceiling**. There is no hard
  upper limit; longer is fine when value-driven, never padding. As a chapter
  approaches or passes 2,500 words, add another diagram to carry the extra
  density rather than trimming substantive content.
- Voice: no em-dash (zero tolerance), no rule-of-three, no contrived AI phrasing,
  no confessional register. Run `lgtm-professional-voice` before calling a
  chapter done.
- **Reader-facing content stands alone.** No baked-in chapter or part numbers or
  internal codes in figures, and no cross-references to sibling sites in
  reader-facing pages or decks. Keep generic "sibling book" phrasing.
- Verification status lives **only** in `_plans/`, never on a reader-facing page.
- Parts use **Arabic numbering** (Part 0, Part 1, ... Part 15) with a descriptive
  title, never Roman numerals.
- **No intra-book chapter-number cross-references** ("as we saw in ch8") in prose
  or slides. Name the pattern or idiom instead.
- Companion decks are the **last** deliverable for a release iteration: 101, 201
  (idiomatic C#), 301 (testing/legacy/AI), and 401 (concurrency patterns).

## Git workflow

- **Never commit directly to `main`.** Every change, including a one-line docs
  fix, goes branch → PR → squash-merge.
- Each release iteration gets its own `release/rNN` branch.

## Skills to use

- `lgtm-csharp` — the .NET 10 / C# 14 scaffold and toolchain conventions (SDK
  pin, `Directory.Build.props`, xUnit + NSubstitute, analyzers, prerequisites).
- `lgtm-jekyll` — site scaffolding and diagram embedding conventions.
- `lgtm-tutorial` — chapter authoring workflow and per-chapter example scaffolding.
- `lgtm-presentation` — companion decks (last deliverable).
- `lgtm-diagram-generator` — paired SVG + `.excalidraw` figures.
- `lgtm-professional-voice` — voice pass before a chapter or release ships.
- `lgtm-idiomatic-review` — idiomatic .NET 10 / C# 14 review pass on example code.
- `lgtm-github` — branch/PR/tag/release workflow for this GitHub-hosted repo.
- `lgtm-relay` — plan/execute/validate relay for a release iteration.

## MCP tooling

There is no `dotnet` dev-agent MCP equivalent to the Quarkus or Camel servers.
Use the `dotnet` CLI via the shell, the official **NuGet MCP server** (`dnx`,
requires the .NET 10 SDK) for package operations, and Microsoft Learn for docs.
