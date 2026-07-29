# User guide

Blueprints is a desktop application for planning releases in signed local files. It currently targets technical preview users who are comfortable choosing local directories.

## Concepts

- **Local workspace:** the editable copy on this computer.
- **Shared sync root:** a separate directory used to exchange signed files. It may be on a local disk, network share, or another synchronized location.
- **Identity:** a local Ed25519 key pair and display name used to sign project data.
- **Trust:** whether required files and signatures validate.
- **Version:** a planned release in one of four states: Planned, In Progress, Frozen, or Released.
- **Item:** one release-note entry with an item key, type, changelog category, title, and completion state.

## Create a project

1. Start Blueprints.
2. Enter a project name and short code.
3. Keep `SemVer` or enter the versioning scheme your project uses.
4. Choose different local and shared roots. Neither may contain the other.
5. Select **Create Signed Project**.

Blueprints creates the first local identity automatically in the current preview. Identity onboarding is planned.

## Plan a release

1. Open the **Releases** tab.
2. Enter a version name and select **New**.
3. Select the version and edit its name, status, and notes.
4. Select an item type and changelog category, enter a title, then select **Add Item**.
5. Mark completed items as **Done**.

Item keys are generated from project rules. Released versions are immutable.

## Export a changelog

1. Select a version.
2. Select **Export Changelog**.
3. Review the preview and saved path shown in the version editor.

Incomplete items are excluded by the current default rules. When a local Git repository is linked, recent commit subjects and matching item keys are added as source context.

## Link local Git

1. Open **Integrations**.
2. Enter the path to a Git worktree.
3. Select **Save**, then **Refresh**.

This integration is read-only. It reads repository status and recent commit metadata; it does not commit, push, or modify the repository.

## Exchange changes

The shared root is an exchange layer, not the editable workspace.

- **Refresh Analysis** compares local, shared, and last-synced baselines.
- **Push** copies valid outgoing signed documents and updates the signed manifest.
- **Pull** validates incoming signatures and manifest continuity before applying changes.
- If both copies changed, Blueprints blocks mutation until you choose **Keep Local** or **Accept Shared**.

The current conflict preview is low-level. Make a backup before resolving important conflicts.

## Trust and read-only mode

Open **Trust** to see signature, audit, shared-folder, and conflict diagnostics.

- **Trusted:** normal editing is allowed.
- **Untrusted:** one or more signatures did not validate; mutation is disabled.
- **Corrupt:** required data or audit continuity is missing or malformed; mutation is disabled.

Do not “repair” a signature by deleting it or copying another signature file. Restore a known-good workspace or shared snapshot and re-open it.

## Data and backup

Back up both:

- the chosen local workspace;
- the Blueprints application-data directory containing the identity and its protected key.

A workspace without the signing identity may remain readable but cannot be safely continued as the same member. Formal key recovery is not implemented yet.

See [workspace format](workspace-format.md) and [security model](security-model.md) for details.
