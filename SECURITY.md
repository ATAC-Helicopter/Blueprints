# Security policy

## Supported versions

Blueprints is pre-release. Security fixes target the latest commit on `develop` and are promoted to `main` with the next release candidate. No released version is currently covered by a long-term support commitment. The planned stable-version coverage and response targets are documented in [the support policy](docs/support-policy.md).

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability.

Use GitHub's **Report a vulnerability** form in the repository Security tab. Include:

- affected version or commit;
- reproduction steps or a proof of concept;
- the expected and observed security boundary;
- possible impact;
- any suggested mitigation.

If private vulnerability reporting is unavailable, contact the repository owner privately through the contact method on their GitHub profile.

You should receive an acknowledgement within seven days. Confirmed reports will be handled privately until a fix and disclosure plan are ready.

## Security-sensitive areas

Changes involving these areas need explicit review and adversarial tests:

- canonical serialization and detached signatures;
- private-key protection and identity storage;
- membership authority;
- shared-folder validation and conflict resolution;
- signed manifests and audit-chain continuity;
- recovery behavior after corrupt or untrusted input.

Read [the security model](docs/security-model.md) before changing these components.
Use [the threat model](docs/threat-model.md) to review attacker capabilities and residual risk.
