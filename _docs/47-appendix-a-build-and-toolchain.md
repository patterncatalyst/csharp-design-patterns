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

- Find the one file that sets the target framework, language version,
  nullable reference types and warnings-as-errors for every example.
- Say what `global.json` configures (which runner `dotnet test` uses) and what it doesn't
  (an SDK pin).
- Run `scripts/check-prereqs.sh` and read its result.
- Build and test the whole repository with `scripts/build.sh` and
  `scripts/verify.sh`, and explain why `verify.sh` passes `--project`.
- Build and test one example on its own.

## Why this matters

Every example builds and tests the same way: the local `dotnet` CLI, no IDE
wizard, no container. Three small files make that work: a shared MSBuild
properties file, a `global.json` that selects the test runner, and three POSIX
shell scripts. Come here when a build misbehaves, when you need to know which
compiler flags are on, or when you add an example and want it to inherit the
same settings.

## Platform

| Item | Version |
| --- | --- |
| .NET SDK | 10.0.113 (floor: any 10.x) |
| Target framework | `net10.0`, the .NET 10 LTS release, supported to November 2028 |
| C# | 14 (`LangVersion` 14.0) |
| Test runner | Microsoft.Testing.Platform, selected in `global.json` |
| Test packages | xUnit v3 and NSubstitute, pinned per test project |
| Editors | VS Code with C# Dev Kit, or JetBrains Rider |
| Reference OS | Fedora; no containers anywhere in the build or test path |

## Directory.Build.props: shared compiler and analyzer settings

MSBuild walks up the directory tree from each project file and imports the
first `Directory.Build.props` it finds, with no `<Import>` in the `.csproj`.
The file sits at `examples/Directory.Build.props`, one level below the
repository root:

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

Placing it under `examples/` scopes it to example code. At the root it would
also reach the Jekyll site tooling beside `_docs/` and `_data/`.

| Property | Effect |
| --- | --- |
| `TargetFramework` | Every example targets `net10.0`. |
| `LangVersion` | Pins the compiler to C# 14. |
| `Nullable` | `string?` versus `string` is a compiler-checked claim. |
| `ImplicitUsings` | Adds `System`, `System.Linq` and the other common `using` directives. |
| `TreatWarningsAsErrors` | Any compiler or analyzer warning fails the build. |
| `EnforceCodeStyleInBuild` | An `.editorconfig` style violation fails the build. |
| `AnalysisLevel` | `latest-recommended` turns on the current recommended Roslyn analyzer tier. |
| `Deterministic` | Two builds of the same source are byte-identical, so a CI failure reproduces locally. |

A test project's `.csproj` adds only its package references and the
`IsTestProject` flag. A new project under `examples/` gets the same compiler
and analyzer settings with nothing to copy and nothing to drift.

## global.json: the test runner, not the SDK

The repository root holds `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

It has one setting: the runner `dotnet test` uses. There's no `sdk` block, so
no SDK version or `rollForward` policy. `check-prereqs.sh` enforces the SDK
floor instead.

.NET 10 has two test execution paths: the legacy VSTest path, and
Microsoft.Testing.Platform, a leaner host that runs a test assembly as its own
executable instead of inside a separate runner process. The `xunit.v3`
package ships the MSBuild wiring for that host and generates the test
assembly's entry point at build time, so no test project carries a
Microsoft.Testing.Platform property of its own.

Under this runner, `dotnet test` takes a project through `--project` and
rejects a bare positional path.

## check-prereqs.sh: the SDK floor

`scripts/check-prereqs.sh` finds `dotnet` on `PATH` and checks that its major
version is at least 10:

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

It's a floor, not a pin: any `10.x` SDK passes. The verified version,
10.0.113, is a comment in the script and in the prerequisites, not an
equality check. Run it from the repository root:

```sh
./scripts/check-prereqs.sh
```

A pass prints the version found and exits zero. A missing or old SDK prints
the failed check and exits non-zero, so a later build failure never has to be
traced back to the toolchain by hand.

## build.sh and verify.sh: the whole-repository loop

`scripts/build.sh` builds every example in Release:

```sh
for proj in "${examples_dir}"/*/*.csproj; do
    echo "--- ${proj}"
    dotnet build -c Release "${proj}"
done
```

The glob `examples/*/*.csproj` matches each library project. Test projects
sit one level deeper in `*.Tests/`, so `verify.sh` builds them as it runs
them.

`scripts/verify.sh` runs every example's tests:

```sh
for proj in "${examples_dir}"/*/*Tests*.csproj "${examples_dir}"/*/*.Tests/*.csproj; do
    [ -f "${proj}" ] || continue
    echo "--- ${proj}"
    dotnet test --project "${proj}"
done
```

Its comment gives the reason for `--project`: ".NET 10's
Microsoft.Testing.Platform mode of `dotnet test` (opted into via the
repo-root `global.json`) requires the project path via `--project`; a bare
positional path is rejected." Both scripts print a message and exit cleanly
when `examples/` has no projects yet.

Run them from the repository root after `check-prereqs.sh` passes:

```sh
./scripts/build.sh
./scripts/verify.sh
```

CI runs the same scripts on the same toolchain, so a clean local run predicts
a clean CI run.

## Standalone builds: one example at a time

Each example builds on its own:

```sh
cd examples/10-testing-with-xunit-and-nsubstitute
dotnet test
```

That restores, builds and tests the one example, with the same
`Directory.Build.props` and the same test runner, because MSBuild and the SDK
find both by walking up from the current directory. A bare `dotnet test`
works here because the directory holds one test project; `--project` matters
only when a script drives several projects from the root.

Each example's `README.md` describes this path. To move an example out of the
repository, carry `Directory.Build.props` with it or copy its properties into
the `.csproj`.

## Editors and the dotnet CLI

| Editor | What it adds |
| --- | --- |
| VS Code with C# Dev Kit | Language service, test explorer and debugger over the `dotnet` CLI |
| JetBrains Rider | A full .NET IDE that runs natively on Linux |

Both sit on top of the CLI. Every command in the examples is a plain `dotnet`
invocation you can run from a terminal, and CI and the verification scripts
use that path. Visual Studio isn't covered because it doesn't run on Linux,
the reference platform. Nothing in the build or test path uses Docker or
Kubernetes: CI installs the SDK directly on its runner, so the local path and
the path that gates a pull request are the same.

## For deeper coverage

- [.NET SDK overview](https://learn.microsoft.com/en-us/dotnet/core/sdk) from
  Microsoft Learn covers what the SDK installs and how its components, the
  CLI, the runtime, and the `dotnet` driver, fit together.
- [.NET CLI overview](https://learn.microsoft.com/en-us/dotnet/core/tools/)
  documents every `dotnet` subcommand the scripts and examples use,
  including `build`, `test`, `restore`, and `sln`.
- [Customize your build with Directory.Build.props and Directory.Build.targets](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory)
  explains the upward-search import behavior behind
  `examples/Directory.Build.props`.
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
  covers the test host the repository's `global.json` selects, including the
  `dotnet test --project` requirement `scripts/verify.sh` works around.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
