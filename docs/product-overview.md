# Product Overview

## What Blueprints Is

Blueprints is a local-first release planning application for developers and small teams. It is built around versions, changelog structure, signed project data, and file-based collaboration rather than around generic task boards.

The finished app is intended to help a team answer a small set of practical questions:

- What is going into the next release?
- What changed since the last release?
- Who made a change?
- Can this project state be trusted?
- Is the shared project safe to sync?

## What The Finished App Will Feel Like

Blueprints is meant to feel like a structured desktop tool for shipping software, not like a general-purpose work tracker.

The core experience is:

- create or open a project
- work inside release versions
- add and organize release items under changelog categories
- sync through a shared folder
- detect trust issues early
- export a release changelog when a version is ready

The app is intentionally designed for small technical teams that want local ownership over their project data.

## Primary Concepts

### Project

A project defines:

- project identity
- project code
- versioning scheme
- default changelog categories
- item types
- item key rules
- changelog export defaults
- member list and roles

Every project has a signed configuration so the rules that shape the workspace can be validated, not just displayed.

### Version

A version is the main planning container in Blueprints.

Each version has:

- a name such as `1.6.0`
- a release state
- optional notes
- an ordered set of release items

Versions move through a simple lifecycle:

- `Planned`
- `In Progress`
- `Frozen`
- `Released`

The intent is that teams plan by release target first, then by individual items inside that release.

### Item

An item is a changelog-oriented unit of work. It is not just a freeform card.

Each item belongs to:

- exactly one version
- exactly one category
- exactly one item type

Typical examples:

- a feature under `Added`
- a bug fix under `Fixed`
- a hardening change under `Security`

Items also carry a human-readable key such as `VS-1601` or `BUG-1042` so teams can refer to them in conversation and changelogs.

### Member

Projects are invitation-only. Every member has:

- a user ID
- a display name
- a public signing key
- a role

Roles are intentionally small in scope:

- `Viewer`
- `Editor`
- `Admin`

### Trust

Trust is a first-class part of the app.

When a project is opened or synced, Blueprints evaluates whether:

- signatures are valid
- the author was authorized
- required project files are intact
- the shared project state is structurally coherent

The app surfaces a trust result such as:

- `Trusted`
- `Untrusted`
- `Corrupt`

If trust cannot be established, the intended behavior is to make the problem visible and restrict unsafe actions.

## Main Areas Of The Finished App

The completed desktop app is expected to revolve around these main areas:

- project picker and recent workspaces
- main shell with project summary, trust badge, and sync status
- version list and version detail views
- item editor and categorized item lists
- members/settings screens for project configuration
- conflict resolution flows
- changelog preview and export flows

## Design Direction

The finished product is meant to be:

- structured rather than generic
- dense but readable
- desktop-first
- local-first
- explicit about integrity and risk

The UI should emphasize clarity over decoration. Trust, sync health, and release status should always be visible enough that a user understands the state of the project before making changes.
