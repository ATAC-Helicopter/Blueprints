# Backup and disaster recovery

Blueprints separates editable project state, signing identities, and the shared exchange layer. A usable recovery plan must account for each one explicitly.

## What to back up

Back up:

1. every chosen local project workspace;
2. the complete Blueprints local application-data directory, especially `Identities/` and `Security/`;
3. the shared project directory if it is not already protected by versioned storage;
4. signed identity and project invitation files while onboarding is in progress.

The application-data root is the operating system's local application-data directory plus `Blueprints`. The local project path and shared exchange path are shown in the application.

Private signing keys exist only in the identity directory. On macOS and Linux, restoring an encrypted private key without the matching `Security/local-private-key-protector.key` does not restore the identity.

## Create a consistent backup

1. Finish or cancel the current edit.
2. Refresh the exchange view and record its manifest and audit status.
3. Close Blueprints so no workspace save is in progress.
4. Copy the local workspace and the complete Blueprints application-data directory to versioned backup storage.
5. Back up the shared exchange directory separately.
6. Verify that the backup contains document/signature pairs and protected identity files.
7. Record the backup time and the last pulled/pushed/shared manifest versions.

Do not place private identity data inside the project workspace or shared exchange folder.

## Restore a local workspace

1. Keep the damaged copy; do not overwrite it in place.
2. Restore the Blueprints application-data directory, including identities and key-protection material.
3. Restore the project workspace to a new local path.
4. Start Blueprints and open the restored local path with the intended shared exchange path.
5. Confirm the identity, project ID, trust state, audit chain, membership revision, and manifest evidence before editing.
6. If the shared location is newer, pull only after its manifest and audit checks are green.

If the protected identity cannot be restored, follow [member key lifecycle](key-lifecycle.md). Blueprints has no last-administrator bypass.

## Restore the shared exchange

1. Stop all team members from pushing or pulling.
2. Preserve the damaged shared directory.
3. Restore one complete known-good shared snapshot, including `project/`, `versions/`, `log/`, `manifest/`, and signatures.
4. Have an administrator compare its manifest version with each member's last locally recorded version.
5. Reject a restored snapshot that rolls the manifest backward unless every member deliberately resets from the same reviewed backup.
6. Reopen and refresh from one trusted local workspace before allowing the team to resume.

The `packs/` directories help diagnose individual publications, but they are not guaranteed to be complete project backups.

## Recover from a conflict choice

Before applying a conflict choice, Blueprints stores both available sides under:

```text
.blueprints/recovery/conflicts/<recovery-id>/
```

`resolution.json` identifies the path, selected side, presence of each document/signature, and outcome. To reverse a choice, close Blueprints, back up the current workspace, restore the desired JSON and `.sig` pair to its recorded relative path, then reopen and validate. If the restored side differs from the shared baseline, expect a new explicit conflict.

Incoming deletions retain the previous local pair in the batch inbox recovery directory and add a `.deleted` marker.

## Recovery drill

Before relying on a backup policy, perform a drill on a disposable copy:

- restore the identity and protection material;
- open a restored workspace;
- validate audit and trust status;
- export a changelog;
- join a second disposable workspace through an invitation;
- push and pull one signed change;
- confirm a tampered copy is rejected.
