# Archived: Codex Handoff

> Historical handoff material. For current project status, use [the roadmap](../../Roadmap.md) and [documentation index](../README.md).

This document is the primary handoff for future Codex/chat sessions. It should let another agent continue work with minimal rediscovery.

## 1. Fast Start

Read these files first:

1. `AgentQuickstart.md`
2. `ProductDirection.md`
3. `Roadmap.md`
4. `IntegrationsStrategy.md`
5. `VaultSyncContext.md`
6. `TestPlan.md`
7. `README.md`

One-line state:

> Blueprints is now a real signed local-first Avalonia release planner with version/item/release/changelog/member/conflict foundations, app-level push/pull, sync/trust diagnostics, read-only local Git status, Git-aware changelog export, and selected-version source diagnostics; the next highest-value slice is adding release-readiness actions around unmatched source changes.

## 2. Repository State At This Handoff

Repository:

- `ATAC-Helicopter/Blueprints`

Expected branch:

- `develop`

GitHub default branch:

- `develop`

Branch note:

- `main` is the shippable/stable branch.
- `develop` is the default branch and active development line.
- `main` and `develop` are currently aligned.

Important current work:

- Shared-folder safety and audit-log foundations have been implemented locally.
- Planning/handoff docs have been refreshed to preserve product direction.
- Main window has been refactored into a clearer shell with Releases, Team, Sync, Trust, and Integrations tabs.

Known worktree notes from this session:

- `Blueprints.App/Views/MainWindow.axaml` was already modified before the documentation refresh.
- `.vscode/` was untracked.
- `CodexHandoff.md` was untracked/open in the IDE before being rewritten.
- Do not revert user changes unless explicitly asked.

Latest verification run:

```sh
dotnet build Blueprints.sln
dotnet test Blueprints.Tests/Blueprints.Tests.csproj
```

Latest known result:

- build succeeded
- tests passed: 45

## 3. Product Direction

Blueprints should be treated as:

- a local-first release planning app
- a changelog generation tool
- a signed, tamper-evident shared-folder collaboration tool
- a source-control-aware release workflow tool

The product should not drift into a generic task board.

The product sentence:

> Plan releases. Produce changelogs. Trust the files.

Full direction lives in:

- `ProductDirection.md`
- `IntegrationsStrategy.md`
- `VaultSyncContext.md`

Execution roadmap lives in:

- `Roadmap.md`

## 4. What Already Exists

The app is no longer shell-only. Current code includes:

- Avalonia desktop app on `.NET 8`
- project create/open flows
- recent project tracking
- local identity creation/loading
- Windows DPAPI-backed private key protection on Windows
- local AES-GCM private key protection for Linux/macOS development runs
- test-only key protector in coordinator tests where focused isolation is useful
- signed filesystem persistence for `project`, `members`, `version`, and `item` documents
- trust-state evaluation during workspace load
- read-only lockout for untrusted/corrupt workspace mutation
- version create/edit workflow
- item create/edit workflow
- generated human-readable item keys
- release workflow and Markdown changelog export
- invitation-only membership management
- active/inactive member updates and last-admin protection
- shared-folder sync manifest/state tracking
- service-level push/pull
- incoming signature validation before pull apply
- manifest continuity validation during pull
- conflict detection and explicit keep-local/accept-shared actions
- audit log append/validation foundation
- shared-folder path-overlap safety check
- Windows ACL broad-write warning foundation

## 5. Main Remaining Product Gaps

Highest-value next gap:

- expose push/pull in the app coordinator, view model, and UI

Other major gaps:

- source-control/VaultSync integration architecture and Integrations view
- dedicated sync screen with operation summaries
- dedicated trust/audit/safety diagnostics screen
- conflict preview/diff rather than only path-level keep/accept
- release-item workflow polish and category grouping
- changelog export options
- Windows packaging and manual release testing

## 6. Important Files

Planning and handoff:

- `AgentQuickstart.md`
- `ProductDirection.md`
- `Roadmap.md`
- `IntegrationsStrategy.md`
- `VaultSyncContext.md`
- `TestPlan.md`
- `Plan.md`
- `ImplementationPlan.md`

Core app behavior:

