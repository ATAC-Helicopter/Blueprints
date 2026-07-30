# Source Lens

Source Lens converts existing project signals into editable Blueprints work-item proposals. It is an approval workflow, not an autonomous importer.

## Supported sources

| Source | How it is read | What becomes a proposal |
| --- | --- | --- |
| `CHANGELOG.md` | Local read-only Markdown parsing | Bullet entries, completed by default |
| `Roadmap.md` | Local read-only Markdown parsing | Bullet and task-list entries with checkbox state |
| GitHub issues | Direct REST API request | Open and closed issue metadata |
| GitHub pull requests | Direct REST API request | Open, closed, and merged pull-request metadata |
| GitHub releases | Direct REST API request | Draft and published release records |
| GitHub Projects | Direct authenticated GraphQL query | Standalone draft items and project-linked issues |
| GitLab issues | Direct REST API request | Open and closed issue metadata |
| GitLab merge requests | Direct REST API request | Open, closed, and merged change-request metadata |
| GitLab releases | Direct REST API request | Published and upcoming release records |
| GitLab milestones | Direct REST API request | Active and closed planning milestones |

The scanner checks each linked repository root and its `docs` directory for common changelog and roadmap filename casing. It reads at most 5,000 lines and 100 candidates per Markdown file. GitHub reads are limited to 100 issues, 100 pull requests, and 50 releases per repository. A per-repository scan returns at most 250 deduplicated proposals, and combined discovery returns at most 500 proposals across up to eight worktrees.

Standalone draft discovery reads only Projects linked to the repository, checks at most 10 Projects and 100 items per Project, and returns at most 100 drafts. Issues and pull requests found in those Projects remain owned by their dedicated discovery feeds, preventing duplicate Project proposals.

## Requirements

- Link one or more local Git worktrees in **Source Lens**, one path per line.
- Install Git.
- The Git worktree must have a recognizable `github.com` origin remote.
- Public issues, pull requests, and releases can be discovered anonymously.
- Set `BLUEPRINTS_GITHUB_TOKEN` in the application environment for private repositories, draft releases, or GitHub Projects. Use the narrowest read-only repository permissions that cover the sources you need.
- A `gitlab.com` origin is detected for GitLab discovery, including nested group paths.
- Public GitLab.com sources can be discovered anonymously. Set a read-only `BLUEPRINTS_GITLAB_TOKEN` in the application environment for private projects.

Local Markdown discovery still works if GitHub is unavailable. Source Lens reports the skipped provider as a warning instead of failing the entire scan.

Repository discovery talks to hosted services through a provider-neutral reader contract. The GitHub implementation calls the versioned REST and GraphQL endpoints directly with a 15-second timeout, a 4 MiB response limit, bounded result counts, and parallel source reads. Transport does not change proposal approval, provenance, or signed workspace behavior.

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

Internally, Source Lens classifies references without making one hosted provider the domain model. GitHub pull requests and GitLab merge requests are both change requests; GitHub Projects and GitLab milestones are both hosted planning records. A reference records:

- provider (`Local`, `GitHub`, or `GitLab`);
- artifact kind (planning document, commit, issue, pull request, release, or project);
- repository identity;
- provider identifier;
- an optional web location.

Local Markdown, GitHub, and GitLab discovery all emit this structure without changing signed Blueprints project truth.

## Security and privacy

- Discovery is read-only for the repository and GitHub.
- Blueprints never invokes provider write commands during discovery or apply.
- GitHub credentials are read only from `BLUEPRINTS_GITHUB_TOKEN`. They are not written to integration settings, signed project files, logs, warnings, or proposal provenance.
- GitLab credentials follow the same rule through `BLUEPRINTS_GITLAB_TOKEN`.
- Untrusted source text is length- and count-bounded and rendered as text.
- Proposals are transient UI state until explicit approval.
- Invalid taxonomy, missing versions, frozen/released targets, broken trust, and sync conflicts block apply.
- Apply signs Blueprints documents with the local member identity; it does not sign or modify source-provider data.

## Provider write boundary

No Source Lens workflow writes to a hosted provider. The provider-operation contract distinguishes reads from create/update/project/release writes. A future write implementation must pass a separate approval matching the exact provider, repository, operation, and target. That approval is valid for no more than ten minutes and can be consumed only once. Batch or standing approval is intentionally unsupported.

Adding a transport method is not enough to authorize a write: a UI workflow must show the exact mutation, collect approval for that one target, and pass it through the policy at execution time. Credentials and a Blueprints workspace edit do not imply provider-write consent.

Source Lens does not prove that imported statements are true. Review is the human trust decision.
