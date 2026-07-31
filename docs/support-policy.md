# Support policy

Blueprints remains pre-release until the v1.0 release criteria are complete. This document defines the support target being qualified; it is not yet a claim that preview builds are production-supported.

## Planned v1.x platform tiers

| Tier | Platforms | Commitment |
| --- | --- | --- |
| Supported desktop | Windows 11 x64/Arm64, macOS 14 or newer on Apple silicon and Intel, Ubuntu 24.04 LTS x64 | Clean install/launch, core workflow, upgrade, identity recovery, and two-user exchange are release-tested |
| Best effort | Other current x64/Arm64 Linux distributions with compatible .NET and desktop dependencies | Bugs are accepted, but every distribution is not release-tested |
| Unsupported | End-of-life operating systems, mobile platforms, web browsers, and network filesystems that cannot provide normal file replacement semantics | No compatibility or recovery guarantee |

The exact minimum versions may move before v1.0 when an operating system or runtime reaches end of support. A stable release will record its final matrix in its release notes.

## Supported versions

Once v1.0 is published:

- the latest v1.x minor release receives security and correctness fixes;
- the immediately previous minor release receives critical security fixes for at least 90 days after its successor;
- preview builds receive fixes only through a newer preview;
- workspace migration support is maintained from every stable v1.x schema to the current v1.x schema.

Breaking workspace or extension-contract changes require a new major version.

## Response targets

These are project targets, not a paid service-level agreement:

| Severity | Acknowledge | Initial assessment | Fix or mitigation target |
| --- | ---: | ---: | ---: |
| Critical: key disclosure, trusted-state bypass, or unauthenticated arbitrary file mutation | 2 business days | 3 business days | 7 days |
| High: integrity or authorization weakness requiring meaningful user interaction | 3 business days | 7 days | 30 days |
| Moderate/low security issue | 7 days | 14 days | Next appropriate release |
| Data-loss defect without a security boundary bypass | 3 business days | 7 days | 30 days |

Complex reports may require more time. The maintainer should communicate changed expectations privately rather than leaving a reporter without an update.

## Release qualification matrix

Every supported desktop target must exercise:

- clean launch and first-run identity creation;
- encrypted identity backup and clean-profile restore;
- create, reopen, edit, freeze, release, preview, and export;
- schema inspection, migration backup, failure rollback, and future-schema rejection;
- two distinct identities exchanging changes;
- tampered, replayed, incomplete, and conflicting shared input;
- interrupted local transaction recovery;
- local and exchange backup restoration;
- keyboard navigation, visible focus, scaling, and a basic screen-reader smoke test;
- upgrade and uninstall behavior once distribution work begins.

CI compilation alone is not platform qualification. Results should be retained with the release record.
