# Blueprints

Product and Technical Design Specification

Version: 1.1 Draft
Status: Structured Draft

## Decision Summary for v1.0

- Windows-first release
- Each user works from a local copy and syncs through a shared source of truth
- Membership is invitation-only
- Each item belongs to exactly one version
- Human-readable item keys are first-class and shown in changelogs
- ID formats and changelog display rules are customizable per project
- Released versions are immutable
- Frozen blocks editor changes; admins can override with an explicit signed action
- Changelog export excludes incomplete items by default
- Item ordering is manual with chronological fallback
- Multiple admins are supported, but quorum-based multi-signature rules are out of scope
- No break-glass recovery bypass in v1.0; recovery requires another valid admin
- Externally modified signed content cannot be re-signed as trusted history

## 1. Executive Summary

**Product name:** Blueprints

**Tagline:** Plan releases. Ship confidently.

**Category:** Developer tooling, release planning, offline collaboration

Blueprints is a local-first, version-centric release planning application for developers and small teams. It replaces generic task boards with a structured system focused on release versions, categorized changes, changelog generation, and secure file-based collaboration.

The product is intentionally not:

- A Kanban board
- Jira
- A cloud-first SaaS tool
- A real-time collaborative editor

The product is:

- A release-first planning system
- Local-first by default
- Designed for small technical teams
- Safe to share through folders instead of servers
- Built around tamper-evident collaboration

## 2. Problem Statement

Small teams often plan releases using generic tools that do not match release-oriented workflows. Typical boards are optimized for task management, not for version planning, changelog generation, or secure offline collaboration.

For teams that prefer local ownership over cloud dependence, the gap is larger:

- Shared folders are easy to use but unsafe by default
- Attribution is weak or editable
- Roles can be spoofed
- File tampering is difficult to detect
- Changelogs are manually assembled at release time

Blueprints addresses this by treating versions, membership, signatures, and changelog structure as first-class concepts.

## 3. Product Goals

### Primary goals

- Provide structured version-based planning
- Generate professional changelogs from release data
- Enable small-team collaboration without requiring a server
- Prevent role, identity, and display name abuse in shared projects
- Detect tampering and unauthorized edits
- Stay lightweight, understandable, and extensible

### Non-goals for v1.x

- Enterprise workflow support
- Large-team permission hierarchies
- Cloud hosting
- Real-time cursors or simultaneous document editing
- Agile sprint tooling
- Time tracking

## 4. Target Users

### Primary users

- Solo developers
- Indie game developers
- Small open-source teams of 2 to 5 members

### Secondary users

- Small internal engineering teams

### Environment assumptions

Projects are shared through:

- SMB / NAS folders
- Synced folders such as OneDrive or Dropbox
- Shared drives

No central server is assumed in v1.0.

## 5. Product Principles

- **Release-first:** the version is the primary planning unit
- **Local-first:** project data lives in files the team controls
- **Structured over generic:** change categories and release states are built in
- **Safe by design:** shared projects should surface trust and integrity issues early
- **Small-team optimized:** workflows should remain simple, fast, and understandable

## 6. Core Scope for v1.0

### 6.1 Projects

Each project contains:

- `ProjectId` (GUID)
- `Name`
- `VersioningScheme` (SemVer by default)
- `CreatedUtc`
- `ProjectCode` (for example `VS`)
- `ItemKeyRules`
- `ChangelogRules`
- `Members`
- `Versions`

Project capabilities:

- Create project
- Open existing project
- Export project data
- Share project folder
- Validate trust state on load
- Configure project-specific item key and changelog behavior

### 6.2 Versions

Each version contains:

- `VersionId` (GUID)
- `Name` such as `1.6.0`
- `Status`
- `CreatedUtc`
- `ReleasedUtc` (optional)
- `Notes` (optional)
- Categorized items

Supported statuses:

- Planned
- In Progress
- Frozen
- Released

Lifecycle rules:

- Frozen versions can block new items if project policy enables it
- Releasing a version sets `ReleasedUtc`
- On release, the app may prompt to move incomplete items forward
- Released versions are immutable in v1.0
- If post-release work is needed, it should go into a new version
- Frozen is a hard block for editors
- Admins may override Frozen by performing an explicit signed action recorded in the audit log

### 6.3 Changelog Categories

Default categories:

- Added
- Changed
- Fixed
- Removed
- Security

