# Blueprints Test Plan

Status: Working Draft
Date: 2026-05-16
Scope: Current repository state versus `ProductDirection.md`, `Roadmap.md`, `Plan.md`, and `ImplementationPlan.md`

## Purpose

This document records what is testable now, what has coverage, and what still needs manual/product verification.

It should be updated whenever a major product slice lands.

## How To Run

From the repository root:

```sh
dotnet build Blueprints.sln
dotnet test Blueprints.Tests/Blueprints.Tests.csproj
dotnet run --project Blueprints.App/Blueprints.App.csproj
```

Linux app launch helper:

```sh
scripts/run-app.sh
```

Wayland note:

- the app can build/test on Linux
- Avalonia app launch currently requires X11/XWayland display availability
- `scripts/run-app.sh` reports missing `DISPLAY` clearly when running under Wayland
- `scripts/diagnose-linux-display.sh` reports session variables, X sockets, tools, and display probe status

Latest known verification:

- build succeeded
- tests passed: 45

## Current Baseline

The current app and codebase provide:

- Windows identity/key storage through DPAPI
- Linux/macOS development identity key storage through local AES-GCM protection
- cross-platform-friendly tests through local/test key protectors
- signed project, membership, version, item, manifest, and audit documents
- trust-state evaluation during workspace load
- read-only mutation lockout for untrusted/corrupt local workspaces
- project create/open workflows
- recent project tracking
- version and item management
- generated item keys
- release workflow and changelog export
- member invitation and update workflows
- shared-folder sync manifest and local sync state
- baseline-aware push and pull service logic
- app-level push and pull commands in the Sync tab
- path-level sync diagnostics with semantic summaries and raw local/shared conflict preview
- provider-agnostic integration status cards for Local Git, GitHub, GitLab, and VaultSync
- read-only Local Git detection for repository root, branch, remote URL, dirty state, and latest tag
- recent Git changes since latest tag with item-key extraction
- incoming signature validation before pull apply
- manifest continuity validation during pull
- basic conflict detection and keep-local/accept-shared resolution
- audit log append and validation foundation
- shared-folder path-overlap safety checks
- Windows ACL broad-write warning foundation
- Avalonia shell wired to live workspace state

Important current limitation:

- sync diagnostics include semantic summaries and raw local/shared previews; trust diagnostics include workspace, audit-chain, shared-folder, and conflict cards. Local Git change mapping now feeds changelog export and selected-version pre-export diagnostics, but release-readiness actions are not built yet.

## Status Legend

- `Implemented`: code path exists and has meaningful automated coverage
- `Partial`: foundation exists, but user workflow or diagnostics are incomplete
- `Missing`: not meaningfully implemented yet

## Verification Matrix

| Area | Planned Behavior | Current Status | Notes |
| --- | --- | --- | --- |
| Identity bootstrap | User identity exists locally with protected private key storage | Implemented | Windows uses DPAPI; Linux/macOS use local AES-GCM; DPAPI-specific tests stay Windows-specific |
| Signed local workspace | Project, members, versions, and items are signed and validated | Implemented | Covered by storage/coordinator tests |
| Project create/open | User can create and open real projects | Implemented | UI and coordinator exist |
| Recent projects | Recent project references can be reopened | Implemented | Stored by app service |
| Version workflow | User can create/edit versions | Implemented | Released versions block further edits |
| Item workflow | User can add/edit items | Implemented | Keys generated from project rules |
| Release workflow | User can release a version and enforce immutability | Implemented | Release timestamp set; future edits blocked |
| Changelog export | User can export Markdown from real version data | Implemented | Default excludes incomplete items |
| Membership invitation | Admin can invite/update members | Implemented | Last active admin protection exists |
| Read-only untrusted mode | Mutations are blocked when trust is broken | Implemented | Coordinator tests cover broken trust mutation |
| Shared sync manifest | Shared folder keeps signed manifest state | Implemented | Service-level tests cover manifest/state |
| Push foundation | Local changes can be copied to shared sync root | Implemented | Service and app-level coordinator/UI tests exist |
| Pull foundation | Shared changes can be copied to local workspace | Implemented | Service and app-level coordinator/UI tests exist |
| Incoming signature validation | Pull rejects tampered incoming content | Implemented | Covered by sync tests |
| Manifest continuity | Pull rejects shared content that no longer matches signed manifest | Implemented | Added in sync service |
| Conflict handling | App can detect and resolve conflicts explicitly | Partial | Keep-local/accept-shared exists with semantic summaries plus raw local/shared preview; richer diff polish still missing |
| Shared-folder safety checks | App evaluates unsafe shared-folder setup | Partial | Path overlap and Windows ACL warning foundation exists; Trust tab now surfaces findings |
| Audit log | App records and validates tamper-evident history | Partial | Signed hash-linked entries exist; Trust tab surfaces audit-chain validation |
| Sync status display | App shell shows real sync status | Implemented | Sync tab exposes Refresh, Push, Pull, pending counts, diagnostics, and raw previews |
| Trust diagnostics | User can understand audit/safety/trust failures | Implemented | Trust tab shows structured cards with recovery guidance |
| Navigation | App provides clear project sections | Partial | Initial tabbed shell exists; future pass should split views and polish spacing/empty states |
| Conflict preview | User can compare mine/theirs/result | Partial | Semantic summaries and raw JSON/text local/shared preview exist; table-style diff is still missing |
| Integration status | App shows source-control and VaultSync provider status | Partial | Provider-agnostic cards, read-only Local Git detection, recent commit/item-key mapping, Git-aware changelog export, and selected-version source diagnostics exist; provider APIs not wired yet |
| Packaging | User can install/run packaged Windows app | Missing | Future v1.0 readiness |

