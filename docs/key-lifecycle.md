# Member key lifecycle

Blueprints 0.3 treats a member identity as the pair of a user ID and one Ed25519 key ID. Membership revisions are signed project truth. Accepted public keys are also retained as machine-local trust anchors so historical audit entries remain verifiable.

## Planned rotation

Before replacing a working key:

1. ensure the project has another active administrator;
2. create and back up the replacement local identity;
3. export its signed identity invitation;
4. have another administrator add the replacement identity with the required role;
5. exchange and validate at least one change from the replacement identity;
6. deactivate the old member record;
7. retain the old public trust anchor for historical verification and destroy old private-key copies only after recovery backups are confirmed.

Blueprints does not silently rewrite historical signatures. Deactivation prevents future membership-authorized use but does not make old signatures invalid retroactively.

## Lost or suspected-compromised keys

- Another active administrator should deactivate the affected member and invite a new identity.
- A replacement identity receives a new user ID and key ID. Blueprints does not claim continuity of private-key possession.
- If the only administrator key is lost, there is no in-app bypass. Restore the protected identity from backup or restore a project copy that still has an available active administrator.
- If a key may be compromised, stop exchanging changes until an administrator has reviewed the audit log, deactivated the member, and published a new membership revision.
- Shared-folder access control is not revocation. A deactivated user who can still write to the folder may cause denial of service, but their key does not regain membership authority.

## Recovery records

Keep identity backups separate from project backups. Project workspaces contain public keys, not private signing keys. Conflict recovery copies preserve documents, not identities.

Blueprints can export the current identity as an encrypted recovery file:

1. open **People** and enter a passphrase of at least 12 characters;
2. create the encrypted backup;
3. store the backup separately from project backups;
4. store the passphrase in a different trusted location;
5. test restoration on a clean operating-system user profile before relying on it.

The recovery file uses AES-256-GCM and authenticates the public identity metadata. Its key is derived with PBKDF2-SHA256 using a random salt and 600,000 iterations. Restore verifies that the decrypted private key matches the recorded public key, then protects it again using the destination device's configured local key protector.

Anyone with both the backup and passphrase can sign as that identity. Do not store them together. Restoring does not reactivate a member that an administrator already deactivated.

Automated same-user key rotation, platform-keystore migration beyond the current platform protectors, revocation timestamps, and hardware-backed keys remain future security work.
