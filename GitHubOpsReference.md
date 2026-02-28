# GitHub Operations Reference

This file is the working reference for future GitHub-related tasks in this repository.

## Repository

- Owner: `ATAC-Helicopter`
- Repo: `Blueprints`
- Remote: `origin`
- URL: `https://github.com/ATAC-Helicopter/Blueprints`
- Default branch: `main`
- Visibility: `private` for now

## Branch Strategy

- `main`: stable branch for pushed, verified repository state
- `develop`: integration branch for active work before promotion to `main`
- `feature/issue-<number>-<slug>`: feature or implementation work tied to a GitHub issue
- `chore/issue-<number>-<slug>`: repository/process/infrastructure work tied to a GitHub issue

Current managed branches:

- `main`
- `develop`
- `feature/issue-7-workspace-load-save`
- `feature/issue-8-dpapi-key-storage`
- `feature/issue-10-shared-folder-sync`
- `feature/issue-11-avalonia-live-workspace`
- `chore/issue-9-public-release-readiness`

Default workflow:

- branch from `develop` for active work
- merge feature or chore branches into `develop`
- promote `develop` into `main` once the state is intentionally ready

## Current GitHub Setup

- Issues: enabled
- Projects: enabled
- Wiki: disabled
- Discussions: disabled
- Auto-merge: enabled
- Merge commits: disabled
- Squash merge: enabled
- Rebase merge: enabled
- Delete branch on merge: enabled

Protected branches:

- `main`
- `develop`

Current protection baseline on protected branches:

- required status check: `build-and-test`
- branch must be up to date before merge
- 1 approving review required
- stale reviews dismissed on new pushes
- conversation resolution required
- linear history required
- force pushes disabled
- branch deletion disabled
- admin bypass remains allowed for now

## Topics

- `dotnet`
- `avalonia`
- `desktop-app`
- `release-planning`
- `local-first`
- `offline-collaboration`
- `cryptography`

## Labels

Default labels exist, plus these project labels:

- `app`
- `core`
- `storage`
- `collaboration`
- `security`
- `planning`
- `needs-triage`
- `breaking-change`

## Milestones

- `v0.1.0 Foundation`
- `v1.0.0`

## Project

- GitHub Project: `Blueprints Roadmap`

## Releases

- Current pre-release tag: `v0.1.0-alpha.1`
- A draft prerelease already exists for that tag

## Standard Local Checks

Run these before pushing or preparing release-related changes:

```powershell
dotnet build Blueprints.sln
dotnet test Blueprints.sln
git status --short
```

## Common Git Commands

Initialize and inspect:

```powershell
git status --short
git branch --show-current
git remote -v
git log --oneline --decorate -n 10
```

Commit and push:

```powershell
git add .
git commit -m "Describe the change"
git push origin main
```

Create and push a tag:

```powershell
git tag -a v0.1.0-alpha.2 -m "Describe the milestone"
git push origin v0.1.0-alpha.2
```

## Common GitHub CLI Commands

Check auth and repo:

```powershell
gh auth status
gh repo view ATAC-Helicopter/Blueprints
```

Inspect labels and milestones:

```powershell
gh label list --repo ATAC-Helicopter/Blueprints
gh api repos/ATAC-Helicopter/Blueprints/milestones
```

Create a label:

```powershell
gh label create my-label --repo ATAC-Helicopter/Blueprints --color 1D76DB --description "Short description"
```

Create a milestone:

```powershell
gh api repos/ATAC-Helicopter/Blueprints/milestones -f title="v0.2.0" -f state="open" -f description="Milestone description"
```

Create a release:

```powershell
gh release create v0.1.0-alpha.2 --repo ATAC-Helicopter/Blueprints --title "v0.1.0-alpha.2" --notes "Release notes here" --draft --prerelease
```

Edit repo settings:

```powershell
gh repo edit ATAC-Helicopter/Blueprints --description "Repository description"
```

## Public Release Readiness Checklist

Before switching the repository to public:

- choose and add a license
- review `README.md`
- review `SECURITY.md`
- confirm issue and PR templates are correct
- confirm CI is green
- confirm no sensitive data or secrets were committed
- review topics, labels, milestones, and release notes
- decide whether Discussions should stay disabled
- decide whether branch protection rules should be added

## Safe Defaults For Future GitHub Tasks

Unless there is a strong reason otherwise:

- keep the repository private until licensing and public docs are ready
- prefer draft prereleases before publishing anything final
- keep merge commits disabled
- use squash or rebase merge only
- treat security-related changes conservatively

## Full GitHub Maintenance Checklist

Use this section every time GitHub-related work is requested.

