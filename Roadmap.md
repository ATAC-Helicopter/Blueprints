# Blueprints roadmap

This is the canonical product roadmap. It is organized by outcomes, not implementation layers, and should be updated when an issue changes scope or a milestone is completed.

Last reviewed: 2026-07-29

## Product direction

Blueprints should become the smallest trustworthy path from “we plan to ship this” to a reviewed Markdown changelog:

1. useful for one developer without any hosted service;
2. understandable without knowing the signed-file implementation;
3. safe and explicit when a small team exchanges work;
4. extensible toward GitHub, GitLab, and VaultSync without making a provider the source of truth.

## Now — v0.2: coherent solo workflow

Goal: a new contributor can build the app, and a developer can plan and export a release without implementation knowledge.

- [x] Restore a reproducible local build and executable helper scripts.
- [x] Establish public-project documentation, templates, automation, and visual identity.
- [ ] Split the main window and view model into feature-sized components.
- [ ] Add folder pickers and validation instead of requiring raw paths.
- [ ] Add first-run identity setup instead of silently creating a default identity.
- [ ] Add delete/archive flows for draft versions and items.
- [ ] Group items by changelog category and improve empty states.
- [ ] Preview changelogs before writing a file; expose current changelog options.
- [ ] Add view-model workflow tests for create, edit, freeze, release, and export.
- [ ] Publish signed preview packages for Windows, macOS, and Linux.

Exit criteria:

- clean clone builds with one documented command;
- create, close, reopen, plan, release, and export work end to end;
- all destructive or immutable actions explain their consequences;
- a preview release is downloadable from GitHub.

## Next — v0.3: understandable collaboration

Goal: two people can exchange signed changes through a shared directory and understand every blocked action.

- [ ] Add a two-workspace end-to-end collaboration test harness.
- [ ] Replace raw JSON conflict previews with document-aware field comparisons.
- [ ] Add guided conflict resolution and safe recovery copies.
- [ ] Show manifest version, last push/pull, audit status, and actionable recovery steps.
- [ ] Replace identity-bundle copy/paste with invitation files or QR/text import.
- [ ] Define behavior for member key rotation and lost keys.
- [ ] Test network-share interruption, partial copies, deleted files, and stale manifests.
- [ ] Document backup and disaster-recovery procedures.

Exit criteria:

- user A can push and user B can pull on a supported shared-folder target;
- tampered or incomplete input is rejected without mutating trusted local state;
- conflicts explain what changed and produce a recoverable result;
- membership changes cannot remove the last active administrator.

## Later — v0.4: source-control awareness

Goal: connect release intent to source history without weakening local ownership.

- [x] Read local Git root, branch, remote, dirty state, tag, and recent commits.
- [x] Match item keys in commit subjects for changelog context.
- [ ] Link a Blueprints project to one or more repositories.
- [ ] Add release-readiness diagnostics for uncommitted and unmatched changes.
- [ ] Define provider-neutral issue, pull-request, and release references.
- [ ] Add a read-only GitHub adapter, then carefully scope write operations.
- [ ] Add GitLab parity after the provider contract stabilizes.

Exit criteria:

- source status is useful offline;
- provider credentials and settings never enter signed project truth;
- no hosted provider is required for core release planning.

## Later — v0.5: VaultSync integration

Goal: let VaultSync improve transport and recovery while each product keeps a clear responsibility.

- [ ] Finalize the exchange-root contract.
- [ ] Detect a VaultSync-managed location and report backup health.
- [ ] Register Blueprints exchange roots through an explicit opt-in adapter.
- [ ] Add a release safety gate based on verified backup state.
- [ ] Test restore of both local and exchange workspaces.

Exit criteria:

- Blueprints owns release semantics and signatures;
- VaultSync owns backup/sync transport and verification;
- either product remains usable when the other is absent.

## v1.0: dependable small-team release planner

Goal: supported installers and documented recovery for small teams.

- accessible and polished desktop navigation;
- versioned workspace schema and migrations;
- signed, reproducible packages and release attestations;
- tested Windows, macOS, and Linux support policy;
- documented threat model, limitations, backup, restore, and key recovery;
- stable extension contracts for providers.

## Explicit non-goals before v1.0

- a hosted Blueprints account service;
- real-time collaborative editing;
- replacing GitHub or GitLab issue tracking;
- executing arbitrary repository hooks or scripts;
- silent automatic conflict merging;
- storing private signing keys in project or shared folders.

## How roadmap work becomes issues

Each unchecked item should become one scoped issue with:

- a user-visible outcome;
- acceptance criteria;
- security and data-migration impact;
- automated and manual verification;
- milestone and area labels.

The roadmap is directional; GitHub milestones and issues are the execution record.
