# Storage and Trust

## Current Approach

The repository already implements the core of its tamper-evident file model:

- documents are serialized to canonical JSON
- signatures are detached and stored beside the document
- verification happens against the exact canonical bytes that were written

This logic lives in `Blueprints.Storage` and `Blueprints.Security`.

## Canonical JSON

`CanonicalJsonSerializer` exists to ensure a document always produces the same signable representation.

Its behavior is:

- serialize using camelCase property names
- avoid pretty-printing
- parse the generated JSON back into a node tree
- recursively sort object properties using ordinal string comparison
- preserve array order
- emit a stable JSON string

This matters because signatures are computed over bytes, not over object graphs. If two equivalent documents were serialized with different property ordering, signatures would fail even though the logical content was the same.

## Signed Document Store

`FileSystemSignedDocumentStore` provides the main persistence API:

- `Write<T>(documentPath, document, signingKey)`
- `Read<T>(documentPath, publicKey)`

Write flow:

1. Serialize the document to canonical JSON.
2. Convert the JSON string to UTF-8 bytes.
3. Sign those bytes with the provided private key material.
4. Write the JSON file.
5. Write a sibling `.sig` file.

Read flow:

1. Read the JSON file as UTF-8.
2. Deserialize it into the requested type.
3. Read the sibling `.sig` file.
4. Verify the signature against the supplied public key.
5. Return the document, canonical JSON, parsed signature, and validity result.

The store returns structured result records rather than just raw values:

- `SignedDocumentWriteResult`
- `SignedDocumentReadResult<T>`

## Signature Model

The detached signature format is represented by `DetachedSignature`:

- `Algorithm`
- `KeyId`
- `SignatureBase64`

Today the only supported algorithm is Ed25519.

`Ed25519SignatureService` enforces two checks before verifying the raw cryptographic signature:

- the declared algorithm must be `Ed25519`
- the signature key ID must match the provided public key ID

This gives the system a place to evolve key identity and algorithm handling without overloading the document schema itself.

## Key Material

The current key-related records are:

- `SignatureKeyPair`
- `SignatureKeyMaterial`
- `SignaturePublicKey`

`Ed25519KeyPairGenerator` creates a key pair keyed by a caller-provided `KeyId`. The repository currently treats keys as application-level inputs; there is not yet a complete key lifecycle or secure local identity enrollment flow in the app.

## Workspace Paths

`WorkspacePathResolver` and `WorkspacePaths` are minimal today. They only package:

- `LocalWorkspaceRoot`
- `SharedProjectRoot`

The more detailed workspace layout appears in [ImplementationPlan.md](../ImplementationPlan.md), including `project/`, `versions/`, `log/`, `sync/`, and `cache/` folders. Those structures are not yet created or enforced by the app.

## Trust States

User-facing trust state is currently modeled very simply:

- `Trusted`
- `Untrusted`
- `Corrupt`

`TrustStatePresenter` converts those enum values into UI text. There is not yet a complete trust engine that evaluates project continuity, membership authority, or conflict history; the current implementation focuses on individual document signature validity.

## What The Tests Prove

The test suite validates the most important current guarantees:

- canonical JSON output is stable
- Ed25519 signatures round-trip correctly
- signed documents can be written and read back successfully
- item key formatting behaves as expected

These tests make the current storage/security layer credible even though the broader product workflow is still under construction.
