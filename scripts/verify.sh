#!/usr/bin/env sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
cd "${repo_root}"

dotnet restore Blueprints.sln
dotnet build Blueprints.sln --configuration Release --no-restore
dotnet test Blueprints.Tests/Blueprints.Tests.csproj \
  --configuration Release \
  --no-build
