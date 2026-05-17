# Blueprints Integrations Strategy

Status: Working Direction
Date: 2026-05-16

## Purpose

Blueprints should integrate deeply with the systems developers already use:

- Git repositories
- GitHub
- GitLab
- other major source-control platforms
- VaultSync

The goal is not to replace source control. The goal is to make release planning, changelog generation, audit history, and sync/trust state aware of source-control reality.

## Product Principle

Blueprints remains release-first.

Integrations should answer practical release questions:

- Which repository does this project map to?
- Which commits, branches, and tags belong to this version?
- Which pull requests or merge requests contributed to this release?
- Which Blueprints items are represented in source control?
- Can a changelog include links back to real development work?
- Can a release in Blueprints create or annotate a source-control release?
- Can VaultSync act as a trusted exchange backend instead of a plain folder?

## Integration Layers

### Layer 1 - Local Git Awareness

This layer should work without cloud credentials.

Capabilities:

- link a Blueprints project to a local Git repository
- detect current branch
- detect tags
- detect commits since previous release tag
- associate item keys in commit messages with Blueprints items
- warn when the working tree is dirty before release
- suggest version release notes from commits
- export changelog with commit/tag references

Candidate implementation:

- start with shelling out to `git` or using a .NET Git library if richer offline queries are needed
- store repository path in local settings, not signed project truth, unless a repository URL becomes shared project metadata

### Layer 2 - Provider-Agnostic Source-Control Model

Before provider-specific code, define common concepts:

- repository
- remote
- branch
- tag
- commit
- pull request / merge request
- issue
- release
- milestone
- pipeline/check status
- compare URL

Suggested abstractions:

- `ISourceControlProvider`
- `SourceRepositoryReference`
- `SourceBranchSummary`
- `SourceTagSummary`
- `SourceChangeSummary`
- `SourceReleaseDraft`
- `SourceControlLink`

Provider-specific integrations can then map GitHub, GitLab, and other platforms into the same app model.

### Layer 3 - GitHub Integration

Useful capabilities:

- link project to GitHub repository
- import issues and pull requests as Blueprints items
- link Blueprints item keys to GitHub issues and pull requests
- show pull request status for a version
- show CI/check status for a version
- create draft GitHub release from Blueprints changelog
- attach changelog output to a GitHub release
- optionally create/update milestone matching a version

Safety notes:

- writing to GitHub should require explicit user action
- never make remote issue/PR state the only source of Blueprints trust
- remote links can enrich changelogs but should not replace signed local project state

### Layer 4 - GitLab Integration

Useful capabilities:

- link project to GitLab project
- import issues and merge requests as Blueprints items
- link items to merge requests
- show pipeline status
- create GitLab release from Blueprints changelog
- optionally sync milestones

Implementation should reuse the provider-agnostic source-control model.

### Layer 5 - Other Source-Control Platforms

Targets to keep in mind:

- Bitbucket
- Azure DevOps
- self-hosted Git providers
- generic Git remotes

Strategy:

- make local Git awareness useful even when no hosted provider exists
- make provider adapters optional
- avoid hard-coding GitHub as the only source-control worldview

### Layer 6 - VaultSync Integration

VaultSync should be treated as a first-class trusted exchange target.

Context from `ATAC-Helicopter/VaultSync`:

- VaultSync is a cross-platform backup and sync app for project folders, NAS/network storage, and local drives.
- It has a CLI and Avalonia UI.
- It tracks projects, snapshots, backups, destinations, verification, restore readiness, and metadata sync.
- Portable metadata lives under `.vaultsync/meta/vaultsync.meta.db`.
- Metadata carries project identity, settings, snapshot summaries, backup history, tombstones, source machine data, and non-secret encryption descriptors.
- Metadata does not carry backup payload contents, plaintext passwords, secret material, full app configuration, or full destination definitions from other machines.

Potential capabilities:

