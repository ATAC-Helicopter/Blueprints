# Blueprints complete user manual

Blueprints is a local-first desktop application for planning releases, organizing work, producing changelogs, and exchanging signed project changes with a team. It does not require a hosted Blueprints account. Your editable project lives in a folder you choose, and collaboration happens through a separate shared folder.

> [!IMPORTANT]
> Blueprints is pre-release software. The source workflow is usable, but supported installers are not available yet. Keep tested backups of your workspaces and identity before relying on the application for important projects.

## 1. Understand the data model

Blueprints keeps three kinds of data separate:

| Data | Purpose | Where it lives |
| --- | --- | --- |
| Local workspace | The editable project copy on this computer | A folder chosen when the project is created, opened, or joined |
| Shared folder | The exchange layer used to push and pull signed changes | A different folder, optionally on synchronized or network storage |
| Local identity | The private signing identity used to prove who changed project data | The operating system's local application-data directory |

The local workspace and shared folder must be different and must not contain one another. Do not use the shared folder as the editable workspace.

Core project concepts:

- **Project:** the complete signed planning workspace.
- **Release:** a planned version or milestone. Release states are Planned, In Progress, Frozen, and Released.
- **Work item:** one outcome assigned to a release. Progress states are Planned, In Progress, Review, and Complete.
- **Relationship:** a typed connection such as “blocks,” “depends on,” or “relates to.”
- **Identity:** a display name and Ed25519 key pair used to sign changes.
- **Trust:** the result of validating project documents, signatures, membership, and audit history.
- **Sync baseline:** the last shared state that this local workspace recorded.

## 2. Install and start Blueprints

### Requirements

- .NET 10 SDK `10.0.300` or a newer compatible patch.
- Git for repository operations and source-change diagnostics.
- On Linux, an accessible X11 or XWayland display.

### Run from source

```sh
git clone https://github.com/ATAC-Helicopter/Blueprints.git
cd Blueprints
./scripts/verify.sh
./scripts/run-app.sh
```

The verification script restores packages, builds the Release configuration, and runs the automated test suite. Equivalent commands are documented in the [development guide](development.md).

If Linux cannot open the window, run:

```sh
./scripts/diagnose-linux-display.sh
```

## 3. Complete first-time setup

### Create a signing identity

Every person needs one local identity before creating or joining a project.

1. Start Blueprints.
2. Enter the display name teammates should see.
3. Select **Create identity**.
4. Enter a passphrase of at least 12 characters.
5. Select **Back up…** and save the encrypted identity backup somewhere separate from the computer.

The identity is not a hosted account. Its private key stays on the device. On Windows it is protected with DPAPI. On macOS and Linux it is encrypted using machine-local protection material in the Blueprints application-data directory.

Losing the identity and its recovery material can leave an otherwise readable workspace impossible to continue as the same member. See [backup and disaster recovery](backup-recovery.md) before creating important project data.

### Choose a project option

The setup screen offers three paths:

- **Start a new project:** create a new signed workspace and become its first administrator.
- **Open a project:** validate and open an existing local workspace with its matching shared folder.
- **Join a team:** use a project invitation created for your identity.

Recent projects appear below these options. Selecting one fills its stored local and shared paths; it does not duplicate the project.

## 4. Create a new project

1. Open **Start a new project**.
2. Enter a clear project name.
3. Enter a short project code such as `APP`. Blueprints uses this code in readable item keys.
4. Enter the release naming convention, such as `SemVer`.
5. Choose the **Local project folder**.
6. Choose the **Shared folder**.
7. Select **Create project**.

Choose dedicated folders. Blueprints rejects overlapping roots and unsafe path arrangements. The shared folder may be local during individual use; it becomes useful when placed on an exchange location accessible to teammates.

After creation, Blueprints opens the project in **Overview** and records it in the recent-project list.

## 5. Open or join a project

### Open an existing project

