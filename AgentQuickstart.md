# Agent Quickstart

Purpose: let a future Codex/chat instance become useful quickly without rediscovering the repo.

If you are starting cold, read these first, in order:

1. `AgentQuickstart.md`
2. `CodexHandoff.md`
3. `ProductDirection.md`
4. `Roadmap.md`
5. `IntegrationsStrategy.md`
6. `VaultSyncContext.md`
7. `TestPlan.md`
8. `README.md`

Then inspect only the files relevant to the next slice.

## One-Line Product Summary

Blueprints is a local-first release planning desktop app with signed file persistence, changelog export, and tamper-evident shared-folder collaboration. Windows remains the first packaged release target, but Linux development is supported.

Strategic expansion: Blueprints should also become source-control aware, with local Git, GitHub, GitLab, other provider, and VaultSync integrations. See `IntegrationsStrategy.md`.

VaultSync-specific context from `ATAC-Helicopter/VaultSync` is captured in `VaultSyncContext.md`.

## Current Technical Shape

Solution projects:

- `Blueprints.App`: Avalonia UI, view models, app composition, app-facing coordinator
- `Blueprints.Core`: domain models, enums, key formatting
- `Blueprints.Storage`: signed JSON persistence and workspace file layout
- `Blueprints.Security`: identity, keys, signatures, DPAPI protector
- `Blueprints.Collaboration`: sync, manifests, conflicts, audit, shared-folder safety
- `Blueprints.Tests`: unit/service tests

## Highest-Value Next Slice

Wire Git change intelligence into release/changelog views.

Why:

- app-level Push/Pull commands now exist
- service-level safety checks already block bad manifests, invalid signatures, bad audit logs, and overlapping changes
- the Sync tab now has path-level diagnostics, semantic field summaries, and raw local/shared previews
- the Trust tab now has structured workspace, audit-chain, shared-folder, and conflict diagnostic cards
- the Integrations tab now has provider-agnostic status cards for Local Git, GitHub, GitLab, and VaultSync
- Local Git repository detection now reads branch, remote URL, dirty state, and latest tag without cloud credentials
- recent commits since the latest tag now appear in the Local Git card with matched item keys
- the next product jump is using those changes inside version and changelog workflows

Likely files:

- `Blueprints.App/Models/Integration*.cs`
- `Blueprints.App/Services/IntegrationStatusService.cs`
- `Blueprints.App/ViewModels/MainWindowViewModel.cs`
- `Blueprints.App/Views/MainWindow.axaml`
- `Blueprints.Tests/IntegrationStatusServiceTests.cs`

Supporting files:

- `Blueprints.Collaboration/Services/FileSystemWorkspaceSyncService.cs`
- `Blueprints.Collaboration/Services/WorkspaceSyncAnalyzer.cs`
- `Blueprints.Collaboration/Services/FileSystemAuditLogService.cs`
- `Blueprints.Collaboration/Services/SharedFolderSafetyInspector.cs`

## Current Feature Inventory

Implemented:

- create/open project
- recent projects
- local identity
- signed workspace persistence
- trust state on load
- read-only lockout when trust is broken
- create/edit versions
- create/edit items
- release version
- export Markdown changelog
- invite/update members
- detect conflicts
- keep-local/accept-shared conflict actions
- shared sync manifest/state service foundation
- push/pull service foundation
- app-level push/pull commands in the Sync tab
- path-level sync diagnostics with semantic summaries and raw local/shared preview in the Sync tab
- trust/audit/shared-folder diagnostics in the Trust tab
- provider-agnostic integration status cards in the Integrations tab
- local Git repository path setting and read-only detection
- recent Git changes with item-key extraction in the Local Git card
- incoming signature validation
- audit log append/validation foundation
- shared-folder path-overlap and Windows ACL warning foundation
- manifest continuity check during pull

Not yet coherent enough:

- richer semantic conflict preview polish
- Git change intelligence in release/changelog workflows
- provider API adapters
- splitting the main shell into smaller view files
- polished release-item workflow

## Verification Commands

From repo root:

```sh
dotnet build Blueprints.sln
dotnet test Blueprints.Tests/Blueprints.Tests.csproj
```

Windows app run:

```sh
dotnet run --project Blueprints.App/Blueprints.App.csproj
```

Linux app run:

```sh
scripts/run-app.sh
```

Note: Avalonia currently needs an X11/XWayland `DISPLAY` for this app. In a Wayland session, make sure XWayland is installed/enabled and that the launching shell exports `DISPLAY`.

For display debugging:

```sh
scripts/diagnose-linux-display.sh
```

Latest known local verification from this handoff:

- build succeeded
- tests passed: 45

## Important Environment Note

The product app uses Windows DPAPI for private key protection on Windows and local AES-GCM protection on Linux/macOS.

Tests should not require Windows DPAPI unless the test is specifically validating DPAPI. Coordinator tests currently use a test-only `IPrivateKeyProtector` where focused isolation is useful.

## Current Dirty-Tree Awareness

At the time these docs were refreshed, the worktree included active product edits for audit/safety plus pre-existing user-local files:

- `Blueprints.App/Views/MainWindow.axaml` was already modified before this documentation pass
- `.vscode/` was untracked
- `CodexHandoff.md` was untracked/open in the IDE

Do not revert user changes unless explicitly asked.

## Private Repository Workflow

If GitHub work is requested:

- use the GitHub plugin/skills when available
- work from `develop`
- use `feature/<slug>` or `chore/<slug>` for product slices
- commit intentionally
- push only when the private remote is ready
- merge/rebase into `develop` after verification
- update internal planning docs

Do not perform destructive git commands.

## Codebase Cautions

- Trust and signature behavior is product-critical.
- Membership changes are security-sensitive.
- Released versions must remain immutable.
- Shared folder is an exchange source, not a live editing workspace.
- External unsigned edits must not become trusted history.
- Audit log continuity should fail closed.

## Preferred Agent Behavior

The user wants direct progress, not repeated permission checks.

Good pattern:

1. read handoff docs
2. inspect the narrow files for the next slice
3. implement
4. add/update tests
5. run build/tests
6. summarize what changed and what remains

Keep commentary short while working. Keep final answers concise but explicit.
