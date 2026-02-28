using Blueprints.Core.Enums;

namespace Blueprints.Security.Models;

public sealed record TrustReport(
    TrustState State,
    string Summary,
    DateTimeOffset EvaluatedAtUtc);
