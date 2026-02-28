# Blueprints Implementation Plan

Execution Blueprint for v1.0

Status: Draft
Depends on: `Plan.md`

## 1. Implementation Goals

This document turns the product specification into a build-oriented plan for v1.0.

It defines:

- File and folder schema
- Sync model
- Signature and trust validation rules
- Conflict rules by entity type
- Membership invitation flow
- First-pass screens and user flows
- Suggested implementation phases

## 2. Core Architectural Decisions

- Windows-first in v1.0
- Local working copy per user
- Shared folder used as sync source of truth
- JSON file storage with detached signatures
- Every critical entity validated independently
- No direct editing inside the shared source of truth
- Released versions are immutable
- Membership changes are stricter than normal content changes
- Project-facing labels and naming are project-defined wherever safely possible

## 3. Project Storage Model

### 3.1 Local workspace vs shared sync folder

Each user has:

- A local workspace folder used by the app for editing
- A configured shared sync folder used for exchanging signed changes

Recommended local path examples:

```text
C:\Users\<user>\Documents\Blueprints\Workspaces\<ProjectId>\
```

Recommended shared path examples:

```text
\\NAS\Blueprints\VaultSync\
D:\TeamShare\Blueprints\VaultSync\
```

### 3.2 Project folder layout

Local workspace layout:

```text
<ProjectRoot>\
  project\
    project.json
    project.sig
    members.json
    members.sig
    settings.local.json
  versions\
    <VersionId>\
      version.json
      version.sig
      items\
        <ItemId>.json
        <ItemId>.sig
  log\
    <ChangeId>.json
    <ChangeId>.sig
  sync\
    state.json
    inbox\
    staging\
  cache\
    trust-index.json
```

Shared sync layout:

```text
<SharedProjectRoot>\
  manifest\
    sync-manifest.json
    sync-manifest.sig
  project\
    project.json
    project.sig
    members.json
    members.sig
  versions\
    <VersionId>\
      version.json
      version.sig
      items\
        <ItemId>.json
        <ItemId>.sig
  log\
    <ChangeId>.json
    <ChangeId>.sig
  packs\
    <SyncBatchId>\
```

Design intent:

- Local workspace contains app-private state such as sync cursors and cached validation results
- Shared folder contains only exchangeable project state and signed history
- `settings.local.json` is never shared or signed as project truth

### 3.3 Why both full entities and logs exist

The entity files are the current source of state.

The log files provide:

- Tamper evidence
- Change attribution
- Sync traceability
- Recovery diagnostics

The app reconstructs current state from entity files first, then uses the audit log to verify continuity and explain history.

## 4. Entity Schema

### 4.1 `project.json`

Purpose:

- Root project metadata
- Signed project-level configuration

Suggested fields:

```json
{
  "schemaVersion": 1,
  "projectId": "guid",
  "name": "VaultSync",
  "projectCode": "VS",
  "versioningScheme": "SemVer",
  "createdUtc": "2026-02-28T16:00:00Z",
  "defaultCategories": [
    { "id": "added", "label": "Added" },
    { "id": "changed", "label": "Changed" },
    { "id": "fixed", "label": "Fixed" },
    { "id": "removed", "label": "Removed" },
    { "id": "security", "label": "Security" }
  ],
  "itemTypes": {
    "feature": { "label": "Feature" },
    "bug": { "label": "Bug" },
    "issue": { "label": "Issue" },
    "security": { "label": "Security" }
  },
  "itemKeyRules": {
    "feature": { "prefix": "VS", "scope": "version" },
    "bug": { "prefix": "BUG", "scope": "project" },
    "issue": { "prefix": "ISS", "scope": "project" },
    "security": { "prefix": "SEC", "scope": "project" }
  },
  "changelogRules": {
    "includeIncompleteByDefault": false,
    "includeItemKeysByDefault": true,
    "includeDescriptionsByDefault": false,
    "compactModeByDefault": false
  }
}
```

### 4.2 `members.json`

Purpose:

