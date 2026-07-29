# Release process

Blueprints is pre-release. Releases should remain marked as prereleases until the v1.0 exit criteria are met.

## Before promotion

1. Ensure the milestone is complete or explicitly defer remaining issues.
2. Update `Roadmap.md`, user-facing docs, and dependency notices.
3. Run `./scripts/verify.sh` on a clean worktree.
4. Review security-sensitive changes and workspace compatibility.
5. Merge a promotion pull request from `develop` into `main`.
6. Confirm CI and CodeQL succeed on `main`.

## Version and tag

Use semantic versions. Until stable:

```text
v0.y.z-alpha.n
v0.y.z-beta.n
v0.y.z-rc.n
```

Create an annotated, signed tag when possible:

```sh
git tag -s v0.2.0-alpha.1 -m "Blueprints v0.2.0-alpha.1"
git push origin v0.2.0-alpha.1
```

The release workflow publishes platform archives and generates checksums for `v*` tags. It can also be started manually for packaging verification without creating a public release.

## Release notes

Include:

- user-visible additions and fixes;
- security or trust-boundary changes;
- workspace-format compatibility;
- known limitations;
- upgrade and recovery steps;
- artifact checksums.

## Post-release

- smoke-test each published archive;
- mark prerelease status correctly;
- move unfinished issues to the next milestone;
- open a roadmap issue for any release-specific regression.
