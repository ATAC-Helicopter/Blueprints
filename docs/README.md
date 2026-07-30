# Blueprints documentation

Use this directory as the canonical technical and user documentation.

## Start here

| Audience | Document |
| --- | --- |
| Trying the app | [User guide](user-guide.md) |
| Understanding the visual workspace | [Canvas engine](canvas-engine.md) |
| Diagnosing a problem | [Troubleshooting](troubleshooting.md) |
| Building or contributing | [Development guide](development.md) |
| Understanding the code | [Architecture](architecture.md) |
| Inspecting project files | [Workspace format](workspace-format.md) |
| Reviewing trust boundaries | [Security model](security-model.md) |
| Reviewing milestone accomplishments | [Release history](releases.md) |
| Publishing a build | [Release process](releasing.md) |
| Choosing the next work | [Roadmap](../Roadmap.md) |

## Documentation policy

- `README.md`, this index, and the linked guides describe the current repository.
- `Roadmap.md` is the only canonical forward plan.
- `Plan.md`, `ImplementationPlan.md`, `ProductDirection.md`, `IntegrationsStrategy.md`, `VaultSyncContext.md`, `AgentQuickstart.md`, `CodexHandoff.md`, and `TestPlan.md` are retained as historical design and handoff material. They may contain stale status statements.
- A behavior change is incomplete until its relevant guide and tests are updated.
- Persisted-format changes must document compatibility, validation, sync behavior, failure handling, and security impact.

If documentation and code disagree, treat code and tests as current behavior, then fix the documentation in the same pull request.
