# User guide

Blueprints is a desktop application for planning releases in signed local files. The main workspace is a hands-on blueprint canvas: versions and work items are visible as connected nodes, while a compact tool rail opens evidence, trust, and exchange tools.

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
4. Use **Browse** to choose different local and shared roots. Neither may contain the other.
5. Select **Create signed project**.

Blueprints creates the first local identity automatically in the current preview. Identity onboarding is planned.

## Plan a release

1. Enter a version name in the canvas toolbar and select **Version**.
2. Select the version node to edit its name, state, and accomplishment notes in the inspector.
3. Select **Work item**, enter its type, changelog group, title, and context, then select **Connect new**.
4. Select any work-item node to edit it directly in the inspector.
5. Drag nodes to arrange the working view, and use zoom or **Fit map** to navigate larger plans.
6. Mark completed items as ready for release notes.

The lines on the canvas represent real project relationships: a work item is connected to the version that owns it. Item keys are generated from project rules. Released versions are immutable.

## Export a changelog

1. Select a version node.
2. Open the **Versions and releases** tool from the rail.
3. Select **Export notes**.
4. Review the changelog preview and saved path.

Incomplete items are excluded by the current default rules. When a local Git repository is linked, recent commit subjects and matching item keys are added as source context.

## Link local Git

1. Open **Integrations** from the tool rail.
2. Enter the path to a Git worktree.
3. Select **Save connection**, then **Refresh all**.

This integration is read-only. It reads repository status and recent commit metadata; it does not commit, push, or modify the repository.

## Exchange changes

The shared root is an exchange layer, not the editable workspace.

- **Refresh comparison** compares local, shared, and last-synced baselines.
- **Push** copies valid outgoing signed documents and updates the signed manifest.
- **Pull** validates incoming signatures and manifest continuity before applying changes.
- If both copies changed, Blueprints blocks mutation until you choose **Keep Local** or **Accept Shared**.

The current conflict preview is low-level. Make a backup before resolving important conflicts.

## Trust and read-only mode

Open **Trust** from the tool rail to see signature, audit, shared-folder, and conflict diagnostics.

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
