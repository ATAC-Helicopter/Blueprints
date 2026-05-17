namespace Blueprints.App.Models;

public sealed record SyncDiagnosticCard(
    string Severity,
    string Source,
    string Path,
    string Message);
