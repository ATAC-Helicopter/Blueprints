#!/usr/bin/env sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
cd "${repo_root}"

dotnet build Blueprints.sln
dotnet test Blueprints.Tests/Blueprints.Tests.csproj
