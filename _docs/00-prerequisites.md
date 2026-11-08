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

- Name the pinned SDK, language version, target framework, test and mocking
  libraries, and the file that applies them to every example.
- Install the .NET 10 SDK on Fedora and confirm it with `dotnet --version`
  and `dotnet --list-sdks`.
- Run `scripts/check-prereqs.sh` and read its result.
- Build and test `examples/00-prerequisites` on its own.

## Why this matters

Every example runs on the `dotnet` CLI and a text editor. Fedora is the
reference platform. There's no Windows path and no Visual Studio path, since
Visual Studio doesn't run on Linux. Use VS Code with the C# Dev Kit extension,
or JetBrains Rider; both drive the same `dotnet` commands you can type in a
terminal, so no step needs F5 or Solution Explorer.

There are no containers either: no Docker, no Kubernetes, no Blazor or other web
front end. Every `.csproj` under `examples/` restores, builds and tests against
the local SDK. CI does the same on a GitHub-hosted runner with
`actions/setup-dotnet`, so a green run on your machine is a green run in CI for
the same reason.

{% include excalidraw.html name="00-prerequisites" caption="Figure 0.1: The .NET 10 SDK, dotnet CLI, and example harness this book builds every chapter against, with no IDE and no container between you and the code." %}

## The stack

| Tool | Pin | Verified |
|---|---|---|
| .NET SDK | 10.0.113 | 2026-10-29 |
| Language | C# 14 | 2026-10-29 |
| Target framework | `net10.0` (LTS, supported to Nov 2028) | 2026-10-29 |
| Test framework | xUnit v3 (GA) | 2026-10-29 |
| Mocking | NSubstitute | 2026-10-29 |

The platform is **.NET 10 with C# 14**. `net10.0` is a Long Term Support
release, so a reader a year from now is still on a supported runtime. C# 14 ships
with that SDK. Nullable reference types are on everywhere: `string?` against
`string` is a compiler-checked statement about whether a value can be absent.

xUnit v3 runs on Microsoft.Testing Platform, which starts a test assembly as
its own executable instead of hosting it in a separate runner process. .NET 10
dropped the legacy VSTest path that older `dotnet test` invocations used. A test
project opts in with
`<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>` in
its `.csproj`, and every test project here sets it. NSubstitute supplies
substitute collaborators: a substitute is an instance of the real interface with
its calls recorded, with no `Setup()` or `.Object` step.

### One file for every project

`examples/Directory.Build.props` sets the target framework, language version,
nullable reference types, implicit usings and `TreatWarningsAsErrors`. MSBuild
walks up from each `.csproj` to the nearest `Directory.Build.props`, so every
project under `examples/` inherits it, and the file's location keeps it away from
the Jekyll site at the repository root. Analyzers run at
`AnalysisLevel=latest-recommended`, and `EnforceCodeStyleInBuild` makes an
`.editorconfig` violation a build failure. A warning fails the build in every
example without any project repeating the setting.

### Package versions

Package versions are surveyed when each example project is created.
`examples/00-prerequisites/Consignments.Tests/Consignments.Tests.csproj` pins
`xunit.v3` 4.0.2, `xunit.runner.visualstudio` 4.0.1 and `Microsoft.NET.Test.Sdk`
18.11.0, verified against SDK 10.0.113 on 2026-10-29. `NSubstitute` 6.2.0 was
surveyed with `dotnet package search NSubstitute`. Every test project references
NSubstitute from the start, even where nothing needs substituting yet, so all
test projects share one shape.

## Installing the toolchain on Fedora

The `dotnet` CLI restores, builds, tests, runs and packages. The distribution
package is the quickest install:

```sh
sudo dnf install -y dotnet-sdk-10.0
dotnet --version        # should report 10.0.x (this book's pin is 10.0.113)
dotnet --list-sdks      # confirm a 10.0.x SDK is present
```