- Membership truth
- Roles and public keys
- Revisioned security authority

Suggested fields:

```json
{
  "schemaVersion": 1,
  "projectId": "guid",
  "membershipRevision": 3,
  "members": [
    {
      "userId": "guid",
      "displayName": "Flavio",
      "publicKey": "base64",
      "role": "Admin",
      "joinedUtc": "2026-02-28T16:10:00Z",
      "isActive": true
    }
  ]
}
```

### 4.3 `version.json`

Suggested fields:

```json
{
  "schemaVersion": 1,
  "projectId": "guid",
  "versionId": "guid",
  "name": "1.5.0",
  "status": "InProgress",
  "createdUtc": "2026-02-28T16:20:00Z",
  "releasedUtc": null,
  "notes": null,
  "manualOrder": ["item-guid-1", "item-guid-2"]
}
```

### 4.4 `item.json`

Suggested fields:

```json
{
  "schemaVersion": 1,
  "projectId": "guid",
  "versionId": "guid",
  "itemId": "guid",
  "itemKey": "VS-1567",
  "itemKeyTypeId": "feature",
  "categoryId": "added",
  "title": "New dashboard",
  "description": "Optional text",
  "isDone": true,
  "tags": ["ui", "release-blocker"],
  "createdUtc": "2026-02-28T16:30:00Z",
  "updatedUtc": "2026-02-28T18:00:00Z",
  "lastModifiedByUserId": "guid",
  "lastModifiedByName": "Flavio"
}
```

### 4.5 `log/<ChangeId>.json`

Suggested fields:

```json
{
  "schemaVersion": 1,
  "changeId": "20260228T180001Z_user-guid_0001",
  "projectId": "guid",
  "entityType": "Item",
  "entityId": "guid",
  "changeType": "Update",
  "summary": "Marked VS-1567 as done",
  "timestampUtc": "2026-02-28T18:00:01Z",
  "authorUserId": "guid",
  "membershipRevisionSeen": 3,
  "prevHash": "hex"
}
```

## 5. Item Key Generation Rules

### 5.1 Stable rules

- `ItemId` is always the true internal identity
- `ItemKey` is generated from signed project configuration
- `ItemKey` uniqueness must be validated before commit
- Project-visible names, labels, prefixes, and category labels are configurable
- Stable internal IDs should back project-defined labels so renaming does not break history

### 5.2 Default generation

Feature key format:

```text
<ProjectCode>-<Major><Minor><Sequence>
```

Example:

- Project code: `VS`
- Version: `1.5.0`
- Sequence: `67`
- Result: `VS-1567`

Project-scoped key format:

```text
<Prefix>-<Sequence>
```

Examples:

- `BUG-1042`
- `ISS-215`
- `SEC-8`

These are defaults only. Projects may rename labels, prefixes, and visible terminology.

### 5.3 Sequence behavior

- Version-scoped counters reset per version
- Project-scoped counters never reset
- Counters increment only on successful item creation
- Deleted items do not free their keys for reuse

## 6. Signature and Trust Rules

### 6.1 Signed files

These files must always be signed:

- `project.json`
- `members.json`
- every `version.json`
- every `item.json`
- every log entry
- `sync-manifest.json`

### 6.2 Canonicalization

All signed JSON must use:

- Stable property ordering
- UTF-8 encoding
- Consistent newline handling
- No insignificant whitespace dependence

### 6.3 Validation points

The app validates signatures:

- On project open
- Before push
- During pull
- During merge
- Before writing resolved conflicts
- Before changelog generation if trust state is not already fresh

### 6.4 Trust outcomes

- `Trusted`: all required signatures validate and membership authority is consistent
- `Untrusted`: signatures exist but one or more checks fail, or author authority is invalid
- `Corrupt`: required files are missing, unreadable, malformed, or structurally inconsistent

## 7. Sync Model

### 7.1 Sync philosophy

Sync should operate on logical entities, not on blind folder replacement.

