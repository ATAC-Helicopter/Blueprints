namespace Blueprints.Collaboration.Models;

public sealed record SharedFolderSafetyReport(
    bool IsSafe,
    IReadOnlyList<SharedFolderSafetyFinding> Findings)
{
    public static SharedFolderSafetyReport Safe() => new(true, []);
}
