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
| Project invitation | Out-of-band signed trust bootstrap targeted to one local identity |
| Local project trust anchors | Machine-local accepted member keys; never sourced from unverified shared data |
| Git/provider integration | Informational; not authoritative project truth |
| VaultSync metadata and health sidecar | Untrusted, bounded, read-only recovery evidence; never Blueprints trust authority |
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
- Identity invitations prove possession of their included private key.
- Project invitations bind the target identity, inviter administrator, project, membership revision, and initial member-key set.
- A join is staged and fully validated before the final local workspace is created.
- Documents, manifests, and audit entries are verified against their signer key ID in the locally trusted project-key set.
- Current workspace and incoming project content require an active Editor or Admin key; inactive and Viewer keys remain usable only for historical audit verification.
- Invalid incoming signatures block pull.
- Pull copies incoming data to a local inbox, rechecks signatures and manifest hashes there, snapshots rollback pairs, and only then mutates the local workspace.
- Invalid or missing signed local content produces untrusted or corrupt read-only state.
- Conflicting concurrent changes block further mutation.
- Whole-document conflict choices preserve both available sides in machine-local recovery storage before mutation.
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
- GitHub API credentials are accepted only through the process environment variable `BLUEPRINTS_GITHUB_TOKEN` and remain outside Blueprints workspaces and settings.
- GitLab API credentials follow the same isolation rule through `BLUEPRINTS_GITLAB_TOKEN`.
- Hosted-provider reads are allowed directly. Any future write must have a fresh, exact-target, single-use approval with a maximum ten-minute lifetime; credentials alone never authorize a write.
- VaultSync passive awareness resolves only documented paths, does not parse the metadata SQLite database, and limits the optional health sidecar to 1 MiB, schema depth 16, and 32 distinct warnings.
- VaultSync paths and health evidence are machine-local integration state. They do not enter signed workspaces, manifests, audit history, or release semantics.
- VaultSync exchange registration requires a fresh exact-project and exact-destination approval with a maximum ten-minute lifetime. Approval is single-use; registration enforces the canonical contained path, rejects internal directory links and unexpected content, and atomically creates only a bounded marker.
- VaultSync release readiness treats missing, stale, future-dated, and producer-reported risk as advisory attention. It does not infer that payloads were verified, elevate project trust, or silently block a release.

## Important limitations

- A valid member key can sign malicious or incorrect content; signatures establish key possession, not intent.
- The current model does not provide key revocation, rotation, hardware-backed storage, or recovery.
- Linux/macOS key protection ultimately depends on file permissions protecting the local AES key.
- The audit chain detects many edits and deletions, but is not anchored to an external transparency service.
- Shared-folder availability, confidentiality, and rollback resistance depend on the underlying storage.
- Current workspace saves are not a single atomic transaction across all changed files.
- Canvas layout is a whole signed document; concurrent arrangements conflict as a unit and are not field-merged.
- Conflict resolution provides semantic summaries for known document types but still operates on whole documents rather than merging individual fields.
- Conflict recovery copies are local, unsigned operational records; protect and back them up according to the sensitivity of project content.
- Blueprints does not encrypt project content.
- Duplicate detection is an exact normalized-title warning, not semantic identity proof.
- VaultSync health freshness is reported from supplied timestamps; passive awareness does not independently verify backup payloads or destination contents.

## Reviewing security changes

Security-sensitive pull requests should include:

- the invariant affected;
- attacker capability being considered;
- failure and recovery behavior;
- adversarial automated tests;
- any workspace compatibility impact.

Report vulnerabilities using [SECURITY.md](../SECURITY.md).

Operational behavior for planned rotation, compromise, and lost keys is defined in [member key lifecycle](key-lifecycle.md). Automated same-user rotation and revocation remain unimplemented.