Each category is an ordered list of items.

### 6.4 Items

Each item contains:

- `ItemId` (GUID)
- `ItemKey` (optional human-readable ID such as `BUG-104` or `ISS-12`)
- `ItemKeyType` (for example `Feature`, `Bug`, `Issue`, `Security`)
- `VersionId`
- `Category`
- `Title`
- `Description` (optional)
- `IsDone`
- `Tags` (optional)
- `CreatedUtc`
- `UpdatedUtc`
- `LastModifiedByUserId`
- `LastModifiedByName`

Each item is individually signed.

Rules:

- Each item belongs to exactly one version
- `ItemId` is the stable internal identifier
- `ItemKey` is intended for user-facing reference, sorting, and discussion
- `ItemKey` should be generated automatically by default

### 6.5 Changelog Generation

Blueprints exports changelogs as Markdown.

Example:

```md
## [1.6.0] - 2026-02-28

### Added
- VS-1601 New dashboard

### Fixed
- BUG-1042 Crash on startup
```

Export options:

- Include only completed items by default
- Include item keys by default
- Include release date
- Include descriptions
- Use compact mode

Project-level changelog rules should be configurable, including:

- Whether item keys are shown
- Whether incomplete items are included
- Whether descriptions are shown
- Whether compact mode is the default
- Category visibility and ordering

## 7. Collaboration Model

### 7.1 Collaboration philosophy

Blueprints assumes small teams can collaborate through shared storage without a hosted backend, but only if the application can verify authorship, permissions, and integrity locally.

### 7.2 Collaboration operations

The collaboration flow is based on:

- Local project copy per user
- Shared project folder as sync source of truth
- Share project folder
- Pull changes
- Push changes
- Sync local state
- Auto-merge safe changes
- Resolve conflicts when required

### 7.3 Working model

For v1.0, users should not edit the same live shared folder directly as their primary workspace.

Recommended model:

- Each user has a local working copy
- A shared folder acts as the exchange and sync location
- Push writes signed changes to the shared location
- Pull imports and validates remote changes into the local copy

This is the safest fit for the product scope because it reduces file-locking issues, partial-write risks, and accidental interference from other tools or users.

### 7.4 Expected behavior

- Users should see who changed what
- Unauthorized edits should be rejected
- Ambiguous states should be visible, not hidden
- Invalid projects should open read-only when safety cannot be guaranteed

## 8. Security and Trust Architecture

### 8.1 Identity model

Each device creates:

- `UserId` (GUID)
- `DisplayName`
- Ed25519 keypair

Private key storage:

- Windows: DPAPI
- macOS: Keychain
- Linux: encrypted file fallback

Public keys are stored in project membership data.

### 8.2 Membership model

The `members.json` file contains:

- `MembershipRevision`
- Member list

Each member includes:

- `UserId`
- `PublicKey`
- `DisplayName`
- `Role`
- `JoinedUtc`

Roles for v1.0:

- Admin
- Editor
- Viewer

Rules:

- `members.json` must be signed by one or more admins
- The app must reject invalid membership changes
- Membership is invitation-only in v1.0
- Users cannot promote themselves
- Users cannot change role or display name without valid admin authorization
- Join requests from unknown users are out of scope for v1.0

### 8.3 Signed entities

Critical files are stored with detached signatures:

- `item.json` + `item.sig`
- `version.json` + `version.sig`
- `members.json` + `members.sig`
- `project.json` + `project.sig`

Requirements:

- Ed25519 signatures
- Canonical JSON serialization
- Signature validation during load, pull, and sync
- Authorization validation against membership revision rules
- Project configuration affecting IDs, changelogs, and workflow must be signed like other critical project data

### 8.4 Audit log

The audit log is append-only and tamper-evident.

Example layout:

```text
log/
  2026-02-28T12-34-01_001.json
  2026-02-28T12-34-01_001.sig
```

Each entry contains:

- `ChangeId`
- `PrevHash`
- `ChangeSummary`
- `AuthorUserId`
- `Timestamp`
- `MembershipRevisionSeen`

Each log entry is:

- Signed by the author
- Linked to the previous entry by hash

This should detect:

- History deletion
- Rollbacks
- Silent tampering

### 8.5 Trust states

On project load, Blueprints evaluates trust status:

