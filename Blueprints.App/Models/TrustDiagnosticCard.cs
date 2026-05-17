namespace Blueprints.App.Models;

public sealed record TrustDiagnosticCard(
    string Severity,
    string Area,
    string Summary,
    string Guidance);
