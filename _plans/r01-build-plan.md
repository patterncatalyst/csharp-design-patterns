---
title: "r01 build plan — scaffold + toolchain + Ch35 spike"
order: 1
output: false
---

# r01 — Design Patterns with .NET / C# scaffold

First iteration of the THIRD book in the series. Mirrors the completed
`cpp-design-patterns` and `design-patterns-with-java` books, translated to
.NET 10 + C# 14 + xUnit/NSubstitute. Source of truth for r01 scope and
acceptance criteria. Never touched by content executors.

## Verified toolchain (Fedora, 2026-10-29)

| Item | Version |
| --- | --- |
| .NET SDK | 10.0.113 (.NET 10 LTS, supported to Nov 2028) |
| Language | C# 14, nullable reference types enabled throughout |
| Test | xUnit (v3), via `dotnet test` |
| Mocking | NSubstitute |
| Analysis | Roslyn analyzers + .editorconfig, warnings as errors |

Re-verify the SDK version before each release. No containers anywhere;
local `dotnet` CLI only, Linux-first (Fedora). Codetabs axis is two modern
idioms side by side, plus a `Java | C#` pair in the divergence chapters.

## r01 scope (ships)

1. Public repo `patterncatalyst/csharp-design-patterns` (created, cloned).
2. Jekyll site scaffold mirroring the Java book: layouts, includes, assets,
   `.NET` purple accent, 16 part landing pages, 47 chapter stubs, 7 appendix
   stubs, examples/diagrams/bibliography pages, CI + Pages workflows.
3. `scripts/check-prereqs.sh` for the .NET 10 toolchain.
4. Ch35 spike: the `StowagePlan` Active Object on `Channel<T>` built and
   verified against its four tests (feasibility gate for Part 11).

## Research basis and assigned example, per chapter

Basis: 📗 book-backed · 📘 docs-backed · 🔬 original research (highest
verification bar; claims must come from a runnable experiment).

