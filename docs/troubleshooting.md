# Troubleshooting

This guide covers supported diagnostic and recovery steps. Do not delete signatures or edit signed JSON to make an error disappear.

## The workspace opens read-only

Check the trust summary and open **Trust**.

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
- If both exist, open **Trust**. An invalid layout signature makes the workspace read-only.
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
