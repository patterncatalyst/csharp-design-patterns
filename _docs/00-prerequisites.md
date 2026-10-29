---
title: "Prerequisites and the toolchain"
order: 0
layout: tutorial
sectionid: docs
part: "orientation"
description: "The .NET 10 SDK, dotnet CLI, xUnit, and NSubstitute toolchain this book builds against on Fedora, how the example harness works with no containers, and why every code sample is a single idiomatic C# 14 block."
---

<!-- EXAMPLE (shipping domain): Smoke test: build and test a Consignment record. -->

## Learning objectives

By the end of this chapter you can:

- State the four pinned versions this book's examples build against, the .NET
  SDK, the C# language version, the target framework, and the test and
  mocking libraries, and name the file that enforces them for every example.
- Install the .NET 10 SDK on Fedora and confirm it with `dotnet --version`
  and `dotnet --list-sdks`.
- Run `scripts/check-prereqs.sh` from the repo root and read its output to
  confirm the toolchain gate passes before touching any example.
- Build and test `examples/00-prerequisites` on its own, outside the
  repo-level scripts, and explain what its six tests prove.
- Explain why no chapter in this book tells you to open Visual Studio, edit a
  `.sln` by hand, or run anything in a container.

## Why this matters

Every example in this book runs on tools you already have on a Linux
machine: the `dotnet` CLI, a text editor, and nothing else. Fedora is the
reference platform throughout, and there is no Windows path and no Visual
Studio path, because Visual Studio does not run on Linux. The editors this
book names, VS Code with the C# Dev Kit extension or JetBrains Rider, are
both front ends over the same `dotnet` CLI commands a contributor with only a
terminal and a text editor can run by hand. A chapter never tells you to
press F5, open Solution Explorer, or perform an action that only makes sense
inside one IDE.

There are also no containers anywhere in the example harness. No Docker, no
Kubernetes, no Blazor or other web front end sits between you and the code
you're reading. Every `.csproj` under `examples/` restores, builds, and tests
directly against the local SDK, and CI does the same thing on a GitHub-hosted
runner with `actions/setup-dotnet`, so the path you follow on your own
machine is identical to the one that gates every pull request. If an example
builds and its tests pass on your Fedora box, it builds and passes in CI for
the same reason, not a different one.

That consistency only holds if the toolchain itself is pinned and
verifiable. This chapter sets up exactly that: a fixed SDK version, a shared
set of compiler and analyzer settings that every example inherits without
per-project duplication, and a script that checks the floor version before
you write a line of code. Get this chapter working once, and every later
chapter's "Build, run, observe" section reduces to `dotnet test` in a
subdirectory.

{% include excalidraw.html name="00-prerequisites" caption="Figure 0.1: The .NET 10 SDK, dotnet CLI, and example harness this book builds every chapter against, with no IDE and no container between you and the code." %}

## The stack this book builds against

Five pinned, dated pieces make up the toolchain. The dates matter: a .NET
patch release lands roughly monthly, and this book's `_plans/` tracks a
re-verification date for each row rather than letting a snippet's version
number quietly go stale.

| Tool | Pin | Verified |
|---|---|---|
| .NET SDK | 10.0.113 | 2026-10-29 |
| Language | C# 14 | 2026-10-29 |
| Target framework | `net10.0` (LTS, supported to Nov 2028) | 2026-10-29 |
| Test framework | xUnit v3 (GA) | 2026-10-29 |
| Mocking | NSubstitute | 2026-10-29 |

`net10.0` is a Long Term Support release, which is why this book targets it
rather than a shorter-lived Standard Term Support release: a reader following
along a year from now is still on a supported runtime. C# 14 is the language
version that ships with that SDK, and every example uses it directly, with
nullable reference types turned on everywhere. A `string?` versus a `string`
in this book's code is a compiler-checked claim about whether a value can be
absent, not a convention a reviewer has to remember to enforce by eye.

xUnit v3 is a bigger change under the hood than the version bump suggests.
.NET 10 dropped the legacy VSTest path that older `dotnet test` invocations
relied on, and xUnit v3 runs instead on Microsoft.Testing Platform, a leaner
test host that starts your test assembly as its own executable rather than
hosting it inside a separate runner process. A test project opts into this
mode with `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`
in its `.csproj`, which every test project in this book sets. NSubstitute is
the mocking library for the handful of chapters whose examples need a
substitute collaborator; it has no `Setup()`/`.Object` ceremony to learn,
since a substitute is just an instance of the real interface with its calls
recorded.

