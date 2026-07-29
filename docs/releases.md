# Release history

Blueprints uses versions as durable milestone checkpoints. Each entry links a Git tag to a completed product outcome and summarizes what became true at that point. Pre-v1.0 GitHub releases contain notes only—no application binaries.

## Published milestones

| Version | Date | Milestone | Accomplishments |
| --- | --- | --- | --- |
| [`v0.1.0-alpha.1`](https://github.com/ATAC-Helicopter/Blueprints/releases/tag/v0.1.0-alpha.1) | 2026-02-28 | Foundation | Domain contracts, canonical signed persistence, protected identities, shared-folder sync foundation, audit chaining, and the initial Avalonia application scaffold |

## Planned checkpoints

| Version | Milestone outcome |
| --- | --- |
| `v0.2.0` | A coherent and usable solo release-planning workflow |
| `v0.3.0` | Collaboration that two users can understand and recover |
| `v0.4.0` | Provider-neutral source-control awareness |
| `v0.5.0` | Explicit VaultSync transport and backup-health integration |
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
