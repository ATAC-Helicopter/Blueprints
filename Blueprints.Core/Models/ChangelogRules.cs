namespace Blueprints.Core.Models;

public sealed record ChangelogRules(
    bool IncludeIncompleteByDefault,
    bool IncludeItemKeysByDefault,
    bool IncludeDescriptionsByDefault,
    bool CompactModeByDefault);