None of these settings are repeated project by project. A single
`examples/Directory.Build.props`, scoped to the `examples/` subtree so it
never reaches the Jekyll site at the repository root, sets the target
framework, the language version, nullable reference types, implicit usings,
and `TreatWarningsAsErrors`. Every `.csproj` under `examples/` inherits this
file automatically, because MSBuild walks up from each project looking for
the nearest `Directory.Build.props` on disk. A warning that would normally
print and get ignored fails the build instead, in every example, without a
single project file repeating the setting. Analyzers run at
`AnalysisLevel=latest-recommended`, and `EnforceCodeStyleInBuild` means a
`.editorconfig` style violation is a build failure too, not a nit a linter
reports after the fact.

The individual package versions behind the pinned table above are surveyed
and recorded at the point each example project is created, not assumed from
memory. `examples/00-prerequisites/Consignments.Tests/Consignments.Tests.csproj`
pins `xunit.v3` 4.0.2, `xunit.runner.visualstudio` 4.0.1, and
`Microsoft.NET.Test.Sdk` 18.11.0, all verified against SDK 10.0.113 on
2026-10-29; `NSubstitute` 6.2.0 was surveyed the same way with
`dotnet package search NSubstitute`. NSubstitute is wired into every test
project from the start, including this one, even on a chapter like this one
whose `Consignment` type has no collaborator to substitute yet. That keeps a
later chapter's test project identical in shape to this one, so copying the
pattern forward never means retrofitting a missing package reference.

No Visual Studio appears anywhere in this list, and that's a platform fact,
not a preference. Visual Studio proper doesn't run on Linux, so a Fedora
contributor needs an editor that drives the same `dotnet` commands a
terminal already runs. VS Code with the C# Dev Kit extension adds the
language service, a test explorer, and a debugger on top of the CLI; Rider is
a full .NET IDE that runs natively on Linux and does the same job in a single
application. Either one is a convenience layer. The commands this chapter and
every later one shows are plain `dotnet` CLI invocations a contributor with
only a text editor and a terminal can run by hand, and that's the path this
book tests against.

## Installing the toolchain on Fedora

The `dotnet` CLI is the one tool you need for every step in this book:
restore, build, test, run, and package. On Fedora, the quickest path is the
distribution package:

```sh
sudo dnf install -y dotnet-sdk-10.0
dotnet --version        # should report 10.0.x (this book's pin is 10.0.113)
dotnet --list-sdks      # confirm a 10.0.x SDK is present
```

Fedora's package gives you the newest 10.0.x patch it carries, which tracks
this book's floor closely enough that the two rarely drift apart. If you need
the exact pinned patch, for instance to match a CI run bit for bit, install
it into your home directory instead, alongside whatever the system package
manager already has:

```sh
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --version 10.0.113 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"        # add to your shell profile to persist it
dotnet --version                         # 10.0.113
```

A project can lock every contributor and every CI run to the same SDK with a
`global.json` at its root. This book's own `global.json` doesn't pin a
version; it opts `dotnet test` into the Microsoft.Testing Platform runner
(`{"test": {"runner": "Microsoft.Testing.Platform"}}`) and leaves the SDK
floor to `scripts/check-prereqs.sh` instead, because the `dnf`-installed SDK
already satisfies the pin on the machines this book is built on. If you're
scaffolding your own project and want a hard version lock, add an `sdk` block
alongside it:

```json
{
  "sdk": {
    "version": "10.0.113",
    "rollForward": "latestFeature"
  }
}
```

Git is the only other tool this chapter assumes, for cloning the repository
and working with branches:

```sh
sudo dnf install -y git
git --version
```

That's the complete list. No container runtime, no database server, no
message broker belongs on this machine to follow any chapter in this book.

If you're adding an editor, VS Code is also a Fedora package:

```sh
sudo dnf install -y code
```

Then add the **C# Dev Kit** extension from the Marketplace inside VS Code.
Rider doesn't ship through `dnf`; download it from JetBrains and run its
installer, or manage it through JetBrains Toolbox if you already use that for
other JetBrains products. Neither step changes anything about how an example
builds or tests; it only changes how you navigate and step through the code
while you read.

## Verifying the toolchain: check-prereqs.sh

`scripts/check-prereqs.sh` is the toolchain gate: a small POSIX shell script
that checks the one thing every example actually needs, a `dotnet` CLI on
`PATH` reporting at least a 10.x SDK, and prints what it found. It's safe to
run as a CI step or as the first thing you do after cloning the repository,
because it exits non-zero the moment anything is missing or below the floor,
before a build or a test run has the chance to fail with a less direct error.

Run it from the repository root:

```sh
./scripts/check-prereqs.sh
```

On a machine with the SDK installed, it reports the version it found and
exits clean:

```
Checking local toolchain for Design Patterns with .NET / C#...

OK: dotnet -> 10.0.113

All prerequisites found.
```

If the `dotnet` CLI isn't on `PATH` at all, or the SDK it finds is older than
major version 10, the script says exactly which check failed and points back
at this chapter rather than leaving you to guess from a build error further
down the line. That's the whole contract: one script, one clear pass or fail,
run once before you touch any example.

