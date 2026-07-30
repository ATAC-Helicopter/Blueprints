# Security model

Blueprints uses signatures and explicit validation to detect unauthorized or incomplete changes in local project files. It is not a sandbox, password manager, access-control system, or replacement for operating-system permissions.

## Assets

- release plan integrity;
- membership authority;
- author identity and private signing key;
- sync manifest continuity;
- audit-history continuity;
- user control over incoming shared changes.
- canvas integrity and resistance to malicious rendering input.

## Trust boundaries

| Boundary | Treatment |
| --- | --- |
| Local workspace | Editable only while required content validates |
| Shared sync root | Untrusted exchange input until manifest and signatures validate |
| Local identity directory | Sensitive local state protected by OS permissions and key protection |
| Git/provider integration | Informational; not authoritative project truth |
| Source Lens proposals | Untrusted transient suggestions; no persistence before explicit approval |
| UI input | Validated by application workflows before persistence |
| Canvas layout | Signed shared node positions; entity references and coordinate bounds validated before rendering or saving |
| Canvas viewport | Unsigned machine-local preference; bounded and excluded from sync and trust decisions |

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
- A persisted canvas node must reference an existing signed project entity.
- Canvas coordinates, node count, project identity, schema, and uniqueness are bounded and validated.
- Local viewport values are bounded but cannot affect signed project truth or workspace trust.
- Invalid canvas signatures place the workspace in read-only untrusted state.
- Source discovery uses read-only local/GitHub commands and enforces count, line, and text bounds.
- Source apply validates the complete batch before persistence and requires trusted, conflict-free, mutable targets.
- GitHub CLI credentials remain outside Blueprints workspaces and settings.

## Important limitations

- A valid member key can sign malicious or incorrect content; signatures establish key possession, not intent.
- The current model does not provide key revocation, rotation, hardware-backed storage, or recovery.
- Linux/macOS key protection ultimately depends on file permissions protecting the local AES key.
- The audit chain detects many edits and deletions, but is not anchored to an external transparency service.
- Shared-folder availability, confidentiality, and rollback resistance depend on the underlying storage.
- Current workspace saves are not a single atomic transaction across all changed files.
- Canvas layout is a whole signed document; concurrent arrangements conflict as a unit and are not field-merged.
- Conflict resolution currently exposes low-level previews and supports whole-document choices.
- Blueprints does not encrypt project content.
- Duplicate detection is an exact normalized-title warning, not semantic identity proof.
- GitHub Project discovery currently sees project-linked repository issues, not standalone Project draft items.

## Reviewing security changes

Security-sensitive pull requests should include:

- the invariant affected;
- attacker capability being considered;
- failure and recovery behavior;
- adversarial automated tests;
- any workspace compatibility impact.

Report vulnerabilities using [SECURITY.md](../SECURITY.md).
