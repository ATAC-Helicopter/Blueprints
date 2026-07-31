namespace Blueprints.App.Models;

[Flags]
public enum SourceProviderCapabilities
{
    None = 0,
    Issues = 1 << 0,
    ChangeRequests = 1 << 1,
    Releases = 1 << 2,
    PlanningBoards = 1 << 3,
}
