using Blueprints.App.Models;

namespace Blueprints.App.Services;

public static class ReleaseReadinessDiagnosticBuilder
{
    public static IReadOnlyList<ReleaseReadinessDiagnostic> Build(
        WorkspaceVersionCard? version,
        IntegrationStatusCard? localGit,
        IReadOnlyList<VersionSourceChangeDiagnostic> sourceChanges)
    {
        ArgumentNullException.ThrowIfNull(sourceChanges);
        var diagnostics = new List<ReleaseReadinessDiagnostic>();

        if (version is null)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Blocking,
                    "No release version selected",
                    "Source history cannot be evaluated without a target version.",
                    "Select or create a version before reviewing release readiness."));
            return diagnostics;
        }

        AddRepositoryDiagnostic(diagnostics, localGit);

        var incompleteItemCount = version.Items.Count(static item => !item.IsDone);
        if (incompleteItemCount > 0)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Incomplete release items",
                    $"{incompleteItemCount} selected-version items are not marked complete.",
                    "Finish them, move them to another version, or deliberately export them as incomplete."));
        }

        if (localGit is { State: IntegrationConnectionState.Connected or IntegrationConnectionState.Warning })
        {
            AddSourceChangeDiagnostics(diagnostics, sourceChanges);
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Ready,
                    "Source history is ready",
                    "The linked repository is clean and every recent change maps to a completed item in this version.",
                    "Review the changelog preview, then freeze or release when the human review is complete."));
        }

        return diagnostics;
    }

    private static void AddRepositoryDiagnostic(
        ICollection<ReleaseReadinessDiagnostic> diagnostics,
        IntegrationStatusCard? localGit)
    {
        if (localGit is null || localGit.State == IntegrationConnectionState.NotConfigured)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "No source repository linked",
                    "Blueprints cannot compare this release plan with source history.",
                    "Link a local Git repository in Source Lens. Core release planning remains available offline."));
            return;
        }

        if (localGit.State == IntegrationConnectionState.Error)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Blocking,
                    "Linked repository is unavailable",
                    localGit.Summary,
                    localGit.Guidance));
            return;
        }

        if (localGit.State == IntegrationConnectionState.Warning)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Blocking,
                    "Repository has uncommitted changes",
                    $"{localGit.Target} is not clean, so its source history is not a stable release baseline.",
                    "Review, commit, stash, or deliberately discard the working-tree changes outside Blueprints, then refresh health."));
        }
    }

    private static void AddSourceChangeDiagnostics(
        ICollection<ReleaseReadinessDiagnostic> diagnostics,
        IReadOnlyList<VersionSourceChangeDiagnostic> sourceChanges)
    {
        if (sourceChanges.Count == 0)
        {
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "No recent source changes found",
                    "There are no commits after the repository's latest tag to compare with this version.",
                    "Confirm the tag boundary and repository link are correct."));
            return;
        }

        var unmatched = sourceChanges
            .Where(static change => !change.MatchesSelectedVersion)
            .ToArray();
        if (unmatched.Length > 0)
        {
            var examples = string.Join(
                ", ",
                unmatched
                    .Take(3)
                    .Select(static change => $"{change.Change.ShortHash} {change.Change.Subject}"));
            diagnostics.Add(
                new ReleaseReadinessDiagnostic(
                    ReleaseReadinessLevel.Attention,
                    "Recent commits are unmatched",
                    $"{unmatched.Length} recent commits do not reference a completed item in this version. {examples}",
                    "Connect the commits to completed item keys, move the relevant item into this version, or confirm the commits are intentionally out of scope."));
        }
    }
}
