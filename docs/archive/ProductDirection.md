# Archived: Blueprints Product Direction

> Historical design material. Current product direction is maintained in [the roadmap](../../Roadmap.md).

Status: Working Direction
Date: 2026-05-16

## North Star

Blueprints is a local-first release planning desktop app for small developer teams.

It helps a team plan versions, collect release notes, generate changelogs, and collaborate through shared folders without trusting unsigned or unexplained file changes.

The short version:

> Plan releases. Produce changelogs. Trust the files.

Blueprints should not become a generic task board. Its center of gravity is release planning, with signed local-first collaboration as the differentiator.

## Product Identity

Blueprints is:

- release-first, not sprint-first
- local-first, not cloud-first
- file-based, not server-dependent
- source-control aware, not source-control replacing
- structured, not generic
- small-team oriented, not enterprise workflow software
- tamper-evident, not merely synchronized

Blueprints is not:

- Jira
- Trello
- GitHub Projects
- a real-time collaborative editor
- a general document database
- a cloud SaaS dashboard

## Target Users

Primary users:

- solo developers who want cleaner release notes
- indie game/application developers
- small software maintainers
- small internal teams of 2 to 5 developers

These users likely care about:

- working offline
- keeping project data in files they control
- avoiding a hosted service for every small project
- seeing exactly who changed release-critical information
- producing changelogs without assembling them manually

## Core Product Promise

For a solo developer:

- create a project
- create versions
- add categorized release items
- mark items done
- release a version
- export a changelog

For a small team:

- invite members
- exchange signed project updates through a shared folder
- detect tampering and unsafe shared folder setup
- resolve conflicts intentionally
- preserve auditable change history

## First-Class Concepts

### Project

A project owns:

- project name and code
- versioning scheme
- changelog rules
- item key rules
- members
- shared sync configuration
- signed project identity

### Version

A version is the main planning unit.

Statuses:

- Planned
- In Progress
- Frozen
- Released

Released versions are immutable. Frozen versions block normal edits unless a future explicit admin override flow signs and records the override.

### Item

An item is a changelog/release entry, not a generic task.

Each item has:

- stable internal ID
- optional/generated human-readable key
- version
- category
- title
- description
- done state
- author attribution

### Member

Members define who is trusted to sign project state.

Membership is security-sensitive:

- invitation-only
- signed
- revisioned
- never silently auto-merged

### Trust

Trust is a core UI state, not an implementation detail.

The app should always make clear whether the current workspace is:

- Trusted
- Untrusted
- Corrupt
- Read-only because safety cannot be guaranteed

### Sync

Sync is an explicit exchange operation.

Users work in a local workspace. The shared folder is an exchange source of truth, not the live editing folder.

### Integrations

Blueprints should integrate heavily with developer source-control workflows without surrendering project trust to remote platforms.

First-class integration targets:

- local Git repositories
- GitHub
- GitLab
- other major Git/source-control providers
- VaultSync

Integration direction:

- link versions and items to commits, branches, tags, pull requests, merge requests, and issues
- enrich changelogs with source-control links
- create draft provider releases from Blueprints changelogs
- use VaultSync as a trusted exchange target when available
- keep signed Blueprints state as the authority for release planning and audit history

Detailed strategy lives in `IntegrationsStrategy.md`.

VaultSync-specific context from the existing backup/sync app lives in `VaultSyncContext.md`.

## App Information Architecture

The app should eventually be organized around six surfaces.

### 1. Projects

Purpose:

- create project
- open project
- view recent projects
- choose local workspace and shared sync folder
- show project-level trust summary

Expected actions:

- Create Signed Project
- Open Existing Workspace
- Open Recent
- Return to Project Setup

### 2. Releases

Purpose:

- manage versions
- manage items inside selected version
- release versions
- export changelog

Expected layout:

- left: version list with status badges
- center/right: selected version detail
- item list grouped by changelog category
- inline item editor

Expected actions:

- New Version
- Save Version
- Add Item
- Save Item
- Release Version
- Export Changelog

### 3. Team

Purpose:

- show local identity bundle
- invite members
- update member roles/status
- warn about single-admin risk

Expected actions:

- Copy Identity Bundle
- Invite Member
- Save Member

Future actions:

- Rotate Key
- Transfer Admin
- Recommend Second Admin

### 4. Sync

Purpose:

- show outgoing, incoming, and conflict counts
- push local changes
- pull shared changes
- resolve conflicts
- inspect manifest state

Expected actions:

- Refresh Analysis
- Push
- Pull
- Keep Local
- Accept Shared
- future: Preview Diff, Merge Text

### 5. Trust And Audit

Purpose:

- explain why a workspace is trusted/untrusted/corrupt
- show audit log continuity
- show shared-folder safety warnings
- provide diagnostics before the user edits

Expected actions:

- Recheck Trust
- Recheck Shared Folder
- View Audit Entry
- future: Export Diagnostic Report

