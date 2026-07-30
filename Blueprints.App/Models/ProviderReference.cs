namespace Blueprints.App.Models;

public sealed record ProviderReference(
    SourceProviderKind Provider,
    ProviderReferenceKind Kind,
    string Repository,
    string Identifier,
    string? WebUrl)
{
    public string DisplaySummary =>
        string.IsNullOrWhiteSpace(Repository)
            ? $"{Provider} {Kind} {Identifier}"
            : $"{Provider} {Repository} · {Kind} {Identifier}";
}
