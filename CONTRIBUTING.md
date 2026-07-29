# Contributing to Blueprints

Thank you for helping make release planning calmer and more trustworthy.

## Before opening work

- Search existing issues and the [roadmap](Roadmap.md).
- For a small fix, open a focused pull request directly.
- For new features, workspace-format changes, or security-model changes, open a proposal issue first.
- Never include real private keys, tokens, production workspaces, or confidential changelogs in tests or reports.

## Development flow

1. Fork or branch from `develop`.
2. Use a descriptive branch such as `fix/manifest-validation`.
3. Keep commits focused and explain why the behavior changes.
4. Run `./scripts/verify.sh`.
5. Update tests and documentation.
6. Open a pull request into `develop` using the template.

## Pull-request expectations

A reviewable pull request:

- describes the user-visible outcome;
- identifies security and workspace compatibility impact;
- includes automated coverage proportional to risk;
- reports manual verification when UI behavior changes;
- avoids unrelated formatting or dependency churn;
- keeps hosted-provider details outside the core domain.

Maintainers may ask to split broad pull requests.

## Security and data changes

Read [the security model](docs/security-model.md). For signed formats, identities, membership, sync, audit, or recovery changes, include attacker assumptions and adversarial tests.

Do not report vulnerabilities in public issues; follow [SECURITY.md](SECURITY.md).

## Style

- Follow existing C# conventions and nullable annotations.
- Prefer clear names over comments that repeat code.
- Use immutable records for persisted data.
- Keep public documentation in plain language.
- Do not introduce a dependency without explaining its maintenance and security cost.

## Community

Participation is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). General questions belong in GitHub Discussions when enabled; actionable defects and proposals belong in Issues.
