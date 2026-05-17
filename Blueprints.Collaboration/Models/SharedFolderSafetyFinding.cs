namespace Blueprints.Collaboration.Models;

public sealed record SharedFolderSafetyFinding(
    string Code,
    string Severity,
    string Message);
