#!/usr/bin/env sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

printf '%s\n' '# Blueprints Handoff Context'
printf '%s\n\n' ''

for file in \
  AgentQuickstart.md \
  CodexHandoff.md \
  ProductDirection.md \
  Roadmap.md \
  IntegrationsStrategy.md \
  VaultSyncContext.md \
  TestPlan.md \
  README.md
do
  printf '%s\n' "===== ${file} ====="
  sed -n '1,260p' "${repo_root}/${file}"
  printf '\n'
done
