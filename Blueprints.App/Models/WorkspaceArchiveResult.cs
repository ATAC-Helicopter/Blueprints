namespace Blueprints.App.Models;

public sealed record WorkspaceArchiveResult(
    LocalWorkspaceSession Session,
    string ArchiveDirectory,
    string Summary);
