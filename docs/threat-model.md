# Threat model

This document describes attacker goals, capabilities, trust boundaries, and residual risk for Blueprints. It complements the invariant-focused [security model](security-model.md).

## Security objectives

Blueprints aims to:

- preserve the integrity and authorship evidence of release plans;
- prevent untrusted shared-folder content from silently changing trusted local state;
- keep private signing keys and provider credentials out of projects and shared folders;
- preserve a recoverable previous workspace when a local mutation is interrupted;
- make conflicts, invalid signatures, and incomplete state visible before editing continues.

Blueprints does not promise confidentiality for project content, prevent an authorized member from signing harmful content, or make an untrusted storage provider available.

## Attacker capabilities

The design assumes an attacker may:

- read, add, replace, truncate, replay, or delete files in the shared exchange root;
- provide malformed JSON, oversized values, deep object graphs, invalid Base64, unknown schemas, and mismatched document/signature pairs;
- copy an older valid shared snapshot over a newer one;
- interrupt the process or exhaust disk space between filesystem operations;
- control repository content and hosted-provider response text;
- provide a malicious invitation, recovery file, VaultSync sidecar, integration path, or transaction marker;
- possess the valid signing key of a current or former project member;
- attempt to trick a user into approving the wrong provider or exchange operation.

The model does not assume resistance after the attacker has arbitrary code execution as the same operating-system user. Such an attacker can access application memory and act with the user's filesystem permissions.

## Trust boundaries and abuse cases

| Boundary | Attacker goal | Required treatment | Residual risk |
| --- | --- | --- | --- |
| Local workspace | Leave partially updated signed truth or audit history | Stage the complete mutation, append audit evidence in the staged copy, atomically promote it, and restore the prior directory after interruption | Filesystem or hardware failure may still damage both copies |
| Shared exchange | Inject, replay, delete, or partially copy project files | Bound paths and sizes, verify manifests and signatures, stage pulls, detect conflicts and continuity failures, retain rollback pairs | Storage can deny service or withhold newer data |
| Transaction marker | Redirect cleanup or recovery outside the workspace | Use deterministic sibling paths and require the bounded marker to match every resolved path exactly | Same-user arbitrary code execution can replace local state |
| Identity store | Steal or replace a private signing key | Apply platform/local key protection, restrict local files, verify restored private keys against public identity | macOS/Linux fallback protection depends on a local secret and filesystem permissions |
| Identity backup | Guess the passphrase or alter recovery metadata | AES-256-GCM, PBKDF2-SHA256 with 600,000 iterations, authenticated identity metadata, strict size bounds, and proof that the key matches the public identity | A weak user-chosen passphrase remains guessable offline |
| Invitations | Substitute a different member, project, or key | Signed proof of key possession and exact project/member binding; explicit administrator review | Users can still intentionally trust the wrong person |
| Hosted providers | Exfiltrate credentials or inject authoritative state | Environment-only credentials, bounded read contracts, explicit proposal approval, exact-target single-use write authorization | Provider data can mislead a user who approves it |
| Git worktree | Trigger code execution or unsafe history changes through repository operations | Keep discovery read-only; require explicit writes; use structured arguments; redirect hooks; avoid submodules; reject executable local filters/drivers/monitors; allow only fast-forward pull and ordinary push | Git, credential helpers, SSH agents, user-global Git configuration, and the local filesystem remain external dependencies |
| Canvas/UI input | Cause unsafe persistence or rendering resource exhaustion | Validate identities, counts, coordinate bounds, text bounds, and full batches before persistence | Extremely large otherwise-valid projects may still feel slow |
| Backup-health evidence | Claim a backup is healthy when it is not | Treat external health as bounded advisory evidence and keep it outside signed truth | Blueprints cannot independently prove external backup contents |

## Key compromise

Signatures prove possession of a key, not benign intent. A current authorized member can sign incorrect or malicious project content. Administrators must deactivate a suspected member, stop exchange while reviewing the audit trail, and remove that person's write access at the storage layer.

Deactivation prevents the key from authorizing future project mutations but intentionally does not invalidate historical signatures. Automated same-user rotation and time-qualified revocation remain planned work.

## Release security gates

Before a stable release:

1. run the complete cross-platform test and adversarial-input suites;
2. exercise interrupted workspace promotion and recovery;
3. restore an encrypted identity backup on a clean user profile;
4. perform the documented local and exchange recovery drill;
5. review changes to canonical serialization, signatures, membership, migration, sync, recovery, and provider approval boundaries;
6. commission independent review of the exact source and, once distribution begins, the exact downloadable artifacts.

Do not describe Blueprints or a release as audited unless the stated source and artifacts were actually included in that review.
