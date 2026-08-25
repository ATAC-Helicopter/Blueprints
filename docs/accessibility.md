# Accessibility

Blueprints is designed so the release-planning workflow does not depend on recognizing unfamiliar icons or understanding its signature implementation.

## Current interaction support

- Main destinations use outcome-based text labels and explicit accessible names.
- `Ctrl+1` through `Ctrl+6` switches between Overview, Releases, Imports, Team, Sync, and Validation. Use `Command` instead of `Ctrl` on macOS.
- Standard `Tab` and `Shift+Tab` navigation reaches form fields and actions.
- Releases and work items use standard lists, tables, and form controls with visible text labels.
- Relationship records identify their type and label without requiring a visual diagram.
- `Ctrl`/Command+Shift+`V` creates the named version and `Ctrl`/Command+Shift+`I` opens a new work item for the selected version.
- Work-item progress is changed through the labeled Progress field and saved through the normal signed workflow.
- Destructive and irreversible actions use text labels and explain their consequences.
- Color is supplemented by labels, counts, status text, and guidance.
- Default Fluent focus presentation is retained rather than hidden by custom styling.

## Language rules

Primary navigation and first-run setup use explicit product terms. Advanced evidence such as signatures, manifests, key IDs, and audit details belongs in Validation, Sync, or Team, where it is accompanied by plain-language guidance.

Every new empty, blocked, invalid, or read-only state should answer:

1. what happened;
2. whether the user's data changed;
3. what the user can safely do next.

## Stable-release qualification

The current repository has automated command and workflow coverage, but it does not yet claim completed assistive-technology certification. Before v1.0, retain manual results for every supported desktop platform covering:

- keyboard-only first run, project creation, release planning, export, invitation, sync, conflict, and identity recovery;
- visible focus at 100%, 150%, and 200% display scaling;
- screen-reader names, roles, state changes, and reading order;
- high-contrast or increased-contrast settings;
- reduced-motion behavior if motion is introduced;
- text clipping at supported window minimums and increased system text sizes.

Accessibility regressions are release blockers for the primary workflow.
