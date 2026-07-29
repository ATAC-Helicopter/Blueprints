# Changelog

Notable changes to Blueprints are documented here. The project follows semantic versioning once release tags are created.

## Unreleased

### Added

- Canonical public documentation for users, contributors, architecture, workspace format, security, and releases.
- A milestone-based product roadmap.
- Blueprints visual identity and application icon.
- Cross-platform CI, CodeQL, dependency review, milestone automation, PR labels, and community templates.
- Lightweight milestone-release records generated from the changelog without binary builds.
- A diagram-first blueprint canvas with draggable version and work-item nodes, live relationship connectors, zoom controls, and direct node inspection.
- Native folder pickers for project creation and opening.

### Changed

- Upgraded the application to .NET 10 LTS, Avalonia 12.1.1, and the latest stable direct dependencies.
- Replaced the dashboard-style overview and wide navigation sidebar with a hands-on canvas, contextual inspector, compact tool rail, and focused secondary workspaces.
- Made command feedback visible across the active workspace.
- Clarified local-workspace and shared-exchange terminology.
- Moved superseded planning and handoff documents into `docs/archive`.

### Fixed

- Repository-local SDK selection now works when only a newer compatible SDK is installed.
- Shell helpers are executable.

## 0.1.0-alpha.1 — 2026-02-28

Foundation milestone:

- established project, version, item, member, and changelog domain contracts;
- added canonical JSON persistence and detached Ed25519 signatures;
- protected local signing keys and validated signed workspaces on load;
- created the initial shared-folder sync, conflict, and audit-chain foundation;
- added repository automation and the first executable Avalonia application scaffold.