- `Blueprints.App/ViewModels/MainWindowViewModel.cs`
- `Blueprints.App/Views/MainWindow.axaml`
- `Blueprints.App/Services/ProjectWorkspaceCoordinatorService.cs`
- `Blueprints.App/App.axaml.cs`

Sync/trust/audit:

- `Blueprints.Collaboration/Services/FileSystemWorkspaceSyncService.cs`
- `Blueprints.Collaboration/Services/WorkspaceSyncAnalyzer.cs`
- `Blueprints.Collaboration/Services/FileSystemSyncStateStore.cs`
- `Blueprints.Collaboration/Services/FileSystemSyncManifestStore.cs`
- `Blueprints.Collaboration/Services/FileSystemAuditLogService.cs`
- `Blueprints.Collaboration/Services/SharedFolderSafetyInspector.cs`

Storage:

- `Blueprints.Storage/Services/FileSystemProjectWorkspaceStore.cs`
- `Blueprints.Storage/Services/FileSystemSignedDocumentStore.cs`
- `Blueprints.Storage/Services/CanonicalJsonSerializer.cs`

Security:

- `Blueprints.Security/Services/IdentityService.cs`
- `Blueprints.Security/Services/FileSystemIdentityStore.cs`
- `Blueprints.Security/Services/DpapiPrivateKeyProtector.cs`
- `Blueprints.Security/Services/Ed25519SignatureService.cs`

Tests to expand first:

- `Blueprints.Tests/ProjectWorkspaceCoordinatorServiceTests.cs`
- `Blueprints.Tests/FileSystemWorkspaceSyncServiceTests.cs`

## 7. Recommended Next Slice

Add release-readiness actions around Git-aware version diagnostics.

Why:

- app-level push/pull now exists
- service-level safety checks already block conflicts, bad manifests, bad audit chains, and invalid signatures
- the Sync tab now lists diagnostic paths, semantic field summaries, and raw local/shared previews
- the Trust tab now lists workspace trust, audit-chain, shared-folder, and conflict diagnostic cards
- the Integrations tab now lists Local Git, GitHub, GitLab, and VaultSync provider status cards
- Local Git now detects configured repository root, branch, origin remote, dirty state, and latest tag without cloud credentials
- recent commits since the latest tag now appear in the Local Git card with matched item keys
- changelog export now includes matched and unmatched Local Git changes when available
- selected-version source diagnostics now show matched and unmatched Git changes before export
- the next useful release-planning layer is turning unmatched source changes into release-readiness actions

Recently completed:

1. `FileSystemWorkspaceSyncService` is wired into `ProjectWorkspaceCoordinatorService`.
2. Coordinator methods exist:
   - `PushWorkspace(...)`
   - `PullWorkspace(...)`
3. View-model commands exist:
   - `PushWorkspaceCommand`
   - `PullWorkspaceCommand`
4. Sync tab has Refresh, Push, Pull, and pending count controls.
5. Session refreshes after push/pull and writes result summaries to `WorkspaceMessage`.
6. `ProjectWorkspaceCoordinatorServiceTests` covers app-level push and pull.
7. Sync tab has a path-level diagnostics list and raw local/shared preview for selected diagnostic paths.
8. Sync preview includes semantic summaries for project, membership, version, item, and audit documents.
9. `LocalWorkspaceSession` carries structured audit validation and shared-folder safety reports.
10. Trust tab renders structured diagnostic cards with recovery guidance.
11. Integration spine exists:
   - `IntegrationProviderType`
   - `IntegrationConnectionState`
   - `IntegrationStatusCard`
   - `IntegrationStatusService`
12. `IntegrationStatusServiceTests` covers provider ordering, trust boundary, and VaultSync passive-awareness language.
13. Local Git detection exists:
   - local integration settings store
   - repository path setting in the Integrations tab
   - read-only `git` command inspector
   - clean/dirty repository state on the Local Git card
14. Integration tests now cover clean and dirty Local Git status mapping.
15. Local Git recent-change mapping exists:
   - `SourceChangeSummary`
   - commits since latest tag
   - item-key extraction from commit subjects
   - recent changes rendered in the Local Git card