- `Trusted`: all signatures and integrity checks pass
- `Untrusted`: unauthorized or unverifiable changes were detected
- `Corrupt`: data is invalid, incomplete, or structurally inconsistent

If a project is not trusted:

- Open read-only
- Surface the reason clearly
- Allow admin inspection and optional re-signing if the state is legitimate

## 9. Shared Folder Safety

When a project is opened from or configured in a shared folder, the app should evaluate whether the folder permissions are too broad.

### Windows checks

- Inspect NTFS ACLs
- Warn if `Everyone` or `Authenticated Users` has write access

### Linux and macOS checks

- Warn on world-writable locations
- Warn on broad group-writable locations

### Safety states

- Green: restricted access
- Yellow: uncertain or partially verifiable
- Red: broad write access detected

If the state is red, admins should see a warning and may:

- Acknowledge the risk as project policy
- Re-check later

## 10. Merge and Conflict Model

### 10.1 Auto-merge cases

Auto-merge should succeed when:

- Different entities were modified
- The same entity was modified in different non-overlapping fields
- New entities were added independently
- An item moved while unrelated metadata stayed unchanged

### 10.2 Conflict cases

A conflict should be raised when:

- The same entity was changed by two users
- The same field was changed differently
- Both changes happened after the last shared sync point

### 10.3 Conflict UI

The conflict dialog should show:

- Mine
- Theirs
- Result preview

Resolution actions:

- Keep Mine
- Keep Theirs
- Combine for text fields where safe

### 10.4 Ordering and item keys

Item presentation should support both deliberate ordering and predictable fallback behavior.

Rules:

- Items can be manually ordered within a category
- If no manual order is set, fallback ordering is chronological
- `ItemKey` may be used as a user-facing reference label, but not as the source of truth for identity
- Changelog entries should display the `ItemKey` before the item title by default
- Item key formats should be customizable per project
- Item key visibility should be toggleable in the UI and export settings

Suggested examples:

- `VS-1567`
- `BUG-1042`
- `ISS-215`
- `SEC-8`

Recommended v1.0 key policy:

- Feature keys are version-scoped and project-prefixed
- Bugs, issues, and security items use global counters per project
- The app generates keys automatically when an item is created

Customization requirements:

- Projects can define key prefixes per item type
- Projects can choose which item types use version-scoped counters versus global counters
- Projects can enable or disable item keys in exports and primary views
- Projects can rename item types without changing the underlying stable identity model
- Customization must not allow duplicate keys within the same project state
- Changing key rules affects future generated keys only unless an explicit migration action is performed

Recommended interpretation:

- `VS-1567` means project prefix `VS`, version `1.5`, item sequence `67`
- `BUG-1042` means the 1042nd bug item in the project
- `ISS-215` means the 215th issue item in the project

Rationale:

- Feature work is usually discussed in the context of a target release, so version-scoped keys are readable there
- Bugs and issues often span versions, so global counters are easier to reference over time
- `ItemId` remains the true stable identifier even if future key rules change

### 10.5 Membership conflict policy

Membership changes are security-sensitive and should be treated more strictly than normal content merges.

Rules:

- `members.json` is never auto-merged
- Competing membership revisions always create a conflict
- Conflict resolution requires an admin
- The resolved membership file must be re-signed as a new revision

## 11. User Experience Direction

### 11.1 Design principles

- Clean
- Structured
- Developer-centric
- Dense but readable
- Minimal decoration

### 11.2 Main shell layout

```text
------------------------------------------------
| Sidebar | Top Bar                           |
|         |------------------------------------|
| Projects| Main Content Area                 |
------------------------------------------------
```

### 11.3 Project view

```text
------------------------------------------
| Versions List | Version Detail Panel   |
------------------------------------------
```

Left side:

- Version list
- Status badge
- Release date

Right side:

- Version header
- Generate changelog action
- Categorized sections
- Inline item creation

### 11.4 Visual language

Status colors:

- Planned: gray
- In Progress: blue
- Frozen: amber
- Released: green

Special indicators:

- Conflict: red border or strong red accent
- Trusted: green badge
- Untrusted: red badge

Typography:

- Headings: semibold
- Body: regular
- Version numbers: monospace

## 12. Technical Architecture

### 12.1 Proposed stack

- .NET 8
- Avalonia UI
- MVVM
- JSON file storage
- Ed25519 crypto library

Platform scope for v1.0:

