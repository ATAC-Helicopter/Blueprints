# Blueprints roadmap

This is the canonical product roadmap. It is organized by outcomes, not implementation layers, and should be updated when an issue changes scope or a milestone is completed.

Last reviewed: 2026-07-30

## Product direction

Blueprints should become the smallest trustworthy path from “we plan to ship this” to a reviewed Markdown changelog:

1. useful for one developer without any hosted service;
2. understandable without knowing the signed-file implementation;
3. safe and explicit when a small team exchanges work;
4. extensible toward GitHub, GitLab, and VaultSync without making a provider the source of truth.

## Completed — v0.2: coherent solo workflow

Goal: a new contributor can build the app, and a developer can plan and export a release without implementation knowledge.

- [x] Restore a reproducible local build and executable helper scripts.
- [x] Establish public-project documentation, templates, automation, and visual identity.
- [x] Move the supported runtime to .NET 10 LTS and Avalonia 12.
- [x] Make the primary workspace a draggable, diagram-first blueprint canvas backed by real version and item relationships.
- [x] Persist, sign, audit, sync, validate, and restore shared node positions while keeping viewport preferences machine-local.
- [x] Add approval-first Source Lens discovery for changelogs, roadmaps, GitHub issues, and issue-linked GitHub Projects.
- [x] Replace icon-only navigation with a clear workflow rail and adaptive next-action guidance.
- [x] Keep release, team, sync, trust, and integration operations in focused secondary tools.
- [x] Add native folder pickers instead of requiring raw paths.
- [x] Add explicit first-run identity setup and pre-project identity-invitation export.
- [x] Add two-step recoverable archive flows for draft versions and items.
- [x] Group release items by changelog category and improve version/item empty states.
- [x] Preview changelogs before writing a file and expose incomplete, key, description, and compact options.
- [x] Add view-model workflow tests for identity setup, create, edit, freeze, release, preview, and export.
- [x] Establish lightweight milestone tags and accomplishment records without binary packaging.

Exit criteria:

- clean clone builds with one documented command;
- create, close, reopen, plan, release, and export work end to end;
- all destructive or immutable actions explain their consequences;
- the v0.2 milestone is recorded by a tag, changelog section, release-history entry, and lightweight GitHub prerelease.

## Completed — v0.3: understandable collaboration

Goal: two people can exchange signed changes through a shared directory and understand every blocked action.

- [x] Add a two-workspace end-to-end collaboration test harness.
- [x] Replace raw-first conflict previews with bounded document-aware field comparisons and retained raw evidence.
- [x] Add guided whole-document conflict resolution and machine-local recovery copies.
- [x] Show shared/last push/last pull manifest evidence, audit status, and actionable recovery steps.
- [x] Replace identity-bundle copy/paste with signed identity and project invitation files.
- [x] Define behavior for member key rotation, compromise, and lost keys.
- [x] Test interrupted/partial shared copies, deleted files, required-file deletion, and stale or replayed manifests.
- [x] Document backup and disaster-recovery procedures.

Exit criteria:

- user A can push and user B can pull on a supported shared-folder target;
- tampered or incomplete input is rejected without mutating trusted local state;
- conflicts explain what changed and produce a recoverable result;
- membership changes cannot remove the last active administrator.

## Completed — v0.4: source-control awareness

Goal: connect release intent to source history without weakening local ownership.

- [x] Read local Git root, branch, remote, dirty state, tag, and recent commits.
- [x] Match item keys in commit subjects for changelog context.
- [x] Expand canvas editing with box selection, multi-select, keyboard movement, alignment guides, and a minimap.
- [x] Add user-created typed relationships after defining their domain and conflict semantics.
- [x] Link a Blueprints project to one or more repositories.
- [x] Add release-readiness diagnostics for uncommitted and unmatched changes.
- [x] Define provider-neutral issue, pull-request, and release references.
- [x] Add a bounded read-only GitHub issue/Project discovery adapter through the authenticated GitHub CLI.
- [x] Route hosted discovery through a provider-neutral reader contract and add pull-request/release references.
- [x] Replace the authenticated GitHub CLI implementation with a direct provider adapter.
- [x] Add standalone GitHub Project draft-item discovery.
- [x] Define and separately approve any future provider write operations.
- [x] Add GitLab parity after the provider contract stabilizes.

Exit criteria:

- source status is useful offline;
- provider credentials and settings never enter signed project truth;
- no hosted provider is required for core release planning.

## In progress — v0.5: VaultSync integration

Goal: let VaultSync improve transport and recovery while each product keeps a clear responsibility.

- [x] Finalize the exchange-root contract.
- [x] Detect a VaultSync-managed location and report backup health.
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

## Continuous security assurance

Security work is a release requirement across every milestone, not a one-time feature:

- [ ] maintain an attacker-focused threat model for every trust boundary;
- [ ] add adversarial tests for malformed, replayed, rolled-back, partially written, and maliciously signed input;
- [ ] define key rotation, revocation, recovery, and platform keystore integration;
- [ ] make workspace updates atomic and recoverable;
- [ ] generate an SBOM for every published package;
- [ ] generate provenance attestations when downloadable packages are introduced;
- [ ] commission an independent security review before a stable release;
- [ ] publish supported-version and vulnerability-response targets;
- [ ] never describe a release as audited unless its exact source and artifacts were reviewed.

No release can guarantee that every user is always safe. Blueprints instead aims to make its trust boundaries explicit, minimize sensitive state, fail closed when integrity cannot be established, and respond transparently when a weakness is found.

## How roadmap work becomes issues

Each unchecked item should become one scoped issue with:

- a user-visible outcome;
- acceptance criteria;
- security and data-migration impact;
- automated and manual verification;
- milestone and area labels.

The roadmap is directional; GitHub milestones and issues are the execution record.
