# Release history

Blueprints uses versions as durable milestone checkpoints. Each entry links a Git tag to a completed product outcome and summarizes what became true at that point. Pre-v1.0 GitHub releases contain notes only—no application binaries.

## Published milestones

| Version | Date | Milestone | Accomplishments |
| --- | --- | --- | --- |
| `v0.7.0` | 2026-07-31 | Repository workflow and blueprint clarity | Automatic related-work lanes, visible wide-range zoom, native repository browsing, safe clone/pull/commit/push workflows, upstream status, and removal of the former 100-proposal import cutoff |
| `v0.6.0` | 2026-07-31 | Stable foundations and approachable desktop | Beginner-first desktop redesign, atomic signed workspace and archive mutations, migration and compatibility infrastructure, encrypted identity recovery, stable bounded provider contracts, accessibility shortcuts, threat modeling, and support targets |
| `v0.5.0` | 2026-07-31 | VaultSync recovery integration | Passive bounded backup-health awareness, explicit project-specific exchange registration, advisory release-safety diagnostics, atomic local integration settings, and an end-to-end local/exchange restore drill |
| `v0.4.0` | 2026-07-30 | Provider-neutral source-control awareness | Advanced canvas editing, signed typed relationships, multi-repository readiness analysis, provider-neutral references, direct bounded GitHub and GitLab discovery, isolated credentials, and an explicit approval boundary for future provider writes |
| `v0.3.0` | 2026-07-30 | Understandable collaboration | Distinct signing identities, signed invitation onboarding, member-key-aware trust, staged and recoverable sync, semantic conflict comparison, manifest/audit evidence, recoverable archives, explicit identity setup, and disaster-recovery guidance |
| `v0.2.0-alpha.2` | 2026-07-30 | Interactive workspace | Diagram-first canvas, signed shared layouts, machine-local viewport state, approval-first Source Lens, adaptive workflow navigation, .NET 10, Avalonia 12, and expanded security/user documentation |
| [`v0.1.0-alpha.1`](https://github.com/ATAC-Helicopter/Blueprints/releases/tag/v0.1.0-alpha.1) | 2026-02-28 | Foundation | Domain contracts, canonical signed persistence, protected identities, shared-folder sync foundation, audit chaining, and the initial Avalonia application scaffold |

## Planned checkpoints

| Version | Milestone outcome |
| --- | --- |
| `v1.0.0` | A dependable, supported small-team release planner |

## Release record requirements

Every new milestone entry must include:

- the exact version tag and completion date;
- the roadmap milestone it closes;
- user-visible capabilities delivered;
- security or workspace-format changes;
- important limitations and deferred work;
- a matching dated section in [`CHANGELOG.md`](../CHANGELOG.md).

See the [release process](releasing.md) for tagging and automation details.