1. Open **Open a project**.
2. Choose the existing **Local project folder**.
3. Choose its matching **Shared folder**.
4. Select **Validate and open**.

Blueprints validates the project before enabling edits. An invalid signature, missing required document, broken audit chain, unsafe shared folder, or unresolved conflict can open the project in read-only mode.

### Join with a project invitation

The invitation must target your current identity.

1. Give an administrator your signed identity invitation. You can export it from **Team** in any project where your identity is available.
2. The administrator imports your identity, adds you to the project, pushes the new membership revision, selects your member record, and exports a project invitation.
3. Transfer that project invitation through an authenticated channel.
4. On your computer, open **Join a team**.
5. Choose the project invitation file.
6. Choose an empty local project folder.
7. Confirm the shared folder from the invitation or choose the intended shared location.
8. Select **Check invitation and join**.

Blueprints stages the incoming project, checks the target identity, membership, signatures, project identity, and history, and only then promotes it to the selected local folder.

## 6. Learn the application layout

Once a project is open, the left navigation contains every primary destination:

| Destination | Use it for |
| --- | --- |
| **Overview** | Arrange releases and work on the interactive board |
| **Releases** | Manage release details, readiness, work items, and changelog export |
| **Imports** | Connect repositories, scan sources, review proposals, and inspect VaultSync health |
| **Sync** | Compare local and shared changes, push, pull, and resolve conflicts |
| **Team** | Manage members, roles, invitations, and identity backup |
| **Validation** | Inspect trust, storage boundaries, signatures, and recovery guidance |

The lower-left area shows sync status and the current identity. Open the identity menu to switch projects, open Validation, or delete the current Blueprints project data.

Keyboard navigation uses `Ctrl` on Windows/Linux and `Command` on macOS:

| Shortcut | Destination |
| --- | --- |
| `Ctrl/Command+1` | Overview |
| `Ctrl/Command+2` | Releases |
| `Ctrl/Command+3` | Imports |
| `Ctrl/Command+4` | Team |
| `Ctrl/Command+5` | Sync |
| `Ctrl/Command+6` | Validation |

## 7. Plan work in Overview

### Create a release

1. Select **Create release**.
2. Enter a version or milestone name such as `2.4.0` or `Summer launch`.
3. Select **Create release**.
4. Select the release in the left column and edit its name, state, and outcome in the details panel.

### Add a work item

1. Select the target release.
2. Select **Add work item**.
3. In **Work item details**, choose the progress state, work type, and changelog group.
4. Enter a concise outcome and useful context.
5. Select **Add item**.

Blueprints creates the stable item key automatically. A work item belongs to exactly one release. Its changelog group controls where it appears in generated release notes.

### Update progress

1. Select a work item in the center table.
2. Choose Planned, In Progress, Review, or Complete in the details panel.
3. Select **Save selected**.

A Complete progress state and the completed flag remain consistent for changelog generation.

### Search and filter

- Choose a release in the left column to limit the table to that release.
- Use **Search this release** to match item key, title, description, type, or changelog group.
- Use the progress filter to show All, Planned, In Progress, Review, or Complete items.
- Select a row to open its ordinary edit form on the right.

Search and progress filtering are local view state and do not create shared project changes.

## 8. Create and manage dependencies

Relationships are useful when release ownership alone does not explain how work is connected.

### Define a relationship type

1. Open the **Relationships** tab in Overview.
3. Enter a lowercase type ID, display name, meaning, and `#RRGGBB` color.
4. Enable **Directional** if order matters.
5. Select **Save relationship type**.

Examples include `blocks`, `depends-on`, and `relates-to`.

### Add a relationship

1. Select **New**.
2. Choose the relationship type.
3. Choose the source in **From** and the destination in **To**.
4. Enter an optional label.
5. Select **Save**.

Existing relationships appear as a normal selectable list. Self-links, missing endpoints, unknown types, and duplicate logical links are rejected. The complete relationship document is signed, audited, synchronized, and resolved as one document during a conflict.