## Every example is standalone

Every example under `examples/` is a complete, independently testable
project. You can clone just that one directory's contents, run `dotnet test`
inside it, and get the same result you'd get from the repository's full test
run, because nothing about an individual example depends on state left
behind by a different one. This matters for a reader who wants to copy a
single chapter's code into a scratch directory and keep iterating on it
without pulling in the whole repository.

The repository still gives you a one-shot path across every example at once.
`scripts/build.sh` builds every `.csproj` under `examples/` with
`dotnet build -c Release`, and `scripts/verify.sh` runs the test suite for
each one with `dotnet test --project`, the flag .NET 10's Microsoft.Testing
Platform mode requires in place of a bare positional path. Run either script
from the repository root once you've worked through more than one chapter's
example and want to confirm nothing earlier broke.

Each example's own layout stays small: one library project, one
test project in a subdirectory, and an optional `.slnx` solution file that
lists both, generated and edited only through `dotnet sln` so no hand-written
XML drifts from what the CLI would produce. The library project's `.csproj`
excludes the test subdirectory from its own compile items
(`DefaultItemExcludes`), so the two projects never fight over which one owns
a given `.cs` file. None of this is specific to this chapter's example; it's
the shape every chapter's example follows.

## Build, run, observe

`examples/00-prerequisites` is this chapter's own example and the smoke test
for everything above it: a `Consignment` record and the two small value
types it depends on, `ConsignmentId` and `Weight`, modeling a single booked
shipment with an id, a destination, and a weight. There's no database, no
HTTP call, and nothing to run beyond the local CLI.

Build and test it on its own, exactly as a reader following only this
chapter would:

```sh
cd examples/00-prerequisites
dotnet test
```

That produces six passing tests:

```
Running tests from examples/00-prerequisites/Consignments.Tests/bin/Debug/net10.0/Consignments.Tests.dll (net10.0|x64)
examples/00-prerequisites/Consignments.Tests/bin/Debug/net10.0/Consignments.Tests.dll (net10.0|x64) passed (826ms)

Test run summary: Passed!
  total: 6
  failed: 0
  succeeded: 6
  skipped: 0
  duration: 1s 168ms
```

Each test verifies a behavior, not just a build that exits zero. Two
consignments built from the same id, destination, and weight compare equal,
because `Consignment` is a record and records carry value semantics by
default. A `with` expression amends one property and leaves the original
untouched, which is the test for a design where "changing" a consignment
never mutates the one you started with. And the primary constructors of
`Consignment`, `Weight`, and `ConsignmentId` each reject the invalid input
they claim to reject, a blank destination, a non-positive weight, a blank
booking reference, by throwing rather than silently accepting it. That
pattern, a primary constructor that redeclares its property with a validating
initializer, is the one this book reaches for throughout whenever a value
type needs to make an invalid state unrepresentable.

The toolchain settings from earlier in this chapter show up directly in this
example, not just in the test results. `ConsignmentId` and `Weight` are
declared as `readonly record struct`, so a `Consignment` holds its id and
weight inline rather than as two more heap allocations, and the
nullable-reference-types setting from `Directory.Build.props` backs that
design: a `string` field on either type can never silently become null,
because the compiler flags any path that would let one. Comment out a
validation check and rerun `dotnet build`, and `TreatWarningsAsErrors` turns
the resulting nullable or analyzer warning into a build failure before
`dotnet test` ever starts. That's the toolchain gate from this chapter acting
on code you wrote yourself, not just on the SDK version. This is the one
chapter where confirming the gate is the point; every chapter after this one
trusts that it already passed and moves straight to the pattern under
discussion.

Run the toolchain gate again from the repository root, so you can see both
checks this chapter sets up working together:

```sh
./scripts/check-prereqs.sh
```

```
Checking local toolchain for Design Patterns with .NET / C#...

OK: dotnet -> 10.0.113

All prerequisites found.
```

Two green results, one script and one `dotnet test` run, are everything this
chapter asks you to confirm. If both match what's shown here, every later
chapter's own example builds and tests the same way, from its own directory,
with nothing extra to install.

## For deeper coverage

- Gerard Meszaros, *xUnit Test Patterns: Refactoring Test Code*
  (Addison-Wesley, 2007), is the source for the four-phase test structure and
  the test-double taxonomy behind this book's testing chapter and its
  xUnit appendix; see the test methods in `ConsignmentTests.cs` for a small,
  concrete instance of that structure.
- [.NET 10 and .NET Framework support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) from Microsoft covers which releases are LTS versus STS and for how long, the basis for this book targeting `net10.0`.
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro) explains the test host xUnit v3 runs on in .NET 10, including the `dotnet test` integration this chapter's `global.json` opts into.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
