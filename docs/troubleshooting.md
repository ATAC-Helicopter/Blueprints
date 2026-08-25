# Troubleshooting

This guide covers supported diagnostic and recovery steps. Do not delete signatures or edit signed JSON to make an error disappear.

## The workspace opens read-only

Check the trust summary and open **Validation**.

Common causes:

- a signed JSON document changed without a matching signature;
- a required document or signature is missing;
- the audit chain is incomplete;
- local and shared roots overlap or fail a safety check;
- local and shared copies changed from the same baseline.

Preserve both roots before attempting recovery. Restore the affected document and its sibling `.sig` from the same known-good snapshot.

## The canvas arrangement resets

Look for `project/canvas-layout.json` and `project/canvas-layout.sig`.

- If neither exists, the workspace is using the compatible default arrangement. Move a node or select **Save layout**.
- If both exist, open **Validation**. An invalid layout signature makes the workspace read-only.
- If another collaborator pushed a layout, pull it through the normal sync workflow.
- **Auto arrange** intentionally replaces the saved arrangement with deterministic defaults.
- If only zoom or scroll position resets, inspect `.blueprints/canvas-view.json`; malformed local view state safely falls back to defaults.

Never copy only `canvas-layout.json`; its detached signature must come from the same save.

## A layout conflict blocks editing

Open **Sync** and select `project/canvas-layout.json`.

- **Keep Local** keeps your complete arrangement and publishes it on the next push.
- **Accept Shared** replaces your complete arrangement with the shared copy.

Schema 1 cannot merge individual node positions. Back up both document/signature pairs if both arrangements matter.

## A node is missing

Select **Refresh**, then **Auto arrange** if necessary.

The canvas is projected from signed project entities. A layout entry alone cannot create a node. If a version or item is absent from the signed workspace, its layout entry is rejected or ignored by the projection.

## The wrong node is selected after refresh

Select the node again. Workspace mutation reloads signed state from disk, and a removed or replaced entity cannot remain selected.

## Push or pull is disabled

1. Select **Refresh comparison** in Sync.
2. Open **Validation** and confirm the workspace is Trusted.
3. Check for unresolved conflicts.
4. Confirm the local identity is an active project member with the required role.
5. Confirm the local and shared folders are reachable and still refer to the same project.

Pull requires trusted incoming documents and a valid manifest. Push additionally requires a workspace that the current member can mutate. A Viewer can inspect project and sync state without publishing edits.

## A sync conflict appears

Select the conflict and compare its semantic fields before reading the raw previews. Choose **Keep local** only when the local complete document is authoritative; choose **Accept shared** only when the shared complete document is authoritative.

Blueprints saves both available sides under `.blueprints/recovery/conflicts/` before applying the resolution. There is no field-level merge in schema 1. Back up both roots before resolving important membership or project-configuration conflicts.

## Repository scanning finds no work

Check the following:

- the selected folder is a Git worktree;
- changelog or roadmap files use a supported common filename and are in the repository root or `docs/`;
- the `origin` remote points to `github.com` or `gitlab.com` for hosted discovery;
- a private repository token is present in the Blueprints process environment;
- the token is read-only but has access to the requested repository and source types;
- the warning area does not report provider timeout, response-size, or count limits.

Local Markdown discovery remains available when a hosted provider cannot be reached. A successful scan does not write project data until proposals are explicitly applied.

## A Git action fails

- **Pull latest** requires a clean worktree and a fast-forward update.
- **Commit all** requires a non-empty one-line message and stages the complete worktree.
- **Push commits** requires a named current branch and valid existing Git credentials.
- Clone, pull, commit, and push reject repository-local executable filters, merge drivers, and filesystem monitors.

Run `git status` and the intended Git command in a terminal for additional diagnosis. Blueprints does not resolve merge conflicts, force-push, initialize submodules, or rewrite history.

## An import cannot be applied

Confirm that:

- at least one visible proposal is included;
- every included proposal has a valid editable target release;
- type and changelog-group selections are valid for the project;
- the workspace is Trusted and conflict-free;
- the target release is not Frozen or Released.

Editing a proposal changes only temporary review state. No partial project import is written if batch validation fails.

## Identity backup cannot be restored

Identity restore is available only when no local identity is currently active. Return to setup on a device profile without an existing Blueprints identity, provide the correct passphrase, and select **Restore backup…**.

An exported encrypted identity backup is distinct from the machine-local protected identity directory. For file-level disaster recovery on macOS or Linux, the restored identity also needs the matching `Security/local-private-key-protector.key`. Preserve the failed restore materials and follow [member key lifecycle](key-lifecycle.md) rather than creating replacement signatures manually.

## A project cannot be deleted

Deletion deliberately stops when it cannot prove the target is the selected Blueprints project. It also refuses:

- a filesystem root or user home directory;
- overlapping local and shared roots;
- symbolic-link roots;
- a root inside a Git worktree;
- shared Blueprints content with no matching project identity.

Do not weaken these checks. Back up the intended project, correct the stored paths, and try again.

## Changelog export is empty or incomplete

Select the intended release and preview notes first. Check each item's changelog group and completion state. Incomplete items are excluded unless **Incomplete** is enabled. Enable **Item keys** and **Descriptions** only when those fields should appear in the output.

Repository source context requires a linked, readable local Git worktree. It supplements the signed work items; it does not replace them.

## Build or SDK selection fails

```sh
dotnet --list-sdks
dotnet --version
dotnet restore Blueprints.sln
```

Install .NET 10 SDK `10.0.300` or a newer compatible patch. The repository-owned `global.json` is authoritative.

## Linux application startup fails

Avalonia currently needs an accessible X11/XWayland display.

```sh
./scripts/diagnose-linux-display.sh
```

Review `DISPLAY`, `WAYLAND_DISPLAY`, and `XAUTHORITY`. A successful headless build does not prove that the current shell can open a desktop window.

## Reporting a defect

Include:

- operating system and `dotnet --info`;
- the exact action and visible message;
- whether the workspace was Trusted, Untrusted, or Corrupt;
- whether sync conflicts existed;
- sanitized relative document paths;
- the output of `./scripts/verify.sh`.

Do not attach private keys, protection keys, provider tokens, or confidential project content to a public issue. Report suspected vulnerabilities according to [SECURITY.md](../SECURITY.md).