## 9. Manage releases from start to finish

Open **Releases** for the detailed release workflow.

### Release states

| State | Meaning |
| --- | --- |
| Planned | The release is being defined |
| In Progress | Work is actively moving toward the release |
| Frozen | Scope and content are immutable |
| Released | The release is final and immutable |

Select a release in the left list to edit its name, state, and accomplishment notes. Frozen and Released releases cannot be edited or archived.

### Review release readiness

The readiness panel can report:

- missing or unavailable repository information;
- an uncommitted Git working tree;
- incomplete items;
- missing post-tag history;
- recent commits that do not reference a completed item key;
- missing, incomplete, stale, future-dated, risky, or healthy VaultSync recovery evidence.

Repository errors and dirty state are classified as **Blocking** readiness findings; incomplete scope and unmatched history are classified as **Attention**. Review findings before release because Blueprints will not silently change the repository or release scope. The availability of **Mark released** is governed separately by project trust, conflicts, member permissions, and release state. VaultSync evidence is advisory by default.

### Preview and export release notes

1. Select the release.
2. Choose whether to include incomplete items, item keys, descriptions, or compact formatting.
3. Select **Preview notes**.
4. Review the generated Markdown.
5. Select **Export notes** and choose the destination.

When a repository is linked, recent commit subjects and matching item keys can appear as source context.

### Release or archive

- Select **Mark released** only after reviewing readiness and notes. Released content is immutable.
- Select **Archive version…** twice to archive an eligible draft release.
- Select **Archive item…** twice to archive an eligible Planned or In Progress item.

Archived content leaves the active plan and is retained in a machine-local signed recovery area under `.blueprints/archive/`. Archival is synchronized as a project change.

## 10. Connect repositories and import work

Open **Imports** to connect source repositories and turn existing project signals into reviewed work items.

### Connect an existing repository

1. Select **Choose folder…**.
2. Choose a local Git worktree.
3. Select it from the repository list.

Up to eight worktrees can participate in combined discovery.

### Clone a repository

1. Open **Source settings**.
2. Enter the remote URL, destination, and optional folder name.
3. Select **Clone and connect**.

Authentication is delegated to the existing Git credential helper or SSH agent. Blueprints does not initialize submodules.

### Run explicit Git operations

The repository actions are independent of importing:

- **Pull latest:** requires a clean worktree and accepts fast-forward changes only.
- **Commit all:** stages tracked, untracked, and deleted files, then creates one commit from the supplied message.
- **Push commits:** performs an ordinary non-force push of the current named branch.

Blueprints does not rewrite history, force-push, resolve Git conflicts, create branches or tags, or run repository hooks. Review the worktree before using **Commit all**.

### Scan and approve imports

1. Select a connected repository.
2. Select **Scan for work**.
3. Search or filter the proposal list.
4. Select a proposal and review its title, context, source, target release, type, changelog group, and completion state.
5. Use **Include in import**, **Select shown**, or **Select all** deliberately.
6. Choose a fallback release for entries with no release hint when necessary.
7. Apply the approved proposals.

Scanning is read-only. Proposals are temporary and do not become signed project data until the final apply action. Exact normalized title matches are shown as possible duplicates and excluded by default.

Supported inputs include local changelog and roadmap Markdown, GitHub issues/pull requests/releases/Projects, and GitLab issues/merge requests/releases/milestones. Public provider data can be read anonymously. Private data requires a read-only process environment variable:

```sh
export BLUEPRINTS_GITHUB_TOKEN="your-read-only-token"
export BLUEPRINTS_GITLAB_TOKEN="your-read-only-token"
./scripts/run-app.sh
```

Tokens are read for the current process and are not stored in project or integration settings. See [Imports and source discovery](source-lens.md) for limits, provenance, provider behavior, and security boundaries.

## 11. Manage the team and identities

Open **Team** to manage membership and local identity operations.

Roles:

