namespace Blueprints.App.Models;

public sealed record ProviderOperationIntent(
    SourceProviderKind Provider,
    string Repository,
    ProviderOperationKind Operation,
    string Target);
