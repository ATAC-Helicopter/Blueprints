# Architecture

## Overview

Blueprints is organized as a small .NET solution with clear project boundaries:

- `Blueprints.App`: desktop UI and presentation logic
- `Blueprints.Core`: domain contracts for projects, versions, items, membership, and trust state
- `Blueprints.Storage`: JSON serialization and file persistence for signed documents
- `Blueprints.Security`: key generation, signing, verification, and trust-oriented display helpers
- `Blueprints.Collaboration`: sync status models used by the UI
- `Blueprints.Tests`: unit tests around the implemented infrastructure

The intended product architecture is local-first. Users work from a local workspace, exchange data through a shared folder, and rely on signatures to detect tampering. That full workflow is documented in [ImplementationPlan.md](../ImplementationPlan.md), but only part of it is implemented today.

## Current Runtime Shape

At the moment, the application is best understood as three implemented layers and one illustrative UI:

1. Domain layer
   Defines the records and enums that describe projects, versions, items, members, release state, and trust state.
2. Security and storage layer
   Serializes documents into canonical JSON, signs the serialized bytes, writes a detached signature, and verifies that signature on read.
3. Presentation layer
   Shows sample project, identity, sync, and version summary data in the Avalonia desktop shell.
4. Test layer
   Proves the signing and persistence primitives behave as expected.

## Project Boundaries

### `Blueprints.App`

The UI project references all other library projects and currently contains:

- application bootstrap in `Program.cs` and `App.axaml.cs`
- `MainWindowViewModel` with sample `ProjectSummary`, `IdentitySummary`, `SyncSummary`, and `VersionSummary` instances
- view wiring through Avalonia and a `ViewLocator`

This project does not yet orchestrate workspace loading, sync, editing, or trust validation against real project files.

### `Blueprints.Core`

This project contains the stable vocabulary for the rest of the solution:

- project configuration
- versions and release states
- items and changelog-related metadata
- membership and roles
- trust and item-key scope enums
- `ItemKeyFormatter` for project-scoped and version-scoped human-readable IDs

`Blueprints.Core` currently has no external package dependencies and acts as the center of the solution.

### `Blueprints.Storage`

The storage project provides two main pieces:

- `CanonicalJsonSerializer`
  Serializes objects using camelCase JSON, recursively sorts object properties, and produces a stable canonical representation for signing.
- `FileSystemSignedDocumentStore`
  Writes the canonical JSON to disk, creates a detached `.sig` file next to it, and verifies that signature when the document is read back.

This layer depends on `Blueprints.Core` and `Blueprints.Security`.

### `Blueprints.Security`

The security project implements:

- `Ed25519KeyPairGenerator`
- `Ed25519SignatureService`
- security model records such as `DetachedSignature`, `SignatureKeyPair`, and `SignaturePublicKey`
- `TrustStatePresenter` for UI-friendly trust labels

The signing implementation uses `NSec.Cryptography` and currently models a straightforward single-signer flow.

### `Blueprints.Collaboration`

This project is intentionally thin right now. It contains:

- `SyncHealth`
- `SyncSummary`

These types are consumed by the app shell to represent whether sync is idle, ready, or needs attention.

## Implemented Data Flow

The core implemented persistence flow is:

1. A domain document is created in memory.
2. `CanonicalJsonSerializer.Serialize` converts it into canonical JSON.
3. `Ed25519SignatureService.Sign` signs the UTF-8 bytes of that canonical JSON.
4. `FileSystemSignedDocumentStore.Write` writes:
   - the JSON document
   - a detached `.sig` file containing algorithm, key ID, and base64 signature
5. `FileSystemSignedDocumentStore.Read` re-loads both files and verifies the signature using the supplied public key.

This is the most concrete architectural capability in the repository today.

## Planned But Not Yet Implemented

The design documents describe a broader system that is only partially reflected in code:

- local workspace and shared sync folder separation
- `members.json` authority management in the app flow
- version and item editing workflows
- changelog export
- audit log continuity
- sync manifests, packs, inbox/staging, and conflict handling
- invitation-only membership onboarding

Those features should be treated as roadmap/design intent rather than current behavior.