| Role | Intended access |
| --- | --- |
| Viewer | Read project data without project mutation |
| Editor | Edit permitted project content |
| Admin | Manage members and perform administrative changes |

### Add a member

1. Ask the new member to export an identity invitation.
2. Select **Import signed identity invitation…**.
3. Verify the displayed user ID, name, and public key through a trusted channel.
4. Choose the role.
5. Select **Invite member**.
6. Push the membership change from **Sync**.
7. Select the new member and export a targeted project invitation.

### Change a member

1. Select the member.
2. Change the display name, role, or Active state.
3. Select **Save member**.
4. Push the change.

At least one active administrator must remain. Deactivating a member does not rewrite historical signatures. Key rotation and loss procedures are documented in [member key lifecycle](key-lifecycle.md).

### Back up the current identity

Enter a passphrase of at least 12 characters and select **Create encrypted backup…**. Store the file and passphrase separately. Test restoration before relying on it.

## 12. Sync safely with a team

The **Sync** screen compares three states: local workspace, shared folder, and the last recorded sync baseline.

### Normal workflow

1. Select **Refresh comparison**.
2. Review outgoing and incoming counts, sync evidence, and validation results.
3. Select **Push** to publish valid outgoing signed changes.
4. Select **Pull** to validate and apply incoming signed changes.
5. Refresh again and confirm there are no unexpected pending changes.

Push writes signed documents and an updated signed manifest. Pull checks incoming signatures, project identity, membership, manifest continuity, audit history, and conflicts before applying anything.

### Resolve a conflict

A conflict occurs when local and shared copies changed differently from the same baseline.

1. Select a conflict in the results list.
2. Read the semantic field comparison.
3. Inspect the bounded local and shared raw previews if needed.
4. Choose **Keep local** or **Accept shared**.
5. Refresh, then push or pull as required.

Blueprints preserves both available document/signature pairs under `.blueprints/recovery/conflicts/` before applying the choice. Schema 1 resolves the affected signed document as a whole; individual fields are not merged automatically.

Do not edit signed JSON or delete signatures to bypass a conflict.

## 13. Understand Validation and read-only mode

Open **Validation** to see the current trust result, storage paths, and individual checks.

| State | Result |
| --- | --- |
| Trusted | Normal actions are available, subject to role and lifecycle rules |
| Untrusted | One or more signatures or trust requirements failed; mutation is disabled |
| Corrupt | Required data or audit continuity is missing or malformed; mutation is disabled |

Select **Run validation again** after restoring files or correcting an external storage problem. Validation does not repair or overwrite data.

Common read-only causes include:

- a document changed without its matching signature;
- a required document or signature is missing;
- audit history is incomplete;
- local and shared roots overlap or fail a safety check;
- an unresolved sync conflict exists;
- the current member's role or active status does not allow mutation;
- the selected release is Frozen or Released.

Preserve both local and shared folders before recovery. Restore a JSON document together with the `.sig` from the same known-good snapshot.

## 14. Inspect VaultSync recovery evidence

VaultSync support is optional and passive.

1. Open **Imports** and then **Source settings**.
2. Enter a VaultSync destination, `.vaultsync` directory, `meta` directory, or exact `vaultsync.meta.db` path.
3. Select **Save health link**.
4. Refresh integration status.

Blueprints confirms the documented metadata location without parsing the VaultSync SQLite schema. If a sibling `blueprints.status.json` exists, it can display snapshot, backup, verification, restore-readiness, index, conflict, and warning evidence.

Blueprints does not start backups, verify backup payloads, or treat VaultSync as authority for project trust. Select **Register shared folder** twice to create the project-specific exchange marker after reviewing the exact target. Registration does not automatically switch the open project to that path.

See [VaultSync integration](vaultsync-integration.md) for the exact contract.

## 15. Back up and recover data

Back up all three boundaries separately:

1. the complete local workspace;
2. the complete Blueprints application-data directory, including `Identities/` and `Security/`;
3. the complete shared project folder.

For a consistent backup:

