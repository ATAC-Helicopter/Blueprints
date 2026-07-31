# Canvas engine

The canvas is Blueprints' primary workspace. It is a projection of canonical signed project documents, never a parallel graph store.

## Authoritative data

```text
signed project
├── versions
│   └── work items
├── canvas layout (shared coordinates)
└── typed relationships

machine-local view
└── mode, viewport, zoom, search, filters, minimap and collapsed frames
```

Version-to-item ownership remains authoritative in each item and version document. In Plan, the version frame visually contains its items, so ownership connector lines are deliberately omitted. Typed user-created relationships remain visible.

## Lifecycle semantics

Plan uses four item states:

| Lifecycle | Meaning |
| --- | --- |
| Planned | Accepted into the release plan but not started |
| In Progress | Actively being implemented |
| Review | Awaiting review, validation, or release acceptance |
| Complete | Finished and eligible for normal completed-item release-note behavior |

`ItemDocument.WorkflowState` is optional in schema 1. For compatibility, an older incomplete item maps to Planned and an older completed item maps to Complete. Opening a legacy workspace does not rewrite it. Saving or dragging a card writes the explicit lifecycle through the normal item command; Complete and the established `IsDone` field stay consistent.

The version's `ReleaseStatus` is a separate release-level lifecycle (Planned, In Progress, Frozen, Released). It is not silently reused as item workflow.

## View modes

- **Plan** renders movable/resizable version frames with lifecycle columns and neutral metadata-rich cards.
- **Dependencies** uses a readable automatic graph arrangement, deemphasizes ownership, and emphasizes typed relationships.
- **Release Notes** groups work by the authoritative changelog category.
- **Timeline** is disabled until a target-date contract exists. Blueprints does not invent dates.

Changing a view never changes ownership, changelog category, item type, or relationships.

## Version frames and cards

A frame header shows the version, release state, completed count/percentage, readiness summary, and blocker/open count. The header selects and moves the frame; the lower-right handle resizes it for the session; Collapse is a machine-local preference.

Cards show key, a multi-line title, lifecycle, item type, changelog category, source indicator, and blocker state. Click selects; Ctrl/Shift-click adds/removes selection. Horizontal drag or Left/Right changes lifecycle. Details remain editable in the inspector when trust, conflict, and immutability rules allow it.

Shared coordinates remain in signed `project/canvas-layout.json`. Frame size is currently session-local and intentionally does not expand the signed layout schema. This preserves schema-1 compatibility while the team evaluates whether size is shared structural intent.

## Relationships

Plan and Dependencies render only relationships from signed `project/relationships.json`. Colors come from their type; directional types draw arrowheads. Hovering or selecting an edge reveals its label/type. Selecting an entity emphasizes its incoming/outgoing edges and dims unrelated links.

Connect mode:

1. requires a trusted, conflict-free mutable workspace and an existing type;
2. captures two existing entity endpoints from visible handles;
3. opens/populates the existing relationship editor without committing;
4. requires the user to review type and label;
5. saves through `ProjectWorkspaceCoordinatorService.SaveRelationship`;
6. reuses self-link, duplicate logical edge, type, endpoint, count, signing, transaction, and audit validation.

Edge selection populates the same editor for type/label/source/target changes or removal. There is no second relationship store.

## Inspector and toolbar

The compact toolbar exposes view switching, version/item creation, Connect, search, undo/redo, arrange, fit, zoom-to-selection, zoom controls, filters, minimap, trust, sync, and readiness.

The inspector has:

- **Details:** version and item fields, including lifecycle and changelog category;
- **Relationships:** type and edge editing;
- **Evidence:** readiness and source provenance;
- **History:** signed audit status and layout/relationship revisions.

Instructions live in tooltips, accessible help, and shortcut documentation rather than permanently covering the canvas.

## Navigation and keyboard

| Action | Result |
| --- | --- |
| Ctrl/Command+7, 8, 9 | Plan, Dependencies, Release Notes |
| Ctrl/Command+F | Focus search |
| Ctrl/Command+L | Enter/exit Connect |
| Ctrl/Command+J | Zoom to selection |
| Ctrl/Command+0 | Fit complete board |
| Ctrl/Command+plus/minus | Zoom |
| Ctrl/Command+S | Save shared coordinates |
| Ctrl/Command+Z / Shift+Z / Y | Undo/redo layout |
| Ctrl/Command+Shift+V | Create the named version |
| Ctrl/Command+Shift+I | Start a work item for the selected version |
| Left/Right in Plan | Move selected editable items through lifecycle |
| Arrows in graph views | Move selected nodes one pixel |
| Shift+arrows | Use a ten-pixel graph movement step |
| Enter | Open the selected work item in the inspector |
| Escape | Clear selection and leave Connect |

The minimap is collapsible and click-to-navigate. Search and filters are view-only. Focus temporarily narrows the board to the selected item or version.

## Persistence and trust

Shared:

- entity coordinates in signed `project/canvas-layout.json`;
- relationship types and edges in signed `project/relationships.json`;
- lifecycle/category/type/title/content in signed item documents.

Machine-local:

- persisted locally: mode, viewport, zoom, search, filters, minimap visibility, and collapsed version IDs;
- session-local: selected inspector tab, temporary focus, and frame size.

Local view state is bounded and atomically replaced in `.blueprints/canvas-view.json`; it is unsigned, unaudited, unsynchronized, and cannot establish trust.

Every shared mutation reopens and validates the workspace, requires trusted/conflict-free mutable state, writes canonical JSON and Ed25519 signatures, appends signed audit evidence inside the transaction, and reloads display state from disk. Frozen/released versions reject item lifecycle changes. Untrusted, corrupt, or conflict-blocked workspaces remain read-only.

## Limits and performance

Layout remains bounded to 10,000 entity positions and coordinates from 0 through 100,000. Relationships remain bounded to 100 types and 5,000 edges. Local zoom remains 25%–250%.

Projection and minimap calculations are isolated in testable services. Pointer movement updates the dragged visual rather than rebuilding the graph on every event; persistence and full rerender happen on release. Filtering and mode changes rebuild projections deliberately.

Current limitations:

- frame size is session-local;
- Timeline has no target-date model and is disabled;
- relationship and layout conflicts remain whole-document;
- automatic dependency placement is deterministic, not a general graph optimizer;
- no automated desktop pixel suite replaces platform accessibility qualification.
