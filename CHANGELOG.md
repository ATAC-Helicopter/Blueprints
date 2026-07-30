# Changelog

Notable changes to Blueprints are documented here. The project follows semantic versioning once release tags are created.

## Unreleased

### Added

- Canvas box selection, additive multi-selection, grouped node dragging, `Ctrl+A`, `Escape`, precise arrow-key movement, live alignment guides, and a viewport minimap.
- Release-readiness diagnostics that surface unavailable or dirty repositories, incomplete items, missing post-tag history, and recent commits that do not map to completed items in the selected version.
- Machine-local links for up to eight Git worktrees, with combined read-only health, source discovery, source trace, and release-readiness analysis.
- A provider-neutral reference contract for planning documents, commits, issues, pull requests, releases, and project records across local Git, GitHub, and GitLab.
- Bounded read-only discovery for up to 100 GitHub pull requests and 50 GitHub release records per linked repository, including merge/publication state and provider-neutral provenance.
- Repository-linked GitHub Project discovery for standalone draft items through a bounded read-only GraphQL query covering at most 10 projects, 100 items per project, and 100 returned drafts.
- An injectable provider-neutral hosted-source reader boundary, keeping repository discovery and approval workflows independent from the current authenticated GitHub CLI implementation.
- User-defined directional or undirected relationship types with validated colors and optional descriptions, plus signed relationships between project, version, and work-item nodes.
- Relationship authoring and removal in the canvas inspector, colored relationship projection on the canvas, archive cleanup, audit operations, and document-aware conflict summaries.
- Direct bounded GitHub REST and GraphQL discovery for issues, pull requests, releases, project-linked issues, and standalone Project drafts, including anonymous public-repository reads.
- A provider-operation policy that allows reads directly but requires a fresh, exact-target, single-use approval before any future hosted-provider write.

### Changed

- Alpha 4 development builds now identify themselves as `0.4.0-alpha.4-dev`.
- Canvas guidance now exposes the active selection and the available multi-node keyboard controls; resulting layout changes keep using signed, audited persistence.
- The release planner now presents source-control blockers and follow-up guidance next to source trace and changelog review.
- Source Lens accepts one repository path per line and preserves the full repository-qualified provenance of combined proposals.
- Source Lens proposals now expose structured provider, repository, artifact kind, identifier, and optional web location instead of relying only on GitHub-specific display strings.
- Combined Source Lens summaries now separate issue, pull-request, release, and project-linked proposal counts.
- Schema-1 workspaces may contain an optional signed `project/relationships.json`; missing files remain compatible with earlier workspaces and the complete relationship graph is one revisioned conflict domain.
- Source Lens no longer requires the GitHub CLI. Private repository and GitHub Project discovery uses the environment-only `BLUEPRINTS_GITHUB_TOKEN`; Blueprints does not persist the credential.

## 0.3.0 — 2026-07-30

Understandable collaboration milestone. This release lets distinct local identities join a signed project, exchange changes through an untrusted shared directory, understand conflicts, and recover from blocked or mistaken operations.

### Added

- Session-scoped undo and redo for node drags and auto-arrangement, with toolbar controls and `Ctrl+Z`, `Ctrl+Shift+Z`, and `Ctrl+Y` shortcuts.
- Every applied undo or redo is saved as a new signed, audited canvas-layout revision so history never bypasses workspace integrity.
- A two-workspace collaboration harness covering push, pull, sequential edits, overlapping edits, and preservation of the blocked local copy.
- Automatic machine-local conflict recovery snapshots containing both available document/signature pairs and resolution metadata before a whole-document choice is applied.
- Signed identity-request and project-invitation files with proof-of-key-possession validation.
- Member-key-aware workspace, manifest, incoming-document, and audit validation for distinct local identities.
- A staged join flow that validates invitation targets, trust anchors, membership, signatures, manifest continuity, and audit history before creating the final local workspace.
- Document-aware conflict comparisons for project configuration, membership, versions, items, and audit entries, with bounded raw evidence retained for diagnosis.
- Explicit member-key rotation, compromise, and lost-key operating rules.
- Backup, shared-exchange restoration, conflict reversal, identity recovery, and recovery-drill procedures.
- Explicit first-run identity creation and pre-project signed identity-invitation export.
- Two-step recoverable archive flows for draft versions and items, including layout cleanup and signed audit records.
- Changelog preview without file output plus incomplete-item, item-key, description, and compact export controls.
- End-to-end view-model command coverage for identity setup, project creation, version editing, freezing, releasing, previewing, and exporting.
- Category-grouped release items and actionable empty states for projects without versions or connected accomplishments.

### Changed

- Alpha 3 development builds now identify themselves as `0.3.0-alpha.3-dev`.
- Conflict resolution reports the recovery-copy location and writes its status metadata atomically.
- The exchange view now shows the last pulled and pushed manifest versions and the last successful trust-validation time persisted for the local workspace.
- Team setup now uses native invitation-file import/export instead of identity-bundle copy and paste.
- The exchange workspace now surfaces current shared manifest evidence, local push/pull versions, audit-chain status, and state-specific recovery guidance.

### Fixed

