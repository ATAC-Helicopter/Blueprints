namespace Blueprints.Collaboration.Models;

public sealed record ExchangeValidationResult(
    bool IsValid,
    IReadOnlyList<string> InvalidDocumentPaths);