The shared folder acts as the source of truth for exchanged project state, but every incoming change must still be validated against:

- signature validity
- membership authority
- entity consistency
- audit chain continuity

### 7.2 Push flow

1. Validate local workspace state.
2. Gather changed entities since last successful push.
3. Generate missing signatures and log entries.
4. Write a staged sync batch locally.
5. Copy the batch into the shared folder.
6. Update shared manifest atomically.
7. Mark push complete locally.

### 7.3 Pull flow

1. Read the shared manifest.
2. Detect remote entities newer than local sync state.
3. Copy changed files into local `sync/inbox`.
4. Validate signatures and authority.
5. Merge safe changes automatically.
6. Raise conflicts for unsafe overlaps.
7. Update local sync state only after successful application.

### 7.4 Atomicity expectations

On Windows shared folders, partial writes are a real risk.

v1.0 should therefore:

- write new files under staging names first
- move them into final location only after complete write
- avoid in-place overwrite where possible
- use manifest updates as the final commit point for a sync batch

### 7.5 Local sync state

`sync/state.json` should track:

- last pulled manifest version
- last pushed manifest version
- known remote change IDs
- last successful trust validation timestamp
- unresolved conflicts

## 8. Conflict Rules by Entity Type

### 8.1 Project config

Auto-merge:

- non-overlapping config changes when safe and structurally valid

Conflict:

- edits to the same config field
- edits that would produce duplicate item key rules
- edits that change changelog/category ordering differently
- edits that rename the same visible label differently

### 8.2 Membership

Always strict:

- never auto-merge
- admin resolution required
- resolution creates a new signed membership revision

### 8.3 Versions

Auto-merge:

- note changes vs manual order changes
- status-independent metadata changes on non-released versions

Conflict:

- different status changes to the same version
- concurrent release/freeze transitions
- attempts to edit released versions

### 8.4 Items

Auto-merge:

- tag changes vs description changes
- completion state changes vs unrelated metadata changes
- edits to different items

Conflict:

- different titles for the same item
- different category changes for the same item
- different completion values changed concurrently
- different item key edits to the same item

### 8.5 Logs

Logs are append-only.

Conflict conditions:

- broken hash chain
- duplicate `ChangeId`
- missing predecessor when continuity is required

## 9. Membership Invitation Flow

### 9.1 Invite member

1. Admin opens project members screen.
2. Admin selects `Invite Member`.
3. Invitee shares public key bundle out of band.
4. Admin adds invitee with chosen role.
5. App increments `membershipRevision`.
6. App signs the new `members.json`.
7. Change is pushed to shared sync source.

### 9.2 Accept invite

1. Invitee opens local app identity.
2. Invitee opens the shared project invitation or imported project package.
3. App verifies that invitee public key matches a signed member entry.
4. App creates local workspace.
5. Initial pull downloads current trusted project state.

### 9.3 Role change or removal

1. Admin edits membership.
2. App requires explicit confirmation for demotion/removal.
3. App creates new membership revision and audit log entry.
4. Future pushes by removed users are rejected.

## 10. First-Pass Screens

### 10.1 App shell

- Workspace/project list
- Trust badge
- Sync status
- Current user identity

### 10.2 Create project screen

- Project name
- Project code
- Versioning scheme
- Project-defined categories
- Project-defined item types
- Item key rules
- Changelog defaults
- Shared folder configuration

### 10.3 Open project screen

- Recent local workspaces
- Import existing shared project
- Trust state summary
- Shared-folder safety warning

### 10.4 Project overview screen

- Version list
- Version status badges
- Release actions
- Changelog generation action
- Trust and sync summary

### 10.5 Version detail screen

- Version metadata
- Category sections
- Ordered item list
- Quick add item
- Reorder items
- Freeze/release controls

### 10.6 Item editor

- Item key
- Item key type
- Category
- Title
- Description
- Done toggle
- Tags
- Audit metadata

### 10.7 Members screen

- Member list
- Role badges
- Invite member
- Change role
- Remove member
- Single-admin warning if applicable