- Canvas nodes can no longer be dragged when workspace trust or sync-conflict state forbids mutation.
- Clean test builds now reference the command toolkit explicitly instead of relying on a transitive compile asset.
- Conflict recovery rejects paths that escape the expected workspace root.
- Project invitations targeting another local identity are rejected before a workspace is created.
- Tampered identity invitation fields are rejected when their proof no longer verifies.
- Signed manifest rollback and same-version unknown-batch replay are rejected.
- Item deletions propagate with local recovery copies and deletion markers while required project-document deletion remains blocked.
- Workspace exchange paths are contained beneath their expected roots.
- Pull stages and revalidates a complete inbox before local mutation, records rollback pairs, and restores them if application or sync-state persistence fails.
- Viewer and inactive-member keys are excluded from current workspace and incoming-change authority while remaining available for historical audit verification.

### Security

- Multi-member signatures resolve by key ID against machine-local project trust anchors established by project creation or a targeted signed invitation.
- Identity invitations prove possession of the included private key; project invitations bind the target identity, inviter administrator, project, membership revision, exchange location hint, and trusted member keys.
- Shared manifests reject invalid signatures, rollback, unknown same-version batches, content/hash discontinuity, missing required documents, and malformed document/signature pairs.
- Pull validates shared content, stages it locally, revalidates the staged bytes, and retains rollback copies before changing trusted local state.
- Deactivated and Viewer identities cannot mutate or publish current project content.

### Workspace compatibility

- Signed project schema remains version 1.
- `ProjectMember.keyId` is an optional schema-1 field. Existing workspaces derive the legacy key ID from the member user ID.
- Project trust anchors, archives, conflict recovery, canvas viewport state, and sync staging remain machine-local and are excluded from signed exchange snapshots.
- Existing single-identity projects remain readable and gain a local trust-anchor file after successful validation.

### Known limitations

- This release contains no installers or application binaries.
- Conflict resolution compares known fields but still applies a whole-document choice rather than a field merge.
- Same-user automated key rotation, hardware-backed keys, and revocation timestamps are not implemented.
- Shared-folder confidentiality and availability remain responsibilities of the underlying storage.
- Advanced canvas multi-selection, alignment tools, minimap navigation, and user-defined relationship types are deferred.
- GitHub Project discovery does not include standalone draft items, and provider integrations remain read-only.

## 0.2.0-alpha.2 — 2026-07-30

Interactive workspace milestone. This prerelease makes Blueprints usable as a hands-on visual release planner and establishes the approval-first source-discovery workflow.

### Added

- Canonical public documentation for users, contributors, architecture, workspace format, security, and releases.
- A milestone-based product roadmap.
- Blueprints visual identity and application icon.
- Cross-platform CI, CodeQL, dependency review, milestone automation, PR labels, and community templates.
- Lightweight milestone-release records generated from the changelog without binary builds.
- A diagram-first blueprint canvas with draggable version and work-item nodes, live relationship connectors, zoom controls, and direct node inspection.
- Signed, revisioned node-position persistence with entity validation, audit entries, sync support, auto arrangement, and explicit save controls.
- Machine-local canvas zoom and viewport persistence that cannot create audit noise or collaboration conflicts.
- Canvas-engine and troubleshooting documentation covering behavior, format, security, compatibility, conflicts, and recovery.
- Native folder pickers for project creation and opening.
- Source Lens discovery for changelogs, roadmaps, GitHub issues, and issue-linked GitHub Projects.
- Editable proposal review with duplicate warnings, provenance, confidence, target-version selection, and explicit batch approval.
- Adaptive next-action guidance based on workspace trust, conflicts, release state, source proposals, and sync status.
- A disabled-by-default SonarQube Cloud workflow template for .NET analysis and Coverlet coverage. It requires explicit repository activation and credentials.

### Changed

- Upgraded the application to .NET 10 LTS, Avalonia 12.1.1, and the latest stable direct dependencies.
- Replaced the dashboard-style overview and wide navigation sidebar with a hands-on canvas, contextual inspector, compact tool rail, and focused secondary workspaces.
- Reworked secondary navigation into a readable workflow rail and promoted source discovery from passive status cards to an approval-first workspace.
- Made command feedback visible across the active workspace.
- Clarified local-workspace and shared-exchange terminology.
- Moved superseded planning and handoff documents into `docs/archive`.

### Fixed

- Repository-local SDK selection now works when only a newer compatible SDK is installed.
- Shell helpers are executable.
- Source discovery no longer parses the same planning file twice on case-insensitive filesystems.

### Security

- Shared canvas positions are signed, bounded, entity-validated, audited, and synchronized.
- Source discovery remains read-only and bounded; imports require explicit human approval and trusted mutable targets.
- CodeQL, dependency review, package vulnerability checks, and cross-platform tests remain required repository gates.

### Known limitations

- This release does not include installers or application binaries.
- Identity onboarding, undo/redo, deletion/archive, guided recovery, and polished two-user collaboration remain incomplete.
- GitHub Project discovery currently includes project-linked issues, not standalone draft items.
- SonarQube Cloud is scaffolded but does not run until the project is imported and repository credentials are configured.

## 0.1.0-alpha.1 — 2026-02-28

Foundation milestone:

- established project, version, item, member, and changelog domain contracts;
- added canonical JSON persistence and detached Ed25519 signatures;
- protected local signing keys and validated signed workspaces on load;
- created the initial shared-folder sync, conflict, and audit-chain foundation;
- added repository automation and the first executable Avalonia application scaffold.
