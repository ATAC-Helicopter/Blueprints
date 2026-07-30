# Changelog

Notable changes to Blueprints are documented here. The project follows semantic versioning once release tags are created.

## Unreleased

### Added

- Session-scoped undo and redo for node drags and auto-arrangement, with toolbar controls and `Ctrl+Z`, `Ctrl+Shift+Z`, and `Ctrl+Y` shortcuts.
- Every applied undo or redo is saved as a new signed, audited canvas-layout revision so history never bypasses workspace integrity.
- A two-workspace collaboration harness covering push, pull, sequential edits, overlapping edits, and preservation of the blocked local copy.
- Automatic machine-local conflict recovery snapshots containing both available document/signature pairs and resolution metadata before a whole-document choice is applied.

### Changed

- Alpha 3 development builds now identify themselves as `0.3.0-alpha.3-dev`.
- Conflict resolution reports the recovery-copy location and writes its status metadata atomically.
- The exchange view now shows the last pulled and pushed manifest versions and the last successful trust-validation time persisted for the local workspace.

### Fixed

- Canvas nodes can no longer be dragged when workspace trust or sync-conflict state forbids mutation.
- Clean test builds now reference the command toolkit explicitly instead of relying on a transitive compile asset.
- Conflict recovery rejects paths that escape the expected workspace root.

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