### 10.8 Sync and trust screen

- Last pull and push timestamps
- Detected conflicts
- Untrusted/corrupt entities
- Shared-folder permission warnings
- Audit inspection entry points

### 10.9 Conflict resolution screen

- Entity summary
- Mine/theirs/result comparison
- Signature and author context
- Resolution action
- Re-sign and commit step if required

## 11. Core User Flows

### 11.1 Create new project

1. User creates identity.
2. User creates project and becomes first admin.
3. App creates signed root files.
4. User configures shared folder.
5. App evaluates folder safety.

### 11.2 Add version and items

1. User creates a version.
2. User adds categorized items.
3. App generates item keys automatically.
4. User reorders items if needed.
5. App signs changed entities.

### 11.3 Sync with team

1. User pulls latest remote state.
2. App validates trust.
3. User makes local edits.
4. User pushes signed changes.
5. Other users pull and merge.

### 11.4 Release a version

1. User opens target version.
2. App validates that release action is allowed.
3. User confirms release.
4. App marks version as released and immutable.
5. App offers to move incomplete items to next version.
6. User generates changelog.

### 11.5 Resolve conflict

1. Pull detects conflicting entity update.
2. App blocks unsafe auto-merge.
3. User opens conflict resolution screen.
4. App validates chosen result.
5. Result is signed and logged.

## 12. Suggested Solution Modules

### 12.1 `Blueprints.Core`

- domain entities
- enums and value objects
- business rules
- changelog generator
- item key generator

### 12.2 `Blueprints.Storage`

- filesystem repository layer
- JSON canonical serializer
- schema version loader
- atomic file writer
- workspace/shared path resolver

### 12.3 `Blueprints.Security`

- identity creation
- DPAPI private key protection
- Ed25519 signing and verification
- membership authority evaluator
- trust state evaluator

### 12.4 `Blueprints.Collaboration`

- sync manifest service
- diff engine
- merge engine
- conflict detector
- audit log service

### 12.5 `Blueprints.App`

- Avalonia views
- view models
- command workflows
- notifications and trust banners

## 13. Suggested Delivery Phases

### Phase 1: Local single-user foundation

- Create/open local project
- Create versions and items
- Generate item keys
- Generate Markdown changelog
- Persist signed entities locally

### Phase 2: Identity and membership

- Create user identity
- Sign and verify files
- Add members through admin invitation
- Enforce roles locally

### Phase 3: Shared sync

- Configure shared folder
- Push and pull changed entities
- Shared manifest
- Trust validation on import

### Phase 4: Conflict and recovery UX

- Detect merge conflicts
- Resolve item and version conflicts
- Membership conflict workflow
- Untrusted/corrupt read-only mode

### Phase 5: Hardening

- Shared-folder safety checks
- performance tuning
- audit inspection UX
- migration/versioning support

## 14. Validation and Testing Strategy

### Unit tests

- item key generation
- version lifecycle rules
- role enforcement
- canonical JSON signing
- trust state evaluation
- changelog generation

### Integration tests

- two-user sync flow
- invitation acceptance
- membership revision conflicts
- immutable release protection
- rejected unsigned content
- rejected unauthorized membership edits

### Adversarial tests

- modified signed file
- deleted audit log entry
- replayed old membership file
- forged display name
- broad-write shared folder warning
- partial sync batch write

## 15. What I Need From You

Nothing is blocking the implementation planning pass.

Optional preferences you may want to decide later:

- whether the app name in the UI should always show the project code prominently
- whether changelog export should support a branded header/footer template in v1.0

## 16. Customization Boundary

Project-defined customization should cover:

- project name
- project code
- visible category names
- visible item type names
- key prefixes
- which key counters are version-scoped or project-scoped
- changelog display defaults
- UI visibility of optional metadata

Safety-critical rules must remain fixed:

- stable internal IDs
- signature generation and verification
- trust-state evaluation
- role enforcement rules
- released-version immutability
- audit-log tamper checks