1. Complete or cancel the current edit.
2. Refresh Sync and record the manifest status.
3. Close Blueprints.
4. Copy all three boundaries to versioned backup storage.
5. Verify that project documents have matching signatures and that identity protection material is present.

Never place private identity files or provider tokens in the project or shared folders. Practice restoration on a disposable copy. The complete procedure is in [backup and disaster recovery](backup-recovery.md).

## 16. Switch or remove projects

Open the identity menu in the lower-left corner.

- **Switch project** returns to setup without deleting project data.
- **Project validation** opens the trust screen.
- **Delete project…** opens a persistent confirmation dialog that shows the project code, local path, and shared path before anything is removed.

Deletion validates that the selected local and shared roots belong to the expected project. It refuses broad paths, overlapping roots, symbolic-link roots, and paths inside Git worktrees. Linked repositories and source files are not deleted. Back up the project first; deletion is not an archive operation.

The setup screen also allows deletion of a selected recent project through the same confirmation dialog and safety checks. Select **Cancel** to leave all data untouched or **Delete project permanently** to complete the operation.

## 17. Accessibility and keyboard use

- Use `Tab` and `Shift+Tab` to move through controls.
- Primary destinations have text labels and accessible names.
- Release and work-item lists support normal keyboard selection.
- Work-item progress is changed through the labeled Progress field.
- Destructive actions use explicit labels and confirmation.
- Status is communicated with text and not color alone.

See [accessibility](accessibility.md) for the full shortcut set and current qualification status.

## 18. Troubleshooting quick reference

| Problem | First action |
| --- | --- |
| Project opens read-only | Open Validation and review the first failing check |
| Push or pull is disabled | Refresh comparison; check trust, role, conflicts, and release state |
| Work items appear missing | Clear the Overview search and progress filter, then confirm the selected release |
| A work item is missing | Clear search and filters, then refresh and fit the view |
| Import finds nothing | Confirm the repository selection, supported source files, provider remote, and token scope |
| Git pull fails | Clean the worktree and confirm the update can fast-forward |
| Identity cannot be restored | Restore both the encrypted identity and its required protection material |
| Linux window does not open | Run `./scripts/diagnose-linux-display.sh` |

Never solve a trust error by deleting signatures or manually editing signed JSON. See [troubleshooting](troubleshooting.md) for supported diagnostics.

## 19. Current limitations

- Supported installers and production-ready packaging are not available yet.
- Automated desktop end-to-end UI testing is not yet part of the suite.
- Conflict resolution chooses one complete signed document; it does not merge individual fields.
- Same-user key rotation and time-qualified revocation are not automated.
- Source discovery is read-only for hosted providers; Blueprints does not update issues or project boards.
- Git operations do not initialize submodules, force-push, rewrite history, resolve conflicts, or create branches/tags.
- VaultSync health is advisory and does not prove payload recovery.

## 20. Glossary

| Term | Definition |
| --- | --- |
| Audit entry | A signed, hash-linked record describing one project mutation |
| Changelog group | The category used to organize a work item in release notes |
| Conflict | A path changed differently in local and shared state since the baseline |
| Detached signature | The `.sig` file that authenticates its sibling project document |
| Exchange manifest | The signed inventory and version for a shared publication |
| Local view state | Unsigned machine-local search, filters, zoom, viewport, and view preferences |
| Project invitation | A signed invitation targeted to one identity and project membership revision |
| Shared layout | Signed positions for project, release, and item nodes |
| Source proposal | Temporary imported information that has not yet become project data |
| Trusted workspace | A workspace whose required documents, signatures, membership, and history validate |

## 21. Reference documentation

- [Documentation index](README.md)
- [Imports and source discovery](source-lens.md)
- [Workspace format](workspace-format.md)
- [Security model](security-model.md)
- [Threat model](threat-model.md)
- [Backup and disaster recovery](backup-recovery.md)
- [Troubleshooting](troubleshooting.md)
- [Development guide](development.md)
