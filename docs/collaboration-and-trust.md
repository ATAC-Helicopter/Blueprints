# Collaboration And Trust

## Collaboration Model

The finished Blueprints app is intended to support small-team collaboration without requiring a server.

The working model is:

- each user edits a local workspace
- a shared folder acts as the exchange point
- push publishes signed local changes
- pull imports and validates remote changes

Users should not work directly inside the live shared folder as their main workspace.

This model is intended to reduce:

- partial write problems
- accidental file interference
- lock contention
- silent overwrites

## Identity

Each device creates a local identity consisting of:

- `UserId`
- `DisplayName`
- an Ed25519 keypair

Private key storage is intended to use platform-appropriate secure storage:

- Windows: DPAPI
- macOS: Keychain
- Linux: encrypted file fallback

Public keys are stored in signed membership data so other project members can verify authorship.

## Membership

Membership is one of the most security-sensitive parts of the system.

The finished app is intended to store membership in `members.json`, including:

- membership revision
- member list
- public keys
- roles
- join timestamps

Rules the app is expected to enforce:

- membership is invitation-only
- invalid membership changes are rejected
- users cannot promote themselves
- users cannot change identity-relevant fields without authorization
- removed users cannot continue pushing accepted changes

Membership conflicts are never supposed to auto-merge.

## Signed Project Data

Critical project files are meant to be stored as signed JSON with detached signatures, including:

- `project.json`
- `members.json`
- every `version.json`
- every `item.json`
- audit log entries
- sync manifest data

The signing model exists so that the app can verify:

- who authored a change
- whether the content was altered after signing
- whether the author was authorized under the active membership revision

## Trust Evaluation

Trust should be evaluated whenever the state may have changed in a meaningful way.

Key validation points:

- project open
- pull
- push
- merge
- conflict resolution
- changelog generation when trust state is stale

The intended trust outcomes are:

- `Trusted`: signatures, structure, and authority checks pass
- `Untrusted`: something is signed but not authorized or not verifiable
- `Corrupt`: required files are missing, malformed, incomplete, or structurally inconsistent

## What Happens When Trust Fails

The finished app should not quietly ignore integrity problems.

Expected behavior:

- open the project read-only when safety cannot be guaranteed
- explain why the project is untrusted or corrupt
- show which file or rule failed validation
- allow investigation before any corrective action

For admins, the longer-term intent is to support inspection and controlled re-signing when a state is legitimate but needs to be re-established as trusted history.

## Shared Folder Safety

The shared folder itself is part of the trust story.

When a project is configured against a shared location, the app is intended to evaluate whether permissions are too broad.

Examples of warnings:

- `Everyone` or `Authenticated Users` has write access on Windows
- world-writable paths on Linux or macOS
- overly broad group write permissions where safety is unclear

The product direction is to surface a safety state such as:

- green
- yellow
- red

A red state should not be invisible because it changes the reliability assumptions of the whole collaboration model.

## Audit Trail

Blueprints is intended to keep an append-only, signed audit log.

Each log entry should capture:

- the entity that changed
- the change type
- a short summary
- the author
- the timestamp
- the membership revision seen at the time
- a hash link to the previous entry

The purpose of this log is to make tampering and rollback attempts visible rather than merely to provide activity history.

## Merge Rules

The merge model is meant to be conservative where trust matters and flexible where safe.

Auto-merge should succeed for cases such as:

- edits to different entities
- non-overlapping field changes
- independent additions

Conflicts should be raised for cases such as:

- concurrent edits to the same field
- incompatible state changes
- competing membership revisions

The core principle is that ambiguous states should be surfaced, not hidden.
