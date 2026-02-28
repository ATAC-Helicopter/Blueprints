namespace Blueprints.App.Models;

public sealed record ProjectCreateRequest(
    string Name,
    string ProjectCode,
    string VersioningScheme,
    string LocalWorkspaceRoot,
    string SharedWorkspaceRoot);
