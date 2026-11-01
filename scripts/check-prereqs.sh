#!/bin/sh
# Usage: scripts/check-prereqs.sh
#
# Verify the local toolchain this book's examples build with, and print the
# version found for each tool. No containers: everything here runs on the
# host (Fedora is the reference platform; see _docs/00-prerequisites.md).
#
# Pinned/floor versions, from the r01 build plan's verified toolchain table:
#   .NET SDK   10 (verified against 10.0.113, the .NET 10 LTS line)
#   xUnit / NSubstitute — pinned per test project (no central package
#   management), not checked here.
#
# If the .NET 10 SDK isn't installed, see _docs/00-prerequisites.md for the
# Fedora install steps.

set -eu

status=0

have() {
    command -v "$1" >/dev/null 2>&1
}

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

echo "Checking local toolchain for Design Patterns with .NET / C#..."
echo
check_dotnet
echo

if [ "${status}" -eq 0 ]; then
    echo "All prerequisites found."
else
    echo "One or more prerequisites are missing or below the floor version. See _docs/00-prerequisites.md."
fi

exit "${status}"
