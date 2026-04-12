# User Workflows

## Overview

The finished Blueprints app is intended to support a complete workflow from project setup to release export. This document describes how a user is expected to move through the app once the product is complete.

## 1. Create A Project

An admin creates a new project from the create-project flow.

They provide:

- project name
- project code
- versioning scheme
- default categories
- item types
- item key rules
- changelog defaults
- shared folder location

Once created, the app generates the signed project configuration and initializes the local workspace plus the shared project structure.

Expected result:

- the new project appears in the workspace list
- the current user becomes the founding admin
- the project opens in a trusted state unless the shared folder safety checks raise warnings

## 2. Open Or Import A Project

A user can open:

- a recent local workspace
- an imported shared project
- an invited project package or reference

When opening a project, the app should:

1. validate required files
2. validate signatures
3. validate membership authority
4. assess shared-folder safety
5. present the resulting trust state

If the project is healthy, it opens normally. If not, the app is intended to open it read-only and explain why.

## 3. Invite A Team Member

Membership is invitation-only.

Expected admin flow:

1. Open the project members screen.
2. Choose `Invite Member`.
3. Add the invitee using their public key bundle.
4. Assign a role.
5. Save the membership change.
6. Push the updated signed membership state to the shared location.

Expected invitee flow:

1. Open the app with their local identity already created.
2. Open the shared project or imported invitation.
3. Verify that their public key appears in the signed membership file.
4. Create a local workspace for the project.
5. Pull the initial trusted project state.

## 4. Create And Organize Versions

Versions are the main planning containers.

Typical workflow:

1. Create a new version such as `1.6.0`.
2. Set its initial state to `Planned` or `In Progress`.
3. Add optional release notes or context.
4. Begin adding items under the appropriate categories.

Users should be able to browse versions easily and understand:

- how many items each version contains
- how many are complete
- which version is currently active
- whether a version is planned, in progress, frozen, or released

## 5. Create Items

Inside a version, an editor creates items that represent release-relevant work.

The intended create-item flow is:

1. Choose an item type.
2. Choose a category.
3. Enter a title.
4. Optionally add a description and tags.
5. Save the item.

The app should then:

- generate the item key automatically
- assign timestamps and modification metadata
- sign the saved item document
- place the item in the selected version/category

The goal is to make item creation fast while still keeping the data structured enough for changelog generation and trust validation.

## 6. Edit, Move, And Reorder Items

Users are expected to update items as release planning evolves.

Supported future actions should include:

- changing title or description
- marking an item done
- moving an item between categories
- moving an item between versions where policy allows
- reordering items manually within a category
- updating tags

Ordering rules are intended to work like this:

- manual order takes precedence
- chronological order is the fallback

This keeps release notes readable while still supporting deliberate editorial control.

## 7. Freeze A Version

When a version is nearing release, an admin or authorized editor can freeze it.

The meaning of `Frozen` is:

- normal editing is blocked
- the release content is treated as locked for review
- exceptions require an explicit override path

The product direction is to make freeze meaningful, not decorative. A frozen version should feel operationally different from a version that is merely “almost done.”

## 8. Release A Version

Releasing a version is intended to be a deliberate action.

Expected flow:

1. Review version contents.
2. Confirm changelog settings.
3. Ensure trust state is valid.
4. Mark the version as released.
5. Set `ReleasedUtc`.
6. Export or copy the changelog output.

After release:

- the version becomes immutable in normal workflows
- post-release work should happen in a new version
- incomplete work may be moved forward if the release flow offers that option

## 9. Sync With The Team

Blueprints is designed around local workspaces plus a shared sync location.

Push flow:

1. Validate local state.
2. Package changed signed entities.
3. Write a staged sync batch.
4. Publish it to the shared folder.
5. Update shared sync metadata.

Pull flow:

1. Detect remote changes.
2. Download them into the local inbox.
3. Validate signatures and authority.
4. Merge safe changes automatically.
5. Raise conflicts where needed.
6. Update local sync state only after successful application.

The user should never need to reason about raw file copies. The app should present sync as an intentional operation with visible trust and conflict outcomes.

## 10. Resolve Conflicts

When automatic merge is unsafe, the app should open a conflict workflow.

The intended conflict UI shows:

- `Mine`
- `Theirs`
- a result preview

Available actions should include:

- keep mine
- keep theirs
- combine where safe for text-based fields

Membership conflicts are stricter and should always require admin attention.

## 11. Export A Changelog

Blueprints is meant to produce changelog-ready output from the release plan itself.

Expected export flow:

1. Open a version or release preview.
2. Review the categorized item list.
3. Choose export options.
4. Preview the final Markdown.
5. Export or copy it.

Typical options include:

- include only completed items
- include item keys
- include descriptions
- include release date
- use compact mode

The ideal outcome is that the release plan and the release notes are not separate manual processes.
