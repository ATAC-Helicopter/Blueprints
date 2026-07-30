namespace Blueprints.App.Models;

public sealed record ReleaseReadinessDiagnostic(
    ReleaseReadinessLevel Level,
    string Title,
    string Detail,
    string Guidance);
