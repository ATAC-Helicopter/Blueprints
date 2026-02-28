namespace Blueprints.App.Models;

public sealed record ChangelogExportResult(
    Guid VersionId,
    string VersionName,
    string FilePath,
    string Markdown);
