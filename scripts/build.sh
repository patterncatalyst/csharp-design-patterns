#!/bin/sh
# Usage: scripts/build.sh
#
# Build every example under examples/ with the local .NET 10 SDK (no
# containers). Run scripts/check-prereqs.sh first if you haven't verified the
# toolchain. Until the first example lands, there is nothing to build and this
# script exits cleanly.

set -eu

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
examples_dir="${repo_root}/examples"

if ! ls "${examples_dir}"/*/*.csproj >/dev/null 2>&1; then
    echo "No example projects yet under examples/ — nothing to build."
    exit 0
fi

echo "==> dotnet build (every example)"
for proj in "${examples_dir}"/*/*.csproj; do
    echo "--- ${proj}"
    dotnet build -c Release "${proj}"
done

echo "Build complete. Run scripts/verify.sh to run the test suite."