## Automated Test Coverage To Preserve

Core trust/security:

- canonical JSON serialization
- Ed25519 signing/verification
- DPAPI protector on Windows
- identity creation/loading
- signed document read/write
- workspace load/save trust state

Collaboration:

- sync manifest store
- sync state store
- sync analyzer
- push copies outgoing docs
- pull copies incoming docs
- pull blocks invalid signatures
- pull blocks conflicts
- pull blocks invalid audit chain

App coordinator:

- create project
- open project
- save version
- save item
- release version
- export changelog
- invite/update member
- block mutation when trust is broken
- resolve conflict
- app-level push workspace
- app-level pull workspace
- write audit entry on project create
- block local/shared path overlap
- mark workspace corrupt when audit chain is broken
- expose structured audit/safety trust diagnostics
- integration status service provider spine
- read-only Local Git status mapping

## Manual Test Pass For Current Build

### 1. Project Setup

Steps:

- launch app on Windows
- create a new project
- close/reopen app
- open from recent project

Expected:

- identity is shown
- workspace paths are correct
- project state persists
- trust badge is trusted

### 2. Release Planning

Steps:

- create version
- add feature item
- add bug item
- edit item
- mark item done
- release version
- attempt another edit

Expected:

- item keys are generated
- counts update
- release timestamp is set
- released version is immutable

### 3. Changelog Export

Steps:

- export changelog from a version with done and incomplete items

Expected:

- Markdown file is written
- done items appear
- incomplete items are excluded by default
- item keys display

### 4. Membership

Steps:

- copy local identity bundle
- invite a second member
- change role/status
- attempt to remove/downgrade last active admin

Expected:

- membership revision increments
- last active admin removal is blocked

### 5. Trust Tampering

Steps:

- manually edit a signed JSON file
- reopen/refresh project
- try to mutate workspace

Expected:

- trust becomes untrusted/corrupt
- mutation is blocked with read-only messaging

### 6. Audit Tampering

Steps:

- create project
- create version to add a second audit entry
- delete the first audit entry
- reopen project

Expected:

- workspace trust becomes corrupt
- audit log failure is explained

### 7. Shared Folder Safety

Steps:

- attempt to create a project with shared sync folder inside local workspace

Expected:

- creation is blocked

Windows-only follow-up:

- configure a shared folder writable by broad principals such as Everyone or Authenticated Users

Expected:

- safety warning is produced once diagnostics UI exposes it

### 8. Sync Foundation

Use automated tests as the primary verification until app-level push/pull is exposed.

Expected:

- push/pull service tests pass
- invalid signatures are rejected
- manifest/audit continuity failures are rejected
- conflicts are detected

## Next Test Gates

After app-level push/pull:

- coordinator push/pull tests
- manual two-workspace push/pull pass
- pull invalid signature manual pass
- pull invalid audit manual pass

After navigation refactor:

- manual UI pass across setup, releases, team, sync, trust
- verify no commands are lost

After trust diagnostics:

- manual audit and shared-folder warning pass

After packaging:

- clean Windows machine install/run pass

## Readiness Summary

Current readiness:

- core trust and persistence: strong
- release planning foundation: usable
- changelog export: usable
- membership foundation: usable
- shared sync foundation: service-level usable
- app-level collaboration workflow: not yet complete
- UI/product clarity: needs focused refactor

Best next verification investment:

- expose push/pull in app layer, then run a two-workspace manual collaboration test.
