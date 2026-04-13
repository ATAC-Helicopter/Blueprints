# Release And Changelog Workflow

## Purpose

Blueprints is designed so release planning and changelog generation come from the same structured data model.

The finished app is intended to let a team manage a release in one place and then export a clean changelog without rebuilding that information by hand at the end.

## Release Structure

Each release version is expected to contain categorized items, for example:

- `Added`
- `Changed`
- `Fixed`
- `Removed`
- `Security`

Projects may customize labels, ordering, and visibility, but the overall model stays changelog-oriented.

## Item Keys

Human-readable item keys are intended to be first-class.

Examples:

- `VS-1567`
- `BUG-1042`
- `ISS-215`
- `SEC-8`

The planned default policy is:

- feature work uses version-scoped, project-prefixed keys
- bugs, issues, and security items use project-wide counters

This makes release-scoped work easier to read while preserving stable references for items that span versions.

Even when keys are visible to users, the true internal identity remains the stable item GUID.

## Planned Release Lifecycle

The intended version lifecycle is:

1. `Planned`
2. `In Progress`
3. `Frozen`
4. `Released`

Meaning of each state:

- `Planned`: the release exists but is not yet actively being built
- `In Progress`: active planning and execution are underway
- `Frozen`: normal editing is blocked while the release is reviewed or finalized
- `Released`: the version is complete and treated as immutable

The goal is for release status to drive actual behavior, not just labeling.

## Preparing A Release

Before release, users should be able to:

- review all categories in one place
- check which items are complete
- manually reorder items for final presentation
- inspect notes and release metadata
- confirm trust and sync status

The version detail view is intended to function as the editorial control surface for the release.

## Freezing

Freezing is meant to create a clear boundary before release.

Expected behavior:

- block normal edits
- make last-minute changes explicit
- support a signed override path for admins when an exception is necessary

This keeps the final release set stable enough to review and export confidently.

## Releasing

When a version is released, the app is intended to:

- set the release timestamp
- treat the version as immutable
- preserve the final ordered release content
- enable changelog export from that final state

If unfinished work remains, the release flow may offer to move it into a future version rather than leaving the released version open to ongoing edits.

## Changelog Export

Blueprints is intended to export Markdown changelogs directly from release data.

A typical result should look like:

```md
## [1.6.0] - 2026-02-28

### Added
- VS-1601 New dashboard

### Fixed
- BUG-1042 Crash on startup
```

Planned export options include:

- include only completed items by default
- include item keys
- include release date
- include descriptions
- use compact mode

Projects should be able to define their own default changelog behavior so the export matches team conventions without per-release reconfiguration.

## Why This Matters

Most teams currently rebuild release notes from scattered issue trackers, commit history, and memory.

The intended Blueprints workflow is different:

- plan the release in structured form
- keep the data attributable and trustworthy
- organize items in the order you want readers to see them
- generate the changelog from the release itself

That is the core product promise: release planning that naturally turns into release communication.
