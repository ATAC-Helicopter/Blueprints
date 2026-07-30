# Source Lens

Source Lens converts existing project signals into editable Blueprints work-item proposals. It is an approval workflow, not an autonomous importer.

## Supported sources

| Source | How it is read | What becomes a proposal |
| --- | --- | --- |
| `CHANGELOG.md` | Local read-only Markdown parsing | Bullet entries, completed by default |
| `Roadmap.md` | Local read-only Markdown parsing | Bullet and task-list entries with checkbox state |
| GitHub issues | Authenticated `gh issue list` request | Open and closed issue metadata |
| GitHub pull requests | Authenticated `gh pr list` request | Open, closed, and merged pull-request metadata |
| GitHub releases | Authenticated `gh release list` request | Draft and published release records |
| GitHub Projects | `projectItems` attached to returned issues | Project-linked issues with project context |

The scanner checks each linked repository root and its `docs` directory for common changelog and roadmap filename casing. It reads at most 5,000 lines and 100 candidates per Markdown file. GitHub reads are limited to 100 issues, 100 pull requests, and 50 releases per repository. A per-repository scan returns at most 250 deduplicated proposals, and combined discovery returns at most 500 proposals across up to eight worktrees.

Standalone GitHub Project draft items are not imported yet. Only issues visible through the repository issue query and linked to a Project are recognized.

## Requirements

- Link one or more local Git worktrees in **Source Lens**, one path per line.
- Install Git.
- For GitHub discovery, install and authenticate the GitHub CLI with `gh auth login`.
- The Git worktree must have a recognizable `github.com` origin remote.

Local Markdown discovery still works if GitHub is unavailable. Source Lens reports the skipped provider as a warning instead of failing the entire scan.

## Approval workflow

1. Open **Source Lens**.
2. Link up to eight local repositories, one path per line, and select **Scan sources**.
3. Select a proposal from the inbox.
4. Review and edit its title, description, target version, work type, changelog category, and completion state.
5. Include or exclude the proposal with **Approved**.
6. Repeat for the remaining proposals.
7. Select **Apply approved to blueprint**.

Nothing is written to the workspace during discovery or editing. Apply requires a trusted, conflict-free workspace and an editable target version. All approved proposals are validated before persistence and are then written as one signed workspace mutation with one signed audit entry.

## Suggestions and duplicate handling

Blueprints infers:

- security work from security and vulnerability terms;
- bugs and fixes from bug/fix language or labels;
- features from feature/enhancement language;
- changelog categories from headings, labels, and issue state;
- completion from Markdown checkboxes, changelog history, and closed GitHub issues.

These are suggestions only. The user can replace every inferred field.

An exact normalized title match against existing work is marked as a possible duplicate and excluded by default. The user may deliberately approve it. Blueprints does not silently merge, overwrite, or delete existing items.

## Provenance

Applied items retain tags that identify:

- that the item came through Source Lens;
- the source kind;
- a compact source reference such as `github:#42` or `roadmap:Roadmap.md:18`.

When several repositories are scanned, the source reference is qualified with the full local worktree path before it enters the proposal inbox. Source content remains informational. It does not inherit authority from GitHub, a roadmap, or a changelog.

Internally, Source Lens classifies references without making one hosted provider the domain model. A reference records:

- provider (`Local`, `GitHub`, or `GitLab`);
- artifact kind (planning document, commit, issue, pull request, release, or project);
- repository identity;
- provider identifier;
- an optional web location.

Local Markdown and GitHub issue discovery already emit this structure. Pull-request, release, standalone project, and GitLab readers can use the same contract without changing signed Blueprints project truth.

## Security and privacy

- Discovery is read-only for the repository and GitHub.
- Blueprints never invokes provider write commands during discovery or apply.
- GitHub credentials remain owned by the GitHub CLI and are not copied into project files.
- Untrusted source text is length- and count-bounded and rendered as text.
- Proposals are transient UI state until explicit approval.
- Invalid taxonomy, missing versions, frozen/released targets, broken trust, and sync conflicts block apply.
- Apply signs Blueprints documents with the local member identity; it does not sign or modify source-provider data.

Source Lens does not prove that imported statements are true. Review is the human trust decision.
