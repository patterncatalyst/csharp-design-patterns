# Examples

Runnable companions to the book. One self-contained demo per hands-on chapter,
each proving the chapter's pattern with a test rather than just compiling.

## Layout

Each demo lives at `examples/NN-pattern/`, matching the chapter number:

```
examples/
  11-strategy/
    <Project>.csproj
    <Project>.Tests.csproj     # or a <Project>.Tests/ directory
    src/ ...
    README.md                  # rendered on the site; carries behavioural verification
```

## Build and test

With the pinned toolchain from the prerequisites chapter (the .NET 10 SDK; run
`scripts/check-prereqs.sh` first):

```sh
cd examples/11-strategy
dotnet test
```

Every demo is standalone-testable from its own directory. From the repo root,
`./scripts/build.sh` builds every demo and `./scripts/verify.sh` runs every
suite.

## Rules

- **Shipping domain only.** Consignments, carriers, rate quotes, routing,
  tracking, documentation, billing. No `Foo`/`Bar`, no shapes, no animals.
- **No containers, no UI.** Plain `dotnet` on the host. No Docker, no
  Kubernetes, no Blazor or other front end.
- **Behavioural verification.** Each README states what the test proves, not
  just that the build exits zero.
- **Nullable reference types on, warnings as errors.** Shared through
  `Directory.Build.props`.