Fedora ships the newest 10.0.x patch it carries. To match a CI run bit for bit,
install the exact patch into your home directory beside the system SDK:

```sh
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --version 10.0.113 --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"        # add to your shell profile to persist it
dotnet --version                         # 10.0.113
```

The repository's `global.json` doesn't pin an SDK. It opts `dotnet test` into the
Microsoft.Testing Platform runner (`{"test": {"runner":
"Microsoft.Testing.Platform"}}`) and leaves the floor to
`scripts/check-prereqs.sh`. For a hard lock in your own project, add an `sdk`
block:

```json
{
  "sdk": {
    "version": "10.0.113",
    "rollForward": "latestFeature"
  }
}
```

Git is the only other tool:

```sh
sudo dnf install -y git
git --version
```

No container runtime, database server or message broker is needed.

VS Code is also a Fedora package:

```sh
sudo dnf install -y code
```

Add the **C# Dev Kit** extension from the Marketplace. Rider isn't in `dnf`;
download it from JetBrains or install it through JetBrains Toolbox. An editor
changes how you navigate the code, not how an example builds.

## Verifying the toolchain: check-prereqs.sh

`scripts/check-prereqs.sh` is a POSIX shell script that checks for a `dotnet` CLI
on `PATH` reporting SDK 10 or later. It exits non-zero when the CLI is missing or
below the floor, before a build fails with a less direct error, so it also works
as a CI step. Run it from the repository root:

```sh
./scripts/check-prereqs.sh
```

```
Checking local toolchain for Design Patterns with .NET / C#...

OK: dotnet -> 10.0.113

All prerequisites found.
```

When it fails, it names the check that failed.

## Every example is standalone

Each directory under `examples/` is a complete project. Copy one into a scratch
directory, run `dotnet test`, and you get the same result as the full repository
run, since no example depends on another.

For a run across all of them, `scripts/build.sh` builds every `.csproj` with
`dotnet build -c Release`, and `scripts/verify.sh` runs each suite with
`dotnet test --project`, the form Microsoft.Testing Platform mode requires in
place of a bare positional path.

An example holds one library project, one test project in a subdirectory, and an
optional `.slnx` solution file maintained only through `dotnet sln`. The library
`.csproj` excludes its test subdirectory from its compile items
(`DefaultItemExcludes`), so each `.cs` file belongs to one project.

## Build, run, observe

`examples/00-prerequisites` models one booked shipment: a `Consignment` record
with a `ConsignmentId`, a destination and a `Weight`. Nothing runs beyond the
local CLI.

```sh
cd examples/00-prerequisites
dotnet test
```

Six tests pass. Two consignments with the same id, destination and weight are
equal, because records have value equality. A `with` expression returns an
amended copy and leaves the original unchanged. Each primary constructor rejects
its invalid input, a blank destination, a non-positive weight or a blank booking
reference, by throwing. A primary constructor that redeclares its property with a
validating initializer is the standard way to make an invalid value
unrepresentable, and later examples use it throughout.

`ConsignmentId` and `Weight` are `readonly record struct` types, so a
`Consignment` holds them inline instead of as two more heap allocations. With
nullable reference types on, a `string` member can't silently become null.

Experiments:

- Comment out a validation check and run `dotnet build`: the resulting analyzer
  or nullable warning fails the build under `TreatWarningsAsErrors`, before
  `dotnet test` starts.

## For deeper coverage

- Gerard Meszaros, *xUnit Test Patterns: Refactoring Test Code*
  (Addison-Wesley, 2007), is the source for the four-phase test structure and
  the test-double taxonomy used throughout;
  `ConsignmentTests.cs` is a small instance of that structure.
- [.NET 10 and .NET Framework support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) from Microsoft covers which releases are LTS versus STS and for how long, the basis for targeting `net10.0`.
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro) explains the host xUnit v3 runs on in .NET 10, including the `dotnet test` integration the repository's `global.json` opts into.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
