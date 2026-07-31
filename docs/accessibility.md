# Accessibility

Blueprints is designed so the release-planning workflow does not depend on recognizing unfamiliar icons or understanding its signature implementation.

## Current interaction support

- Main destinations use outcome-based text labels and explicit accessible names.
- `Ctrl+1` through `Ctrl+6` switches between Home, Plan releases, Find work, People, Share changes, and Safety check. Use `Command` instead of `Ctrl` on macOS.
- Standard `Tab` and `Shift+Tab` navigation reaches form fields and actions.
- All icon-only canvas controls expose text tooltips and accessible names.
- Canvas nodes support keyboard selection and movement; the on-screen footer lists the available keys.
- Destructive and irreversible actions use text labels and explain their consequences.
- Color is supplemented by labels, counts, status text, and guidance.
- Default Fluent focus presentation is retained rather than hidden by custom styling.

## Language rules

Primary navigation and first-run setup use user outcomes rather than implementation terms. Advanced evidence such as signatures, manifests, key IDs, and audit details belongs in Safety check, Share changes, or People, where it is accompanied by plain-language guidance.

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
