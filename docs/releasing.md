# Release process

Blueprints uses releases as lightweight milestone records while the product is pre-release. A milestone release records what was accomplished; it does not publish installers or application archives.

## Before promotion

1. Ensure the milestone is complete or explicitly defer remaining issues.
2. Update `Roadmap.md`, user-facing docs, and dependency notices.
3. Run `./scripts/verify.sh` on a clean worktree.
4. Review security-sensitive changes and workspace compatibility.
5. Create a promotion branch from `main`, apply the verified `develop` diff, and merge that protected pull request into `main`.
6. Confirm CI and CodeQL succeed on `main`.

## Milestone versions

Each roadmap milestone maps to one semantic version:

```text
v0.1 milestone -> v0.1.0
v0.2 milestone -> v0.2.0
v0.3 milestone -> v0.3.0
v0.4 milestone -> v0.4.0
v0.5 milestone -> v0.5.0
v0.6 milestone -> v0.6.0
```

Use SemVer prerelease identifiers for honest intermediate checkpoints, for example `v0.2.0-alpha.2`. Use the clean `v0.x.0` form only when that roadmap milestone's exit criteria are complete. GitHub records remain marked as prereleases until v1.0.

Before tagging:

1. close or explicitly defer every milestone issue;
2. move the relevant `Unreleased` changelog entries into `## 0.x.0 — YYYY-MM-DD`;
3. add the version and its major accomplishments to [release history](releases.md);
4. promote the exact milestone tree to `main`;
5. confirm CI and CodeQL are green on `main`.

Create an annotated, signed tag from `main` when possible:

```sh
git switch main
git pull --ff-only
git tag -s v0.2.0-alpha.2 -m "Blueprints v0.2.0-alpha.2 — interactive workspace"
git push origin v0.2.0-alpha.2
```

The milestone workflow verifies that the tag belongs to `main`, requires a matching changelog heading, extracts that section, and creates a GitHub prerelease with no attached binaries.

## Release notes

Include:

- user-visible additions and fixes;
- security or trust-boundary changes;
- workspace-format compatibility;
- known limitations;
- upgrade and recovery steps;
- deferred work that moved to the next milestone.

## Post-release

- mark prerelease status correctly;
- verify the GitHub release has no accidental binary assets;
- move unfinished issues to the next milestone;
- open a roadmap issue for any release-specific regression.
