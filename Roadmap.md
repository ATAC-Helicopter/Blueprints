# Blueprints Roadmap

Status: Working Execution Plan
Date: 2026-05-16

This roadmap turns the product direction into implementation slices. It should be kept more current than the original high-level product spec.

## Current Baseline

The app is past foundation work. It has a real signed workspace model and a broad set of product flows.

Currently implemented:

- project create/open
- recent projects
- local identity
- signed project persistence
- trust evaluation
- versions and items
- release workflow
- changelog export
- member invitation/update
- conflict detection and simple resolution
- shared sync manifest/state foundation
- service-level push/pull
- incoming signature validation
- audit log foundation
- shared-folder safety foundation

Main remaining gap:

- the UI and product flow need to become coherent, navigable, and explicit about sync/trust/audit operations.

Strategic expansion:

- Blueprints should become source-control aware, with first-class local Git, GitHub, GitLab, other provider, and VaultSync integration. Details live in `IntegrationsStrategy.md`.
- VaultSync implementation context from the existing backup app lives in `VaultSyncContext.md`.

## Immediate Priorities

### Priority 1 - Preserve Context

Status: In Progress

Tasks:

- create `ProductDirection.md`
- create `Roadmap.md`
- create `AgentQuickstart.md`
- update `CodexHandoff.md`
- update `TestPlan.md`
- update `README.md`

Definition of done:

- a future agent can understand current state and next work in under five minutes
- docs mention the new audit/safety implementation
- docs no longer describe implemented app workflows as missing

### Priority 2 - Expose Push/Pull In App Layer

Status: Implemented

Problem:

`FileSystemWorkspaceSyncService` has `Push` and `Pull`, but the coordinator/view model do not expose obvious project-level push/pull commands.

Implemented:

- `FileSystemWorkspaceSyncService` is wired into app composition
- coordinator methods exist:
  - `PushWorkspace(...)`
  - `PullWorkspace(...)`
- view-model commands exist:
  - `PushWorkspaceCommand`
  - `PullWorkspaceCommand`
- Sync tab has Refresh, Push, Pull, and pending counts
- session refreshes after operation and result summaries appear in `WorkspaceMessage`
- push/pull stay blocked when trust is broken; pull still blocks invalid manifest/audit/signatures

Tests:

- coordinator push copies outgoing docs and refreshes sync summary
- coordinator pull imports incoming docs and refreshes sync summary
- service-level tests cover conflicts and invalid audit/manifest/signature protections

Next follow-up:

- use Git change intelligence in release/changelog workflows

### Priority 2.1 - Sync Diagnostics Panel

Status: Initial pass complete

Implemented:

- `SyncDiagnosticCard` app model
- Sync tab diagnostics list populated from current conflict analysis and blocked push/pull results
- raw local/shared preview for the selected diagnostic path
- semantic summaries for project, membership, version, item, and audit documents
- diagnostics refresh after push/pull and session refresh

Next follow-up:

- polish semantic summaries into a clearer field-level comparison table
- categorize manifest, audit, signature, and overlap failures with dedicated visuals and recovery actions

### Priority 2.2 - Trust Diagnostics Panel

Status: Initial pass complete

Implemented:

- `TrustDiagnosticCard` app model
- `LocalWorkspaceSession` carries audit validation and shared-folder safety reports
- Trust tab renders workspace trust, audit-chain, shared-folder, and conflict diagnostic cards
- recovery guidance is shown for corrupt audit chains, path overlap, missing folders, broad ACLs, unavailable ACL checks, and unresolved conflicts
- coordinator test coverage asserts structured audit diagnostics after audit-chain tampering

Next follow-up:

- add table-style trust details if cards become too dense
- include shared manifest/signature diagnostics when pull failure state is promoted into session-level trust state

### Priority 4 - Integration Spine

Status: Initial pass complete

Implemented:

- provider enum for Local Git, GitHub, GitLab, and VaultSync
- connection-state enum for NotConfigured, Connected, Warning, and Error
- `IntegrationStatusCard` app model
- `IntegrationStatusService` provider statuses
- Integrations tab renders provider cards with target, summary, guidance, checked time, and trust boundary
- VaultSync status explicitly starts with passive awareness of `.vaultsync/meta/vaultsync.meta.db`
- tests preserve provider ordering, not-configured defaults, Blueprints trust authority, and VaultSync transport/backup-health language
- local integration settings store persists configured repository path
- read-only Local Git inspector detects repository root, branch, origin remote, dirty state, and latest tag
- Integrations tab can save/refresh the Local Git repository path
- tests cover clean and dirty Local Git status mapping
- `SourceChangeSummary` captures commits since latest tag
- Local Git card renders recent commits and item-key matches

