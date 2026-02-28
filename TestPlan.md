# Blueprints Test Plan

Status: Working Draft
Date: 2026-02-28
Scope: Current `develop` branch versus `Plan.md` and `ImplementationPlan.md`

## Purpose

This document turns the product specification into a practical verification plan for the current application state.

It is not only a QA checklist. It also records what is already implemented, what is only partially implemented, and what is still missing.

## Current Baseline

The current app and codebase already provide:

- Windows-first local identity creation and loading
- DPAPI-backed private key protection
- signed project, membership, version, and item persistence
- trust-state evaluation during workspace load
- shared-folder sync manifest and local sync state
- baseline-aware push and pull logic
- incoming signature validation before pull apply
- an Avalonia shell that shows real local workspace and sync status

The current app does not yet provide the full v1.0 user workflow described in the product plan.

## Test Objectives

The verification pass should answer these questions:

1. Does the current app preserve the trust and signature guarantees already implemented?
2. Does the current UI accurately reflect the real state of the local workspace and sync layer?
3. Which planned v1.0 flows are fully implemented, partially implemented, or not implemented?
4. Which missing areas need to become tracked product issues?

## How To Run

From the repository root:

```powershell
dotnet build Blueprints.sln
dotnet test .\Blueprints.Tests\Blueprints.Tests.csproj
dotnet run --project .\Blueprints.App\Blueprints.App.csproj
```

## Status Legend

- `Implemented`: code path exists and is covered well enough to exercise meaningfully now
- `Partial`: some of the foundation exists, but the end-user workflow is incomplete
- `Missing`: not meaningfully testable through the app yet

## Verification Matrix

| Area | Planned Behavior | Current Status | Notes |
| --- | --- | --- | --- |
| Identity bootstrap | User identity exists locally with protected private key storage | Implemented | Backed by DPAPI and covered by tests |
| Signed local workspace | Project, members, version, and item files are signed and validated on load | Implemented | Trust state is surfaced in the app shell |
| Local workspace bootstrap | First-run local project/workspace can be created automatically | Implemented | Current shell creates a starter workspace rather than showing a creation flow |
| Shared sync manifest | Shared folder keeps signed manifest state for exchange | Implemented | Manifest exists in collaboration layer and has tests |
| Push foundation | Local changes can be staged and copied to shared sync root | Implemented | Service-level implementation exists; no user command yet |
| Pull foundation | Incoming shared changes can be imported into local workspace | Implemented | Service-level implementation exists; no user command yet |
| Incoming signature validation | Pull must reject tampered incoming content before apply | Implemented | Covered by tests |
| Sync status display | App shell shows real sync status from live workspace/session state | Implemented | Current shell reflects outgoing/incoming/conflict counts indirectly through summary |
| Open project workflow | User can choose/open existing project from UI | Missing | Current app bootstraps a default workspace automatically |
| Create project workflow | User can create a new project from UI | Missing | No create-project screen or flow yet |
| Project overview workflow | User can meaningfully manage versions/items from the shell | Partial | Overview renders live data, but management actions are not wired |
| Add version | User can create versions from UI | Missing | Button and view structure are not connected to domain commands |
| Add/edit item | User can create and edit release items from UI | Missing | No editor workflow yet |
| Release workflow | User can freeze/release a version and enforce immutability | Missing | Domain intent exists, but no command/UI flow yet |
| Changelog export | User can export Markdown changelog from real data | Missing | Planned but not implemented |
| Membership invitation | Admin can invite and manage members | Missing | Signed membership file exists, but no invitation UX or workflow exists |
| Conflict handling | App can present conflict UI and allow explicit resolution | Missing | Conflict detection exists in sync logic, but there is no conflict resolution UI/workflow |
| Shared-folder safety checks | App warns when shared folder permissions are too broad | Missing | Planned Windows ACL checks are not implemented |
| Audit log | App records and verifies append-only tamper-evident change history | Missing | Planned in spec, not implemented in current code |
| Read-only untrusted mode | App should degrade safely when trust is broken | Partial | Trust state exists, but there is no dedicated read-only mode UX |

## Manual Test Pass For Current Build

These are the useful manual checks that can be performed now:

### 1. Startup and identity

- Launch the app
- Confirm the shell opens on Windows
- Confirm a local identity is displayed
- Confirm a local workspace is created under local app data

Expected result:

- app loads without setup prompts
- identity display is populated
- trust badge shows a valid state for the starter workspace

### 2. Local workspace rendering

- Confirm project name and project code render
- Confirm versions render from persisted workspace data
- Confirm member count and membership revision render

Expected result:

- shell is backed by persisted workspace state, not hardcoded sample data

### 3. Sync state rendering

- Confirm local workspace path and shared sync path are visible
- Confirm sync summary changes depending on whether shared folder content exists

Expected result:

- sync status reflects actual analyzed state, not a fixed placeholder string

### 4. Tamper resistance

- Modify a signed file manually
- Reload through the storage/services path or rerun tests

Expected result:

- trust becomes untrusted or corrupt
- tampered incoming sync content is rejected before apply

### 5. Push/pull service behavior

- Use the existing tests as the executable verification path for push/pull behavior

Expected result:

- manifest creation works
- local sync state updates
- incoming invalid signatures are blocked
- overlapping local/shared changes surface as conflicts

## Recommended Next Test Gates

The next full verification gates should happen after these missing product slices are built:

1. project creation and open flows
2. version and item editing flows
3. changelog export and release workflow
4. membership invitation and management
5. conflict resolution UI
6. shared-folder safety checks
7. audit log implementation

## Readiness Summary

Current readiness level:

- Core trust and persistence foundation: usable
- Shared sync foundation: usable at service level
- Desktop shell: usable as a live status shell
- End-user workflow completeness for v1.0: not yet complete

The app is ready for engineering validation of the foundation, but not yet for a true end-user acceptance pass against the full v1.0 product definition.