### 6. Integrations

Purpose:

- link a local Git repository
- connect GitHub, GitLab, or other provider projects
- connect VaultSync as a sync backend
- show branches, tags, commits, PRs/MRs, issues, and release links
- publish or draft releases from signed Blueprints changelogs

Expected actions:

- Link Repository
- Connect Provider
- Import Issue/PR/MR
- Link Item
- Draft Provider Release
- Connect VaultSync

## UX Direction

Blueprints should feel like a focused developer tool:

- dense but readable
- quiet, structured, and practical
- minimal marketing language inside the app
- no decorative hero once a project is open
- clear status badges for trust, sync, release status, and conflicts
- version numbers and item keys can use monospace

Suggested shell:

```text
-----------------------------------------------------
| Sidebar        | Top status bar                    |
| Projects       | Trust | Sync | Identity           |
| Releases       |-----------------------------------|
| Team           | Current view                      |
| Sync           |                                   |
| Trust          |                                   |
| Integrations   |                                   |
-----------------------------------------------------
```

The first screen after opening a project should be Releases, not a dashboard.

## Current Product State

Implemented in code:

- Avalonia desktop app on .NET 8
- project create/open flows
- recent projects
- local identity creation/loading
- DPAPI-backed key protection on Windows
- local AES-GCM key protection for Linux/macOS development runs
- signed project/member/version/item persistence
- trust-state evaluation on load
- read-only lockout for untrusted/corrupt workspace mutation
- version create/edit/release flows
- item create/edit flows
- generated item keys
- Markdown changelog export
- membership invite/update flows
- conflict detection and basic keep-local/accept-shared resolution
- shared sync manifest/state tracking
- service-level push/pull
- incoming signature validation before pull
- audit log append/validation foundation
- shared-folder path-overlap checks and Windows ACL warning foundation
- manifest continuity validation during pull

Important gap:

- service-level push/pull exists, but project-level commands and UI buttons are not yet exposed in the main app flow.

## Strategic Milestones

### v0.1 - Solo Release Planner

Goal:

Blueprints is pleasant and coherent for one developer managing releases locally.

Must have:

- clear project setup
- clear release/version workspace
- polished item editing
- category grouping
- changelog preview/export
- immutable release behavior
- trust badge and read-only behavior
- docs for local use

### v0.2 - Shared Folder Collaboration

Goal:

Two users can exchange signed updates through a shared folder without silent tampering.

Must have:

- project-level push/pull commands
- sync UI
- shared-folder safety surface
- audit status surface
- conflict resolution UI that explains what happened
- membership invitation flow usable by humans

### v0.3 - Product Polish

Goal:

Blueprints feels like an app instead of a technical prototype.

Must have:

- sidebar or tab navigation
- empty states
- better validation messages
- keyboard-friendly editing
- basic visual consistency pass
- sample project
- updated screenshots/docs

### v1.0 - Windows Small-Team Release

Goal:

A small Windows-first team can rely on Blueprints for release planning and changelog generation.

Must have:

- Windows manual test pass
- packaged build
- recovery limitations documented
- all trust/sync critical paths covered by tests
- no unsigned content accepted as trusted
- no silent membership privilege escalation
- no released version silently editable

Linux development support is explicitly in scope before v1.0 so the app can be built, tested, and run from the primary development environment. Windows remains the first packaged release target until cross-platform packaging is intentionally planned.

### v1.1 - Source-Control Aware Release Planning

Goal:

Blueprints understands the repository context around a release.

Must have:

- local Git repository linking
- branch/tag/commit detection
- item-key matching in commits
- changelog source links
- provider-agnostic integration model
- initial GitHub integration
- GitLab integration contract

### v1.2 - Provider Releases And VaultSync

Goal:

Blueprints can publish or draft release artifacts into major platforms and use VaultSync as a trusted exchange target.

Must have:

- draft GitHub release from changelog
- draft GitLab release from changelog
- VaultSync target configuration
- VaultSync health/status in Sync and Trust views
- generic provider extension points for other source-control platforms

## Product Decisions

Confirmed:

- local workspace plus shared sync folder
- no direct editing inside shared source of truth
- signed detached documents
- audit log entries are signed and hash-linked
- membership is invitation-only
- multiple admins supported
- no break-glass bypass in v1.0
- released versions are immutable
- item keys are user-facing, not identity
- changelog output is Markdown

Still to decide later:

- exact app navigation component: sidebar vs top tabs
- whether v0.1 should include installer packaging
- how much diff/merge UX belongs in v0.2 versus v1.1
- whether Linux/macOS safety checks are v1.0 or future
- exact VaultSync integration contract and metadata layout
- which source-control provider ships first after local Git awareness

## Design Principle For Future Work

When choosing what to build next, prefer work that makes one of these user sentences true:

- "I know what release I am working on."
- "I know what changed."
- "I know who changed it."
- "I know whether I can trust this workspace."
- "I can export the changelog now."
