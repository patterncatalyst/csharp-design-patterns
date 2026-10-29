#!/bin/sh
# Usage: scripts/verify.sh
#
# Run the xUnit suite for every example under examples/ with the local .NET 10
# SDK (no containers). Each example is also standalone-testable from its own
# directory with `dotnet test`. Until the first example lands, there is nothing
# to test and this script exits cleanly.

set -eu

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
examples_dir="${repo_root}/examples"

if ! ls "${examples_dir}"/*/*.csproj >/dev/null 2>&1; then
    echo "No example projects yet under examples/ — nothing to test."
    exit 0
fi

echo "==> dotnet test (every example)"
for proj in "${examples_dir}"/*/*Tests*.csproj "${examples_dir}"/*/*.Tests/*.csproj; do
    [ -f "${proj}" ] || continue
    echo "--- ${proj}"
    # .NET 10's Microsoft.Testing.Platform mode of `dotnet test` (opted into via
    # the repo-root global.json) requires the project path via --project; a bare
    # positional path is rejected.
    dotnet test --project "${proj}"
done
