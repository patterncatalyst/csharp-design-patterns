---
title: "Build and toolchain reference"
order: 47
layout: tutorial
sectionid: docs
part: appendices
appendix: "A"
description: "A one-stop reference for building and testing every example: the pinned .NET 10 SDK, the dotnet CLI, Directory.Build.props, standalone versus whole-repository builds, the scripts/ helpers, and both editors."
---

## Learning objectives

By the end of this appendix you can:

- Locate the one file that sets the target framework, language version,
  nullable reference types, and warnings-as-errors for every example, and
  explain why it lives where it does rather than at the repository root.
- Read `global.json` correctly: state what it configures (the test runner)
  and what it does not configure (an SDK version pin).
- Run `scripts/check-prereqs.sh` and interpret a pass or a failure.
- Build and test the whole repository with `scripts/build.sh` and
  `scripts/verify.sh`, and explain why `scripts/verify.sh` passes `--project`
  to `dotnet test` instead of a bare path.
- Build and test a single example on its own, outside the repository-level
  scripts, and name the two editors this book assumes.

## Why this matters

Every example in this book builds and tests the same way: a local `dotnet`
CLI, no IDE project wizard, no container. That consistency depends on three
small, ordinary files doing their job quietly in the background, a shared
MSBuild properties file, a `global.json` that opts into a specific test
runner, and a trio of POSIX shell scripts. None of the pattern chapters
explain these files in depth, because explaining them once, here, means a
chapter's own "Build, run, observe" section can stay short: `cd` into an
example directory and run `dotnet test`. This appendix is the reference you
come back to when a build behaves unexpectedly, when you want to know exactly
which compiler flags are in effect, or when you are scaffolding a new example
of your own and want it to inherit the same settings as every other one.

## Directory.Build.props: the shared compiler and analyzer settings

MSBuild looks for a file named `Directory.Build.props` by walking up the
directory tree from each project file it builds, and imports the first one it
finds automatically, with no explicit `<Import>` required in the `.csproj`
itself. This book places that file at `examples/Directory.Build.props`, one
level below the repository root rather than at the root itself:

```xml
<Project>

  <PropertyGroup>
    <!-- .NET 10 LTS, supported to Nov 2028 -->
    <TargetFramework>net10.0</TargetFramework>

    <!-- C# 14 with nullable reference types on everywhere -->
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Warnings are errors; analyzers and editorconfig style fail the build -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>

    <!-- Deterministic, reproducible builds -->
    <Deterministic>true</Deterministic>
  </PropertyGroup>

</Project>
```

The placement matters as much as the contents. MSBuild's upward search would
keep going past `examples/` and reach a `Directory.Build.props` at the
repository root if one existed there, which would then apply to the Jekyll
site tooling that lives alongside `_docs/`, `_data/`, and the rest of this
book's source. Scoping the file to `examples/` instead means every `.csproj`
under that subtree inherits it, and nothing outside that subtree is affected
by a setting meant for example code.

Seven properties do the real work. `TargetFramework` pins every example to
`net10.0`, the Long Term Support release. `LangVersion` pins the compiler to
C# 14, and `Nullable` turns on nullable reference types everywhere, so a
`string?` versus a `string` is a compiler-checked claim rather than a
convention a reviewer enforces by eye. `ImplicitUsings` adds the common
`using` directives (`System`, `System.Linq`, and the like) without repeating
them in every file. `TreatWarningsAsErrors` converts any compiler or analyzer
warning into a build failure, `EnforceCodeStyleInBuild` does the same for an
`.editorconfig` style violation that a linter would otherwise report only
after the fact, and `AnalysisLevel` set to `latest-recommended` opts every
example into the current recommended tier of the built-in Roslyn analyzers.
`Deterministic` makes two builds from the same source produce byte-identical
output, which matters for reproducing a CI failure locally.

None of this is repeated project by project. A test project's `.csproj` adds
only what makes it a test project (its own package references and an
`IsTestProject` flag); the target framework, language version, and nullable
setting all come from the shared file one directory up. Adding a new example
under `examples/` means its `.csproj` automatically gets the same compiler
and analyzer posture as every other one, with nothing to copy forward and
nothing to drift.

## global.json: selecting the test runner, not the SDK

