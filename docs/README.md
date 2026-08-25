# Blueprints documentation

Use this directory as the canonical product, operations, security, and development documentation for Blueprints.

## Start here

Start with the [complete user manual](user-guide.md). It covers installation, first launch, project setup, daily planning, releases, imports, team management, sync, validation, backup, recovery, and troubleshooting in application order.

## Documentation map

### Use the application

| Need | Document |
| --- | --- |
| Learn the complete workflow | [Complete user manual](user-guide.md) |
| Manage releases, work items, and relationships | [Complete user manual](user-guide.md#7-plan-work-in-overview) |
| Import issues, changes, releases, and planning files | [Imports and source discovery](source-lens.md) |
| Use keyboard or assistive technology | [Accessibility](accessibility.md) |
| Diagnose a problem | [Troubleshooting](troubleshooting.md) |

### Operate and protect a project

| Need | Document |
| --- | --- |
| Back up, restore, or test recovery | [Backup and disaster recovery](backup-recovery.md) |
| Understand identity recovery and key changes | [Member key lifecycle](key-lifecycle.md) |
| Inspect VaultSync recovery evidence | [VaultSync integration](vaultsync-integration.md) |
| Understand supported platforms and response targets | [Support policy](support-policy.md) |
| Review release milestones | [Release history](releases.md) |

### Develop and review Blueprints

| Need | Document |
| --- | --- |
| Build, test, and contribute | [Development guide](development.md) |
| Understand projects, dependencies, and runtime flows | [Architecture](architecture.md) |
| Inspect the persisted schema | [Workspace format](workspace-format.md) |
| Understand legacy signed layout compatibility | [Canvas data contract](canvas-engine.md) |
| Review cryptography and trust boundaries | [Security model](security-model.md) |
| Review attacker assumptions | [Threat model](threat-model.md) |
| Activate code-quality reporting | [SonarQube Cloud](sonarqube.md) |
| Publish a release | [Release process](releasing.md) |
| Choose the next product work | [Roadmap](../Roadmap.md) |

## Recommended reading paths

- **First-time user:** complete user manual → troubleshooting → backup and disaster recovery.
- **Team administrator:** complete user manual sections 11–16 → member key lifecycle → security model.
- **Contributor:** development guide → architecture → workspace format → security model → contributing guide.
- **Security reviewer:** security model → threat model → workspace format → backup and disaster recovery.

## Documentation policy

- `README.md`, this index, and the linked guides describe the current repository.
- `Roadmap.md` is the only canonical forward plan.
- Files under `docs/archive/`, when present, are historical design and handoff material. They are not product documentation and may contain stale status statements.
- A behavior change is incomplete until its relevant guide, interface terminology, and tests are updated.
- Persisted-format changes must document compatibility, validation, sync behavior, failure handling, and security impact.

If documentation and code disagree, treat code and tests as current behavior, then fix the documentation in the same pull request.
