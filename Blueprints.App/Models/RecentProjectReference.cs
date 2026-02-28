namespace Blueprints.App.Models;

public sealed record RecentProjectReference(
    string Name,
    string ProjectCode,
    string LocalWorkspaceRoot,
    string SharedWorkspaceRoot,
    DateTimeOffset LastOpenedUtc);