- configure a Blueprints project to use a VaultSync workspace
- detect and summarize VaultSync metadata stores
- match Blueprints projects to VaultSync project external IDs
- use VaultSync destination identity, source-machine, backup, snapshot, and restore-readiness information in diagnostics
- surface VaultSync sync health inside Blueprints
- write Blueprints exchange packs to a VaultSync-managed destination
- preserve Blueprints signed document/audit guarantees even when VaultSync handles transport

Important boundary:

- VaultSync may transport and protect files, but Blueprints should still validate signatures, manifests, membership, and audit continuity locally.
- VaultSync metadata should enrich diagnostics, not become Blueprints project trust authority.
- More detailed context lives in `VaultSyncContext.md`.

## Data Ownership Rules

Signed Blueprints project truth:

- project metadata
- versions
- items
- members
- changelog rules
- item key rules
- audit entries

Local-only settings:

- local repository path
- local credential/cache state
- user-specific provider tokens
- preferred source-control account
- local VaultSync mount/path

Shared but not authority by itself:

- GitHub/GitLab issue links
- PR/MR links
- commit links
- release URLs
- provider IDs

Do not accept remote provider data as trusted Blueprints history unless a Blueprints member signs the resulting project change.

## UX Direction

Integrations should appear as a dedicated workspace area after the core app navigation exists.

Suggested sections:

- Releases
- Team
- Sync
- Trust
- Integrations

Integrations view should show:

- linked local Git repository
- linked remote provider
- current branch/tag status
- commits since previous release
- linked PRs/MRs/issues
- release publishing status
- VaultSync connection/sync health if configured

## Suggested Roadmap

### Phase A - Integration Planning

- create provider-agnostic models
- decide storage boundaries for signed/shared/local integration settings
- define GitHub/GitLab/VaultSync capability matrix

Status:

- initial provider-agnostic status spine exists in the app
- providers render as cards in the Integrations tab
- Local Git can be configured with a repository path and detected read-only
- Blueprints signatures, manifests, membership, and audit log remain the documented trust authority

### Phase B - Local Git Awareness

- link local repository path
- detect branch/tags
- detect commits since tag
- match item keys in commits
- enrich changelog preview with source links when available

Status:

- repository path linking exists as local integration settings
- branch, origin remote, dirty state, and latest tag detection exists
- commits since latest tag are read and shown in the Local Git card
- item keys are extracted from commit subjects
- changelog export consumes matched and unmatched recent commits when Local Git data is available
- selected-version release diagnostics for unmatched commits are next

### Phase C - GitHub Adapter

- connect repository
- list issues/PRs
- import/link selected issues/PRs to items
- create draft release from changelog

### Phase D - GitLab Adapter

- connect project
- list issues/MRs
- import/link selected issues/MRs to items
- create release from changelog

### Phase E - VaultSync Adapter

- configure VaultSync target
- detect `.vaultsync/meta/vaultsync.meta.db`
- read or request VaultSync status metadata
- match VaultSync project external IDs to Blueprints projects
- validate VaultSync layout/health
- use VaultSync as exchange root
- show VaultSync health in Sync/Trust views

## Suggested Issue Backlog

1. Define provider-agnostic source-control integration models
2. Add local Git repository linking
3. Detect branches, tags, and commits since release
4. Link commit messages to Blueprints item keys
5. Add Integrations view shell
6. Add GitHub repository linking
7. Import GitHub issues/PRs as Blueprints items
8. Create draft GitHub release from changelog
9. Add GitLab project linking
10. Import GitLab issues/MRs as Blueprints items
11. Create GitLab release from changelog
12. Define VaultSync integration contract
13. Add VaultSync target configuration
14. Show VaultSync sync/health diagnostics

## Non-Goals For Early Integration Work

- real-time sync with source-control providers
- replacing Git history
- two-way automatic issue synchronization without user approval
- silently publishing releases
- treating provider auth as Blueprints project trust
- making GitHub the only supported provider model

## Key Product Decision

Blueprints should integrate heavily with source-control platforms, but signed Blueprints state remains the project authority for release planning and trust.
