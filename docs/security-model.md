# Security model

Blueprints uses signatures and explicit validation to detect unauthorized or incomplete changes in local project files. It is not a sandbox, password manager, access-control system, or replacement for operating-system permissions.

## Assets

- release plan integrity;
- membership authority;
- author identity and private signing key;
- sync manifest continuity;
- audit-history continuity;
- user control over incoming shared changes.

## Trust boundaries

| Boundary | Treatment |
| --- | --- |
| Local workspace | Editable only while required content validates |
| Shared sync root | Untrusted exchange input until manifest and signatures validate |
| Local identity directory | Sensitive local state protected by OS permissions and key protection |
| Git/provider integration | Informational; not authoritative project truth |
| UI input | Validated by application workflows before persistence |

## Cryptography

- Project documents use detached Ed25519 signatures through NSec.
- Canonical JSON makes signed bytes deterministic.
- Windows protects private keys with current-user DPAPI.
- macOS and Linux use AES-GCM with a random local 256-bit protection key stored with user-only permissions where supported.
- Audit entries form a signed, SHA-256 hash-linked sequence.

## Security invariants

- Private keys are never written into a workspace or shared root.
- Invalid incoming signatures block pull.
- Invalid or missing signed local content produces untrusted or corrupt read-only state.
- Conflicting concurrent changes block further mutation.
- Only an active Admin may change membership.
- At least one active Admin must remain.
- Released versions reject further version and item edits.
- Local and shared roots may not overlap.

## Important limitations

- A valid member key can sign malicious or incorrect content; signatures establish key possession, not intent.
- The current model does not provide key revocation, rotation, hardware-backed storage, or recovery.
- Linux/macOS key protection ultimately depends on file permissions protecting the local AES key.
- The audit chain detects many edits and deletions, but is not anchored to an external transparency service.
- Shared-folder availability, confidentiality, and rollback resistance depend on the underlying storage.
- Current workspace saves are not a single atomic transaction across all changed files.
- Conflict resolution currently exposes low-level previews and supports whole-document choices.
- Blueprints does not encrypt project content.

## Reviewing security changes

Security-sensitive pull requests should include:

- the invariant affected;
- attacker capability being considered;
- failure and recovery behavior;
- adversarial automated tests;
- any workspace compatibility impact.

Report vulnerabilities using [SECURITY.md](../SECURITY.md).
