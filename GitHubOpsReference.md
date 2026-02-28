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

## Notes

This file should be updated whenever repository settings, release conventions, labels, milestones, or GitHub workflows materially change.