Next follow-up:

- surface matched/unmatched commits in version and changelog workflows
- add release diagnostics for commits without Blueprints item keys
- keep Git detection read-only until release publishing workflows are explicit

### Priority 2.5 - Linux Development Support

Status: In Progress

Problem:

Development is happening primarily from Linux, but the app was originally hard-gated to Windows and identity storage was wired directly to DPAPI.

Implementation outline:

- keep DPAPI private key protection on Windows
- add non-Windows local private key protection suitable for development
- remove app startup Windows hard-stop
- keep tests runnable on Linux
- document the different protection guarantees

Definition of done:

- `dotnet build Blueprints.sln` succeeds on Linux
- `dotnet test Blueprints.Tests/Blueprints.Tests.csproj` succeeds on Linux
- app composition no longer throws on non-Windows startup

### Priority 3 - Navigation Refactor

Status: Initial shell pass complete

Problem:

The current main window is capable but too dense. It mixes setup, overview, versions, members, conflicts, changelog, and trust in one large surface.

Implementation outline:

- introduce a selected workspace section:
  - Releases
  - Team
  - Sync
  - Trust
- keep Project Setup as separate mode
- make Releases the default after opening a project
- move members into Team
- move conflict actions into Sync
- move audit/safety explanation into Trust

Recommended first technical step:

- split `MainWindow.axaml` into smaller Avalonia user controls if local patterns support it:
  - `ProjectSetupView`
  - `ReleasesView`
  - `TeamView`
  - `SyncView`
  - `TrustView`

Tests:

- build
- view model command tests if practical
- manual launch screenshot/inspection

Definition of done:

- main window can be understood at a glance
- release planning is the primary workspace experience

Current status:

- setup mode is separate from active workspace mode
- active workspace now has tabbed sections for Releases, Team, Sync, Trust, and Integrations
- existing commands remain wired through the current view model
- future refinement can split the large XAML file into dedicated user controls

### Priority 4 - Trust And Audit UI

Status: Planned

Problem:

The app validates audit/safety state, but users need a clear surface explaining what happened.

Implementation outline:

- extend session model or create a diagnostics model for:
  - audit entry count
  - audit validation summary
  - shared-folder safety findings
  - manifest continuity status
- display these in Trust view
- show warning for broad shared-folder write access
- show corrupt/read-only state prominently

Tests:

- opening project with valid audit reports trusted summary
- opening project with deleted audit entry reports corrupt
- broad/shared unsafe path report appears in diagnostics

Definition of done:

- user can understand why editing is enabled or disabled

### Priority 5 - Release Workflow Polish

Status: Planned

Problem:

Release planning works, but the item/version editing experience still feels like a prototype.

Implementation outline:

- group items by category in the selected version
- display item keys consistently
- improve empty states
- improve version status badges
- preview changelog before export
- add export options later:
  - include incomplete
  - include descriptions
  - compact mode

Tests:

- changelog builder coverage for category ordering/options
- UI smoke/manual pass

Definition of done:

- solo developer can manage a release without needing to understand the file model

### Priority 6 - Conflict Review UX

Status: Planned

Problem:

Current conflict resolution can keep local or accept shared, but it does not show a useful diff or result preview.

Implementation outline:

- classify conflicts by document type
- show mine/theirs summaries
- for item/version text fields, allow manual combine later
- keep membership conflicts strict

Tests:

- membership conflicts cannot auto-merge
- same-document conflict is visible with stable path/entity naming

Definition of done:

- user can make an informed conflict decision

### Priority 7 - Packaging And Release Readiness

Status: Later

Implementation outline:

- choose Windows packaging path
- add sample project
- add screenshots
- update README for end users
- write recovery/security limitations
- create draft release workflow

Definition of done:

- someone can download/run/test Blueprints without reading the source tree

### Priority 8 - Integration Architecture

Status: Planned

Problem:

Blueprints should integrate heavily with source-control platforms and VaultSync, but the core signed release-planning model must stay provider-independent.

Implementation outline:

- define provider-agnostic source-control models
- define signed/shared/local storage boundaries for integration metadata
- add local Git repository linking before hosted provider writes
- add an Integrations view shell after navigation refactor
- use `VaultSyncContext.md` to design the VaultSync integration contract before writing transport-specific code

