namespace Blueprints.Collaboration.Models;

public sealed record WorkspaceSyncResult(
    bool Success,
    string Operation,
    int AppliedDocumentCount,
    int ManifestVersion,
    string BatchId,
    IReadOnlyList<string> Conflicts,
    string Summary);
