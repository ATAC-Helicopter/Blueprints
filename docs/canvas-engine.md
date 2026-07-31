# Canvas engine

The canvas is the primary Blueprints workspace. It visualizes signed project entities without replacing them with a second source of truth.

## Graph projection

The current graph is derived from existing domain relationships:

```text
project
└── version
    └── work item
```

- The project node uses `ProjectConfigurationDocument.ProjectId`.
- A version node uses `VersionDocument.VersionId`.
- A work-item node uses `ItemDocument.ItemId`.
- Ownership connector lines are derived from version ownership; they are not persisted separately.
- User-created typed connectors are projected from the optional signed relationship graph.

Changing a node title, state, category, or completion value uses the existing version and item workflows. Moving a node changes only the layout document.

## Typed relationships

The inspector can define a relationship type with a stable lowercase ID, display name, optional description, `#RRGGBB` canvas color, and directional flag. A relationship then connects any two different existing project, version, or item nodes and may carry a short label. Its color is projected on the canvas alongside the built-in ownership lines.

Relationship types and edges are stored together in `project/relationships.json`. Every edit increments the document revision, writes canonical signed JSON, and adds a specific audit action. A type cannot change between directional and undirected while an edge uses it. Archiving a version or item removes dangling edges in the same signed archive operation.

The validator permits at most 100 types and 5,000 edges, rejects unknown types and entities, empty or duplicate IDs, self-links, malformed colors, and duplicate logical edges. For an undirected type, reversing endpoints does not create a distinct edge.

## Interaction model

| Action | Result |
| --- | --- |
| Click a version | Select it and open version fields in the inspector |
| Click a work item | Select it, its owning version, and its item fields |
| Drag empty canvas | Draw a box that selects every intersecting node |
| Ctrl/Shift-click a node | Add or remove it from the canvas selection |
| Drag a selected node | Move the complete selection and save a new signed layout revision on release |
| Arrow keys | Move selected nodes by one pixel and save the layout |
| Shift+arrow keys | Move selected nodes by ten pixels and save the layout |
| Ctrl+A / Escape | Select every node / clear the canvas selection |
| Middle-drag empty canvas | Pan without changing shared node positions |
| Scroll the canvas | Save machine-local viewport offsets after a short debounce |
| Ctrl+mouse wheel or zoom buttons | Change and save machine-local zoom |
| Fit view | Calculate a zoom that fits the complete blueprint and return to the origin |
| Group selector | Choose category, work type, or version lanes for related work |
| Organize | Rebuild deterministic positions in the selected lanes and save them |
| Undo / redo | Restore the previous or next arrangement and save it as a new signed revision |
| Save layout | Explicitly write the current shared node positions |
| Ctrl+S | Save shared node positions |
| Ctrl+Z | Undo the latest node drag or auto-arrangement |
| Ctrl+Shift+Z or Ctrl+Y | Redo the latest undone layout change |
| Ctrl+0 | Fit the local view |
| Ctrl+plus/minus | Zoom the local view |

Category grouping is the default because it matches changelog output. Switching grouping mode automatically organizes the canvas when mutation is allowed; the resulting node positions use the normal signed layout workflow. The grouping choice itself is a local presentation preference and does not change item categories, types, version ownership, or relationship semantics.

Live guides appear when the edge or center of a moving node approaches another node's edge or center. The minimap shows the full graph, selected nodes, and the current viewport. Selection, guides, lane headers, and minimap visuals are session-local UI state; only resulting node coordinates are persisted.

Editing and layout controls are disabled when the workspace is untrusted or has unresolved sync conflicts.

## Persistence

Shared node positions are stored in:

```text
project/canvas-layout.json
project/canvas-layout.sig
```

The signed layout document contains:

- schema and project identity;
- monotonically increasing layout revision;
- node type, entity ID, and coordinates;
- update time and last-modifier identity.

The document is optional for schema-1 compatibility. An older workspace without it opens normally and receives deterministic default positions. The first layout save creates revision 1.

Each successful save:

1. reopens and validates the workspace;
2. requires trusted, conflict-free mutable state;
3. verifies every layout node refers to an existing project entity;
4. validates coordinate, uniqueness, and size limits;
5. writes canonical JSON and an Ed25519 detached signature;
6. appends a signed `canvas.layout.save` audit entry;
7. reloads displayed state from disk.

Layout writes do not rewrite version or item documents.

Undo and redo history is held only for the current application session, is capped at 50 layout changes, and is cleared when the workspace or graph structure changes. History snapshots are not a hidden source of truth. Applying one creates a normal signed layout revision and audit entry, preserving the same validation and collaboration guarantees as a direct drag.

Zoom and viewport offsets are machine-local preferences stored in:

```text
.blueprints/canvas-view.json
```

This file is not signed, audited, synchronized, or authoritative. Invalid local values fall back to the default view. Keeping viewport preferences local prevents ordinary scrolling from creating audit noise or collaboration conflicts.

## Validation limits

The schema-1 validator enforces:

- supported node types: `project`, `version`, and `item`;
- non-empty and unique `(nodeType, entityId)` pairs;
- entity membership in the current workspace;
- finite coordinates from `0` through `100000`;
- no more than `10000` nodes;
- matching non-empty project identity;
- schema version `1` and revision of at least `1`.

These limits prevent malformed, non-finite, or unbounded layout input from reaching Avalonia rendering.

The local view-state store separately accepts zoom from `0.25` through `2.5` and non-negative offsets through `100000`.

## Sync and conflicts

The layout lives under `project/`, so the exchange snapshot, manifest, signature validator, push, pull, and conflict analyzer include it automatically.

Two collaborators moving the same canvas from a common baseline may produce a conflict in `project/canvas-layout.json`. Blueprints deliberately treats the complete layout as one document in schema 1. Conflict resolution therefore chooses the local or shared arrangement as a whole.

Version and work-item content remain separate documents. A layout conflict does not imply that their content has conflicted.

The relationship graph follows the same whole-document rule. A conflict in `project/relationships.json` compares revision, types, edges, update time, and author for diagnosis, then requires choosing the complete local or shared graph. Schema 1 deliberately does not attempt an unsafe automatic edge merge.

## Current limits

- There is one shared release-planning canvas per project.
- Node positions are shared project state, not per-user preferences.
- Directional typed relationships use distinct type semantics and color, but the canvas does not yet draw arrowheads or edit labels directly on an edge.
- Multi-selection groups nodes for movement only; lane headers are projections and do not create persistent group entities.
- Automatic organization is deterministic but not a graph-optimization engine.
- Layout conflict resolution is whole-document.
- Relationship conflict resolution is whole-document.

These are product limitations, not hidden behavior. Planned work belongs in the canonical [roadmap](../Roadmap.md).
