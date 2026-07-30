namespace Blueprints.App.Models;

public sealed record HostedRepositoryDescriptor(
    SourceProviderKind Provider,
    string RepositoryName);