16. Changelog export consumes Local Git changes:
   - matched commits appear under `Source Changes`
   - commits without selected-version item matches are listed as unmatched recent changes
   - the preview panel shows how many Local Git changes were considered
17. Selected-version source diagnostics exist:
   - `VersionSourceChangeDiagnostic`
   - `VersionSourceChangeDiagnosticBuilder`
   - version editor shows matched/unmatched Local Git changes before export
   - tests cover matching only completed selected-version items

Suggested implementation:

1. Add release-readiness actions for commits without Blueprints item keys.
2. Add a command to create a Blueprints item from an unmatched commit.
3. Add changelog export options for including or suppressing source changes.
4. Keep provider state read-only; no commits/tags/releases yet.

## 7.1 Strategic Integration Direction

Blueprints should heavily integrate with source-control platforms and VaultSync after the core sync/trust UI is usable.

Targets:

- local Git repositories
- GitHub
- GitLab
- other major Git/source-control platforms
- VaultSync

Principle:

- remote provider data can enrich release planning, changelogs, and publishing
- signed Blueprints state remains the authority for project truth
- provider writes should be explicit user actions
- VaultSync can become a first-class exchange target, but Blueprints must still validate signatures, manifests, membership, and audit continuity locally

Start with:

1. provider-agnostic source-control models
2. local Git repository linking
3. item-key matching from commits
4. Integrations view
5. GitHub/GitLab adapters
6. VaultSync target contract

VaultSync context has been pulled from `https://github.com/ATAC-Helicopter/VaultSync`.

Key VaultSync facts for integration:

- VaultSync tracks projects, snapshots, backups, destinations, metadata sync, restore readiness, and verification.
- Portable destination metadata is under `.vaultsync/meta/vaultsync.meta.db`.
- VaultSync metadata carries project identity/settings, snapshot summaries, backup history, tombstones, source machine info, and non-secret encryption descriptors.
- Blueprints should use VaultSync for transport/destination/health diagnostics, while retaining Blueprints signatures, manifests, membership, and audit continuity as the trust authority.

## 8. Build And Run

From repo root:

```sh
dotnet build Blueprints.sln
dotnet test Blueprints.Tests/Blueprints.Tests.csproj
dotnet run --project Blueprints.App/Blueprints.App.csproj
```

Platform note:

- key storage uses DPAPI on Windows and local AES-GCM protection on Linux/macOS
- most tests run cross-platform
- DPAPI-specific tests remain Windows-specific

## 9. Private Repository Workflow

Use this workflow unless there is a strong reason not to:

1. branch from `develop`
2. use `feature/<slug>` or `chore/<slug>` for product work
3. implement code first
4. run build/tests
5. commit intentionally
6. push only when the private remote is ready
7. merge/rebase into `develop` after verification
8. return local repo to clean `develop`
9. update internal planning docs

Protected branches:

- `develop`

Repository posture:

- private
- proprietary
- no public contribution, issue, or code-of-conduct workflow

## 10. Response Style Expected By User

The user prefers a direct "keep going" style.

Expected pattern:

- do the work without repeatedly asking permission
- make conservative technical calls
- keep the repo organized
- run tests/build
- keep progress updates short
- final summaries should mention:
  - what changed
  - what was verified
  - current repo state
  - next logical step

The user specifically values:

- repo cleanliness
- up-to-date handoff docs
- safety-first product decisions
- no hard-coded project-facing names where configuration should exist

## 11. Engineering Constraints

Continue these conventions:

- use `apply_patch` for manual edits
- prefer `rg` for search
- avoid destructive git commands
- do not revert unrelated user changes
- add focused tests for trust/sync/security behavior
- keep product-facing workflows practical

Security cautions:

- unsigned content must not become trusted
- external tampering must fail closed
- membership changes are security-sensitive
- released versions must remain immutable
- shared folder is an exchange location, not the live editing workspace
- audit log continuity should fail closed

## 12. One-Line Summary For Another Agent

Blueprints has a solid signed local-first release planning foundation; app-level push/pull, sync/trust diagnostics, provider-agnostic integration cards, read-only local Git change mapping, Git-aware changelog export, and selected-version source diagnostics are now in place, and the next best move is adding release-readiness actions before provider publishing.
