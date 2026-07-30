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

Before creating or joining a project, enter the display name teammates should see and select **Create identity**. Blueprints creates protected local Ed25519 key material. Back up the complete Blueprints application-data directory before relying on that identity.

A new teammate can export a signed identity invitation directly from the setup screen before they belong to any project.

## Plan a release

1. Enter a version name in the canvas toolbar and select **Version**.
2. Select the version node to edit its name, state, and accomplishment notes in the inspector.
3. Select **Work item**, enter its type, changelog group, title, and context, then select **Connect new**.
4. Select any work-item node to edit it directly in the inspector.
5. Drag nodes to arrange the working view, and use zoom or **Fit view** to navigate larger plans.
6. Mark completed items as ready for release notes.

The standard lines on the canvas represent ownership: a work item belongs to its version. Item keys are generated from project rules. Released versions are immutable.

## Connect typed relationships

Use **Typed relationships** in the canvas inspector when the ownership hierarchy is not enough:

1. Select **+**, enter a lowercase type ID such as `blocks`, a name, color, and optional meaning.
2. Choose whether the type is directional, then save it.
3. Choose a source and target node, add an optional label, and select **Save**.
4. Select an existing relationship to edit or remove it.

Typed connectors appear in their configured color. Their definitions and endpoints are signed shared project state, so changes participate in audit, push, pull, trust checks, and whole-document conflict resolution. An endpoint must exist in the current project and cannot point to itself. Archiving a node also removes relationships that would otherwise dangle.

## Arrange and share the canvas

- Dragging a node saves the arrangement when the pointer is released.
- Middle-drag empty canvas space to pan.
- Scrolling saves the viewport locally after a short pause.
- Use Ctrl+mouse wheel or Ctrl+plus/minus to zoom; zoom is remembered locally for this workspace.
- Ctrl+S saves shared node positions and Ctrl+0 fits the local view.
- **Fit view** returns to the standard overview without rearranging nodes.
- **Auto arrange** deliberately replaces positions with deterministic defaults.
- **Save layout** explicitly writes the current arrangement.
- **Undo** and **redo** restore node drags or auto-arrangement changes made during the current session. Use the toolbar, `Ctrl+Z`, `Ctrl+Shift+Z`, or `Ctrl+Y`.

The saved revision appears at the bottom of the inspector. Node positions are signed project state and participate in push, pull, trust validation, audit history, and conflicts. Undo and redo create new signed revisions instead of rewriting history, and their in-memory stack resets when the app session or graph structure changes. Zoom and scroll offsets stay machine-local so navigating does not create team conflicts. Older workspaces without a layout file use default positions until the first save.

See [canvas engine](canvas-engine.md) for the exact model and [troubleshooting](troubleshooting.md) for recovery.

## Export a changelog

1. Select a version node.
2. Open the **Versions and releases** tool from the rail.
3. Choose whether to include incomplete items, item keys, descriptions, or compact output.
4. Select **Preview notes** to review the exact Markdown without writing a file.
5. Select **Export notes** and review the saved path.

Incomplete items are excluded by the current default rules. When a local Git repository is linked, recent commit subjects and matching item keys are added as source context.

Before freezing or releasing, review **Release readiness** in the release planner. It reports:

- an unavailable or unconfigured repository;
- uncommitted working-tree changes that make the source baseline unstable;
- incomplete items in the selected version;
- missing post-tag history;
- recent commits that do not reference a completed item key in the selected version.

Repository errors and dirty state are blockers. Unmatched commits and incomplete items require attention because they may be intentionally out of scope; Blueprints explains them but does not silently change source history or the release plan.

## Archive draft work

Draft versions and items in Planned or In Progress state can be archived. Select the archive action twice to confirm. The entity leaves the active signed plan, its removal participates in sync, and its signed files remain under `.blueprints/archive/` for recovery. Frozen and released versions are immutable and cannot be archived.

## Discover work with Source Lens

1. Open **Source Lens** from the navigation.
2. Enter up to eight Git worktree paths, one per line.
3. Select **Save links**, then **Scan sources**.
4. Review the proposal inbox produced from changelogs, roadmaps, GitHub issues, and issue-linked GitHub Projects.
5. Edit the selected proposal’s title, context, target version, type, changelog group, and completion state.
6. Exclude anything that should not enter Blueprints.
7. Select **Apply approved to blueprint**.

Discovery is read-only. It does not commit, push, edit issues, change Projects, or modify planning files. Proposals are not project data until Apply. Apply requires a trusted workspace, adds the reviewed proposals as signed work items, records their provenance, and writes one audit action.

GitHub discovery requires the authenticated GitHub CLI. Local changelog and roadmap discovery remains available if GitHub is not configured. See [Source Lens](source-lens.md) for limits, duplicate behavior, and security details.

## Exchange changes

The shared root is an exchange layer, not the editable workspace.

To add a second person:

1. The new person opens **Team** in any local project and exports a signed identity invitation.
2. An existing project administrator imports that file, reviews the requested display name and role, and adds the member.
3. The administrator pushes the membership revision.
4. With the new member selected, the administrator exports a signed project invitation.
5. The new member chooses **Join a team project** on the setup screen and selects an empty local workspace.
6. Blueprints stages and validates the shared project before promoting it to the chosen local location.

Project invitations are targeted to one local identity. Treat them as sensitive team coordination records and transfer them through an authenticated channel.

- **Refresh comparison** compares local, shared, and last-synced baselines.
- **Push** copies valid outgoing signed documents and updates the signed manifest.
- **Pull** validates incoming signatures and manifest continuity before applying changes.
- The exchange header reports the last locally recorded pulled/pushed manifest versions and successful trust check; these are local evidence, not a guarantee that an offline shared folder has not changed.
- If both copies changed, Blueprints blocks mutation until you choose **Keep Local** or **Accept Shared**.

The conflict view summarizes important fields for known project document types and keeps bounded raw previews available for diagnosis. Before applying **Keep Local** or **Accept Shared**, Blueprints preserves both available document/signature pairs under `.blueprints/recovery/conflicts/` in the local workspace. The completion message includes the exact recovery directory.

Conflict recovery copies reduce the risk of an accidental choice, but they are not a substitute for backing up the complete workspace before important collaboration or membership changes.

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

See [workspace format](workspace-format.md), [security model](security-model.md), [member key lifecycle](key-lifecycle.md), and [backup and disaster recovery](backup-recovery.md) for details.
