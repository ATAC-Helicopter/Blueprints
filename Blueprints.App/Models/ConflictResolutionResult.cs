namespace Blueprints.App.Models;

public sealed record ConflictResolutionResult(
    string DocumentPath,
    ConflictResolutionChoice Choice,
    string Summary);