### 1. Local Repository State

Check:

- `git status --short`
- current branch
- whether local `main` and `develop` are up to date
- whether there are unpushed commits
- whether there are uncommitted changes that should not be mixed into repo admin work

Update if needed:

- pull latest `main`
- rebase local work before pushing
- commit repo-management files separately from product/code changes when possible

### 2. Remote Branch Hygiene

Check:

- current managed branches still match the roadmap
- old feature branches that can be deleted
- stale remote-tracking refs
- accidental or noisy branches from bots

Update if needed:

- create missing managed branches
- delete stale local branches
- delete stale remote branches that are no longer needed
- prune remote-tracking refs with `git fetch --prune origin`

### 3. Pull Requests

Check:

- open PR list
- whether any PR is stale, redundant, or superseded
- CI status on each open PR
- labels, milestones, assignees, and project linkage
- whether Dependabot PRs overlap or conflict

Update if needed:

- merge safe maintenance PRs after checks pass
- close redundant PRs with a clear reason
- rebase or refresh PRs if required
- ensure important PRs are attached to the right milestone/project

### 4. Issues

Check:

- whether active work has a corresponding GitHub issue
- whether issues are labeled correctly
- whether milestone assignment is missing
- whether roadmap issues still reflect actual priorities
- whether closed work should also close matching issues

Update if needed:

- create missing issues
- add or correct labels
- assign milestones
- add issues to the project board
- close or rewrite outdated issues

### 5. GitHub Project

Check:

- project exists and is linked to the repo
- roadmap items are actually present in the project
- items reflect current priorities
- project is missing newly created issues or PRs

Update if needed:

- add newly created issues/PRs
- remove obsolete items
- keep milestone and project intent aligned

### 6. Labels

Check:

- whether labels still match the repo workflow
- whether any new work area needs a label
- whether default labels are enough for triage
- whether noisy or unused labels should be retired

Update if needed:

- add missing labels
- rename or recolor labels only if it improves clarity
- keep security/planning/core/app/storage/collaboration labels available

### 7. Milestones

Check:

- whether new planned work belongs to an existing milestone
- whether milestone descriptions still match the scope
- whether completed milestones should be closed

Update if needed:

- create new milestones
- update milestone descriptions
- move issues/PRs into the correct milestone
- close milestones when complete

### 8. Releases and Tags

Check:

- latest tags
- whether draft releases match actual tags
- whether prerelease naming is still consistent
- whether a new tag is needed after meaningful milestones

Update if needed:

- create annotated tags
- push tags
- create or update draft prereleases
- publish releases only when intentionally ready

### 9. Repository Settings

Check:

- description
- topics
- merge strategy settings
- issue/project/wiki/discussions state
- default branch
- visibility

Update if needed:

- keep description accurate
- keep topics relevant
- keep merge commits disabled unless there is a specific reason
- review settings before public launch

### 10. GitHub Actions and CI

Check:

- workflow files still reflect the intended build/test process
- latest workflow runs are passing
- action versions are current enough
- CI is triggered on the intended branches and PRs

Update if needed:

- fix failing workflows quickly
- update action versions
- keep workflows minimal and deterministic
- avoid adding workflows without a clear maintenance owner

### 11. Dependabot

Check:

- whether grouped update behavior is still working
- whether there is PR spam again
- whether dependency PRs are stale or redundant
- whether update cadence is too noisy or too slow

Update if needed:

- adjust grouping rules
- adjust update intervals
- close superseded dependency PRs
- keep GitHub Actions and NuGet updates manageable

### 12. Templates and Community Files

Check:

- issue templates
- PR template
- `README.md`
- `CONTRIBUTING.md`
- `SECURITY.md`
- `CODE_OF_CONDUCT.md`
- `GitHubOpsReference.md`

Update if needed:

- keep templates aligned with the current workflow
- keep docs aligned with repo state
- update security guidance when reporting flow changes
- update this reference whenever repo operations materially change

### 13. Public Readiness

Check:

- license status
- public-facing README quality
- no sensitive data in repo history
- security policy is sensible
- branch protection availability
- release quality and naming

Update if needed:

- add a license before public release
- clean up docs and onboarding
- verify there are no secrets or unsafe artifacts
- apply branch protection once available

### 14. After Any GitHub/Admin Task

Always do:

- verify `git status --short`
- push any intended local repo-management commits
- confirm resulting GitHub state with `gh`
- update `GitHubOpsReference.md` if conventions changed
- avoid leaving the repo in a half-configured state

## Notes

This file should be updated whenever repository settings, release conventions, labels, milestones, or GitHub workflows materially change.
