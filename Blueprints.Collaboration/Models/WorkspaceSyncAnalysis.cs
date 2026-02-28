namespace Blueprints.Collaboration.Models;

public sealed record WorkspaceSyncAnalysis(
    IReadOnlyList<string> OutgoingDocumentPaths,
    IReadOnlyList<string> IncomingDocumentPaths,
    IReadOnlyList<string> PotentialConflictDocumentPaths)
{
    public bool HasOutgoingChanges => OutgoingDocumentPaths.Count > 0;

    public bool HasIncomingChanges => IncomingDocumentPaths.Count > 0;

    public bool HasConflicts => PotentialConflictDocumentPaths.Count > 0;
}