- Windows is the primary supported platform
- Windows behavior defines the baseline for key storage and shared-folder safety checks
- Cross-platform support remains a future goal, but is not a v1.0 delivery requirement

### 12.2 Proposed solution structure

```text
Blueprints.sln
  Blueprints.App
  Blueprints.Core
  Blueprints.Storage
  Blueprints.Security
  Blueprints.Collaboration
  Blueprints.Tests
```

### 12.3 Layer responsibilities

**Core**

- Domain models
- Business rules
- Version lifecycle
- Changelog generation

**Security**

- Identity management
- Signature creation and validation
- Membership validation

**Storage**

- File I/O
- Canonical JSON serialization
- Schema versioning and migration support

**Collaboration**

- Diff engine
- Merge engine
- Conflict resolution
- Audit log builder

## 13. Performance Targets

- Open a 1000-item project in under 300 ms
- Pull or push 500 entities in under 500 ms
- Batch and asynchronously validate signatures where possible

These are target goals, not yet benchmarked guarantees.

## 14. Future Expansion

Potential future extensions:

- Self-hosted collaboration server
- Optional cloud hosting
- Git integration
- Richer permission hierarchies
- Web client
- Plugin system

The schema should be versioned from the start to support migration.

## 15. Roadmap

### v1.0

- Local project creation
- Versions and items
- Signed membership
- Signed edits
- Pull, push, and sync
- Conflict resolution
- Trust states
- Shared-folder safety checks
- Changelog export

### v1.1

- Soft locks
- Search
- Tags
- Better diff preview

### v1.2

- Git tag detection
- Version comparison
- Auto version bump suggestion

## 16. Definition of Done for v1.0

Two users can:

- Create identities
- Share a project folder
- Join a project through approval
- Edit versions
- Push and pull updates
- See accurate attribution
- Resolve conflicts
- Detect tampering
- Generate a changelog
- Restrict roles safely

v1.0 is not done unless all of the following are true:

- No silent privilege escalation
- No undetected membership edits
- No unsigned content accepted as valid
- No direct-edit requirement on the shared source of truth
- Released versions cannot be silently altered

## 17. Positioning

Blueprints is:

- Release-first
- Local-first
- Secure for offline collaboration
- Structured rather than generic
- Expandable without becoming cloud-dependent

It is built for small technical teams that value control, auditability, and low operational overhead.

## 18. Product Decisions and Remaining Questions

### Confirmed product decisions

**Collaboration model**

- v1.0 uses local working copies plus sync to a shared folder
- Users do not work directly inside the shared source of truth
- Sync should operate at the logical entity and changed-file level, not as whole-project overwrite

**Platform scope**

- v1.0 is Windows-first
- Windows security primitives are the baseline reference implementation

**Membership**

- Membership is invitation-only
- Multiple admins are supported
- Advanced quorum or threshold approval is out of scope for v1.0

**Version workflow**

- Each item belongs to exactly one version
- Human-readable keys are included in changelog output by default
- Released versions are immutable
- Frozen is enforced for editors
- Admin overrides are allowed but must be signed and auditable

**Customization**

- Changelog output rules are customizable per project
- Item key generation rules are customizable per project
- UI visibility for keys and metadata is toggleable
- Safety-critical identity and signature behavior is not customizable

### Security and recovery decisions

- v1.0 should not include a break-glass bypass that weakens signature trust
- Admin key replacement is allowed only if another currently valid admin signs a new membership revision
- If the only admin loses their key, the project remains readable but becomes administratively locked
- The app should strongly recommend at least two admins for any shared project
- The app should warn when a shared project has only one admin
- Externally modified signed content cannot be re-signed into trusted history; reviewed changes must be imported as new signed changes

This is the safest recovery model for v1.0 because it avoids hidden override paths that would undermine the trust system.

### Remaining questions

No remaining open product questions are currently blocking the first implementation planning pass.

### Resolved tag decision

- Tags are metadata only in v1.0
- Tags do not affect changelog grouping
- Tags do not affect trust, merge authority, or signature rules
- Tags may be used later for filtering, search, or saved views

## 19. Recommended Next Planning Pass

The next useful document should turn this specification into an execution plan with:

- File and folder schema
- Sync algorithm definition
- Membership approval flow
- Signature verification rules
- Conflict resolution rules by entity type
- First-pass screen list and user flows