The repository root carries one more file, `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Read this file literally. It contains exactly one setting, and that setting
selects which test runner `dotnet test` uses for this repository. It does
not pin an SDK version, and it carries no `sdk` block with a `version` or a
`rollForward` policy. A `global.json` can do both jobs, an SDK version lock
and a test runner selection, but this one does only the second. The SDK
floor is enforced by a different mechanism, described next.

Selecting `Microsoft.Testing.Platform` changes what `dotnet test` runs under
the hood. .NET 10 supports two test execution paths: the legacy
VSTest-based path, and Microsoft.Testing.Platform, a leaner host that starts
a test assembly as its own executable instead of hosting it inside a
separate runner process. This repository's `global.json` opts every test
project into the newer path. The `xunit.v3` package itself ships the MSBuild
wiring a test project needs to run under that host, generating a
self-registering entry point for the test assembly at build time, so no test
project in this book carries any extra Microsoft.Testing.Platform property
of its own; the repository-wide `global.json` setting is the only switch a
contributor has to know about.

One consequence shows up directly in `scripts/verify.sh`: Microsoft.Testing
Platform's `dotnet test` mode requires a project to be named explicitly with
`--project`, and rejects a bare positional path the way the older VSTest path
accepted one. That single flag is the most visible day-to-day effect of this
file.

## check-prereqs.sh: the SDK floor

`scripts/check-prereqs.sh` is a small POSIX shell script with one job: find
`dotnet` on `PATH` and confirm its major version is at least 10.

```sh
check_dotnet() {
    if ! have dotnet; then
        echo "MISSING: dotnet (need .NET SDK >= 10). See _docs/00-prerequisites.md"
        status=1
        return
    fi
    version="$(dotnet --version 2>/dev/null)"
    echo "OK: dotnet -> ${version}"
    major="$(echo "${version}" | cut -d. -f1)"
    if [ -n "${major}" ] && [ "${major}" -lt 10 ]; then
        echo "WARNING: .NET SDK major version ${major} is below the floor of 10"
        status=1
    fi
}
```

This is a floor check, not an exact pin. It accepts any `10.x` SDK, and the
version this book verifies its examples against, 10.0.113, is recorded as a
comment in the script and in the prerequisites chapter rather than enforced
as a hard equality check. That design choice matches how `global.json`
behaves in this repository: nothing here forces every contributor's machine
to carry the identical patch version, only a version new enough that the
language and analyzer features this book relies on are present. Run it from
the repository root:

```sh
./scripts/check-prereqs.sh
```

A passing run prints the version it found and exits zero; a missing or
too-old SDK prints which check failed and exits non-zero, so a build or test
failure further down the chain never has to be traced back to the toolchain
by hand.

## build.sh and verify.sh: the whole-repository loop

`scripts/build.sh` builds every example in Release configuration:

```sh
for proj in "${examples_dir}"/*/*.csproj; do
    echo "--- ${proj}"
    dotnet build -c Release "${proj}"
done
```

Its glob, `examples/*/*.csproj`, matches a `.csproj` one directory below
`examples/`, which is where each example's library project lives. A test
project lives one directory deeper still, inside its own `*.Tests/`
subdirectory, so this script builds every library and leaves the test
projects to `scripts/verify.sh`, which builds each one implicitly as part of
running its tests.

`scripts/verify.sh` runs the test suite for every example:

```sh
for proj in "${examples_dir}"/*/*Tests*.csproj "${examples_dir}"/*/*.Tests/*.csproj; do
    [ -f "${proj}" ] || continue
    echo "--- ${proj}"
    dotnet test --project "${proj}"
done
```

The comment in the script states the reason for `--project` directly:
".NET 10's Microsoft.Testing.Platform mode of `dotnet test` (opted into via
the repo-root `global.json`) requires the project path via `--project`; a
bare positional path is rejected." That line connects the three files
covered so far into a single chain: `global.json` selects the runner,
the runner changes what `dotnet test` accepts on its command line, and
`verify.sh` is written to match. Both scripts exit cleanly with a message
rather than an error when `examples/` has no project files yet, which keeps
them safe to run from a freshly cloned repository before any example has
landed.

Run either script from the repository root, after `check-prereqs.sh` has
passed:

```sh
./scripts/build.sh
./scripts/verify.sh
```

Together they are the loop this book exercises on every change: check the
toolchain once, build every example, run every test suite, and trust a clean
result the same way CI does, because CI runs the identical scripts on the
identical pinned toolchain.

## Standalone builds: one example at a time

Every example is also a complete, independently buildable unit. You do not
need the repository-level scripts to work on a single pattern:

```sh
cd examples/10-testing-with-xunit-and-nsubstitute
dotnet test
```

This command restores, builds, and tests exactly the one example you are
in, using the same `Directory.Build.props` settings and the same
`global.json` test runner as a whole-repository run, because both files are
found by walking up from the current directory regardless of where `dotnet`
is invoked from. A bare `dotnet test` works here, without `--project`,
because you are already inside the single project's directory and there is
only one test project for Microsoft.Testing.Platform to find; `--project`
only becomes necessary once a script is driving several projects from the
repository root.

This standalone path is also the one described in each example's own
`README.md`. Copying a single `examples/<NN-pattern>` directory out of the
repository and continuing to iterate on it in isolation works, because
nothing about one example depends on state left behind by another, aside
from its own `Directory.Build.props` chain: carry that file along (or
replace it with equivalent properties in the copied `.csproj`) if you move
an example outside this repository entirely.

## Editors and the dotnet CLI

This book assumes two editors, and nothing else. VS Code with the C# Dev
Kit extension adds a language service, test explorer, and debugger over the
same `dotnet` CLI commands this appendix already covers. JetBrains Rider is
a full .NET IDE that runs natively on Linux and provides the same
capabilities inside a single application. Either one is a convenience layer
over the CLI, not a replacement for it: every command this book's chapters
and examples show is a plain `dotnet` invocation that a contributor with
only a terminal and a text editor can run by hand, and that is the path this
book's own CI and verification scripts follow.

There is no Visual Studio path in this book, because Visual Studio proper
does not run on Linux, which is the reference platform throughout. There are
also no containers anywhere in the build or test path: no Docker, no
Kubernetes, nothing that sits between a contributor and the `dotnet` CLI
restoring, building, and testing a project directly against the local SDK.
CI installs the same SDK directly on its runner rather than building inside
an image, so the path a contributor follows locally is the same path that
gates a pull request, not a parallel one that happens to usually agree with
it.

## For deeper coverage

- [.NET SDK overview](https://learn.microsoft.com/en-us/dotnet/core/sdk) from
  Microsoft Learn covers what the SDK installs and how its components, the
  CLI, the runtime, and the `dotnet` driver, fit together.
- [.NET CLI overview](https://learn.microsoft.com/en-us/dotnet/core/tools/)
  documents every `dotnet` subcommand this book's scripts and examples use,
  including `build`, `test`, `restore`, and `sln`.
- [Customize your build with Directory.Build.props and Directory.Build.targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory)
  explains the upward-search import behavior behind
  `examples/Directory.Build.props`.
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
  covers the test host this repository's `global.json` selects, including the
  `dotnet test --project` requirement `scripts/verify.sh` works around.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