Tests:

- integration metadata does not weaken signed project trust
- local-only provider settings are not written into signed project truth
- item-key matching from commits is deterministic

Definition of done:

- the app has a clear source-control/VaultSync integration architecture that can support GitHub, GitLab, and other providers without hard-coding one platform into the domain model

## Milestones

### v0.1 - Solo Release Planner

Goal:

One developer can use Blueprints for real local release planning.

Scope:

- navigation refactor
- release/item polish
- changelog export polish
- trust badge and read-only UX
- docs refreshed

Exit criteria:

- create project
- create version
- add done/incomplete items
- release version
- export changelog
- reopen project
- trust state remains clear

### v0.2 - Shared Folder Collaboration

Goal:

Two users can exchange signed updates through a shared folder.

Scope:

- app-level push/pull
- sync screen
- audit/safety diagnostics
- conflict surface
- member invite UX cleanup

Exit criteria:

- user A pushes
- user B pulls
- tampered file is rejected
- deleted audit history is rejected
- overlapping edits produce conflict
- conflict can be resolved explicitly

### v0.3 - Product Polish

Goal:

Blueprints feels like a coherent app.

Scope:

- UI consistency
- empty states
- sample project
- docs/screenshots
- manual test script

Exit criteria:

- project can be demoed without explaining implementation internals first

### v1.0 - Windows Small-Team Release

Goal:

Small teams can trust Blueprints for release planning and changelog generation on Windows.

Scope:

- Windows packaging
- full manual test pass
- security/recovery docs
- stable schema posture
- GitHub release

Exit criteria:

- no unsigned trusted content
- no silent membership escalation
- no silent released-version mutation
- no direct shared-folder editing requirement
- changelog export works from real data

### v1.1 - Source-Control Aware Releases

Goal:

Blueprints understands the Git/source-control context around a release.

Scope:

- local Git repository linking
- branch/tag/commit detection
- item-key matching in commit messages
- changelog links to commits/tags
- provider-agnostic integration models
- GitHub adapter foundation
- GitLab adapter foundation or design-ready contract

Exit criteria:

- a release can show commits since the previous tag
- a changelog can include source-control links
- Blueprints items can link to commits, issues, PRs, and MRs

### v1.2 - Provider Publishing And VaultSync

Goal:

Blueprints can publish release artifacts to major platforms and use VaultSync as a first-class exchange target.

Scope:

- draft GitHub release from Blueprints changelog
- draft GitLab release from Blueprints changelog
- generic provider extension points
- VaultSync target configuration
- VaultSync health/status diagnostics

Exit criteria:

- user can create a provider release draft from signed Blueprints data
- user can configure VaultSync as a sync target without weakening Blueprints signature/audit validation

## Suggested Issue Backlog

Create these GitHub issues if the board needs to be rebuilt:

1. Expose push/pull workflows in app coordinator and UI
2. Add Sync view with manifest, conflict, and operation summaries
3. Add Trust view with audit and shared-folder diagnostics
4. Refactor main window into project setup plus workspace sections
5. Polish release/version/item editor workflow
6. Group selected-version items by changelog category
7. Add changelog export options
8. Improve conflict review with mine/theirs summaries
9. Add single-admin and recovery-risk warnings
10. Add sample project and end-user README walkthrough
11. Add Windows manual test script
12. Package Windows preview build
13. Define source-control integration architecture
14. Add local Git repository linking
15. Detect branches, tags, and commits since release
16. Link commits to Blueprints item keys
17. Add Integrations view
18. Add GitHub repository linking
19. Import/link GitHub issues and pull requests
20. Draft GitHub release from changelog
21. Add GitLab project linking
22. Import/link GitLab issues and merge requests
23. Draft GitLab release from changelog
24. Define VaultSync integration contract
25. Add passive VaultSync metadata/status reader
26. Configure VaultSync as a sync target
27. Show VaultSync health diagnostics

## Non-Goals Until After v1.0

- real-time collaboration
- cloud hosting
- web app
- enterprise permission hierarchy
- quorum/multi-signature approval
- break-glass trust bypass
- generalized task board features
- plugin system
- automatic two-way provider sync without explicit approval

## Engineering Rules For The Roadmap

- keep trust/safety decisions conservative
- do not make unsigned external edits trusted history
- tests should expand around every trust boundary
- prefer app-level slices that can be manually exercised
- update handoff docs after every major slice
