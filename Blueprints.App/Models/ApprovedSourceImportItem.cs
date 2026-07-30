namespace Blueprints.App.Models;

public sealed record ApprovedSourceImportItem(
    Guid VersionId,
    string ItemTypeId,
    string CategoryId,
    string Title,
    string? Description,
    bool IsDone,
    SourceArtifactKind SourceKind,
    string SourceReference);