| Ch | Title | Basis | Shipping-domain example |
| --- | --- | --- | --- |
| 0 | Prerequisites and the toolchain | 📘 | Smoke test: build and test a Consignment record |
| 1 | Patterns as decisions under forces | 📗 | The rating problem stated as competing forces |
| 2 | What 1994 assumed, and what C# invalidated | 🔬 | (argument chapter, no runnable example) |
| 3 | The divergence map | 🔬 | (argument chapter, no runnable example) |
| 4 | Naming patterns for teams and AI assistants | 📗 | Naming across the consignment lifecycle |
| 5 | SRP, OCP, and LSP | 📗 | A RateCalculator doing pricing, tax, and audit, split |
| 6 | ISP, DIP, and DI with the built-in container | 📗 📘 | ICarrierGateway behind a DI registration |
| 7 | The Dispose pattern and IAsyncDisposable | 📘 | ManifestWriter holding a file handle; async ledger append |
| 8 | Value types: struct, readonly struct, Span<T> | 📗 📘 | Money, Weight, SlotId as structs; Span<T> over an EDI segment |
| 9 | NRTs, records, init/required/with | 📘 📗 | Consignment as a record; with for amendments |
| 10 | Proving it works: xUnit and NSubstitute | 📗 📘 | The rating engine under xUnit and NSubstitute |
| 11 | Strategy: delegates, interfaces, keyed services | 🔬 📘 | Rate quotes priced three ways |
| 12 | Template Method and the delegate alternative | 📗 | Customs declaration generation, varying by destination |
| 13 | State and finite state machines | 📗 📘 | Consignment lifecycle FSM + transit-delay model |
| 14 | Command, undo, and Task-returning commands | 📗 | Booking amendments with undo |
| 15 | Factory Method and Abstract Factory | 🔬 📗 | A gateway per carrier, resolved by type (IGateway<TCarrier>) |
| 16 | Builder, required members, Prototype with with | 📘 📗 | A booking request with many optional fields; with for amendments |
| 17 | Singleton, Lazy<T>, and DI lifetimes | 📘 📗 | The tariff table: expensive to load, needed everywhere |
| 18 | Adapter, Facade, Bridge | 📗 📘 | Three carrier APIs behind one interface; Money/Weight conversions |
| 19 | Decorator and Proxy | 📗 📘 | Rate pipeline; caching proxy over a slow gateway |
| 20 | Composite and Flyweight | 📗 📘 | Consolidated shipments; port and commodity codes interned |
| 21 | Observer: the event keyword | 📘 🔬 | Handling events raised as cargo moves |
| 22 | Mediator and Chain of Responsibility | 📗 📘 | Customs clearance chain |
| 23 | Iterator: yield return, LINQ, IAsyncEnumerable | 📘 📗 | Paging a tracking history; IAsyncEnumerable over a live feed |
| 24 | Visitor, Memento, Interpreter | 📗 🔬 | Tariff rules as expression tree; amendment rollback via memento |
| 25 | Extension methods and C# 14 extension members | 🔬 📘 | Extending a carrier SDK type you do not own |
| 26 | Expression trees: code as data | 🔬 📘 | The tariff rule engine as inspectable expression trees |
| 27 | Source generators: patterns at build time | 🔬 📘 | Generating carrier gateway adapters from a manifest |
| 28 | Roslyn analyzers: patterns enforced at compile time | 🔬 📘 | Analyzer: money is never decimal in a wire contract |
| 29 | static abstract members and generic math | 🔬 📘 | IRateable<T> with static abstract members over Money |
| 30 | The Options pattern | 📘 | Carrier gateway configuration |
| 31 | Tester-Doer and the Result debate | 📘 🔬 | Parsing an EDI message and validating a declaration |
| 32 | TAP and the async conventions | 📘 | The carrier gateway's async API surface |
| 33 | Why .NET chose stackless async | 📘 | Booking throughput against a single sailing |
| 34 | Thread-safe creation | 📗 📘 | The tariff table under concurrent first use |
| 35 | Monitor Object and Active Object on Channel<T> | 🔬 | StowagePlan: agents concurrently reserving container slots |
| 36 | Producer-Consumer and pipelines as patterns | 📗 📘 | Handling-event ingest pipeline |
| 37 | Pools: object, connection, ArrayPool<T> | 📗 📘 | Carrier gateway connection pool |
| 38 | Classic GoF under concurrency | 🔬 | Handling events across threads; event feed as IAsyncEnumerable |
| 39 | Shortcut patterns | 🔬 📘 | Dispatch table for document types (FrozenDictionary) |
| 40 | Anti-patterns and pattern misuse | 📗 | The over-abstracted rating engine |
| 41 | Seams in C#: non-virtual by default | 🔬 | A legacy RateEngine: sealed, non-virtual, untestable |
| 42 | Dependency-breaking techniques for C# 14 | 🔬 📗 | Breaking the same engine's dependencies |
| 43 | Fowler vs Feathers, contrasted and combined | 📗 | The same engine under Fowler and under Feathers |
| 44 | Refactor or rewrite | 📗 | The rate engine: refactor or rewrite |
| 45 | Final project | 🔬 | Booking, rating, stowage, clearance, invoice end to end |
| 46 | Working with AI and LLM tools | 🔬 | Reviewing LLM-generated pattern code in the shipping domain |

## Appendices

| Appx | Title |
| --- | --- |
| A | Build and toolchain reference |
| B | xUnit reference |
| C | NSubstitute reference |
| D | xUnit test patterns (Meszaros) |
| E | Pattern-to-force quick reference |
| F | Programming principles reviewed in code |
| G | C# 15 / .NET 11 delta |

## Authoring order (from the outline map)

Parts 0–8 first (establish domain, voice, testing baseline), then the
research-heavy Parts 9 and 11. Part 11 last; Ch35 is the single largest
unknown and is de-risked by the r01 spike. Decks are the final deliverable,
built after all chapters and examples: 101, 201 (idiomatic C#), 301
(testing/legacy/AI), 401 (concurrency patterns).

