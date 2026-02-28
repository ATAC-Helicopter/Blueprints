namespace Blueprints.Core.Models;

public sealed record ProjectConfigurationDocument(
    int SchemaVersion,
    Guid ProjectId,
    string Name,
    string ProjectCode,
    string VersioningScheme,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<CategoryDefinition> DefaultCategories,
    IReadOnlyDictionary<string, ItemTypeDefinition> ItemTypes,
    IReadOnlyDictionary<string, ItemKeyRule> ItemKeyRules,
    ChangelogRules ChangelogRules);
