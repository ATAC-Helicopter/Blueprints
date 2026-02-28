using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Storage.Models;

namespace Blueprints.Tests;

internal static class TestWorkspaceFactory
{
    public static ProjectWorkspaceSnapshot CreateWorkspaceSnapshot(Guid? projectId = null)
    {
        var resolvedProjectId = projectId ?? Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow;

        return new ProjectWorkspaceSnapshot(
            new ProjectConfigurationDocument(
                1,
                resolvedProjectId,
                "VaultSync",
                "VS",
                "SemVer",
                createdUtc,
                [
                    new CategoryDefinition("feature", "Feature"),
                    new CategoryDefinition("bug", "Bug"),
                ],
                new Dictionary<string, ItemTypeDefinition>
                {
                    ["feature"] = new("feature", "Feature"),
                    ["bug"] = new("bug", "Bug"),
                },
                new Dictionary<string, ItemKeyRule>
                {
                    ["feature"] = new("VS", ItemKeyScope.Version),
                    ["bug"] = new("BUG", ItemKeyScope.Project),
                },
                new ChangelogRules(false, true, false, false)),
            new MemberDocument(
                1,
                resolvedProjectId,
                1,
                [
                    new ProjectMember(
                        userId,
                        "Flavio",
                        "public-key",
                        MemberRole.Admin,
                        createdUtc,
                        true),
                ]),
            [
                new VersionWorkspaceSnapshot(
                    new VersionDocument(
                        1,
                        resolvedProjectId,
                        versionId,
                        "1.0.0",
                        ReleaseStatus.InProgress,
                        createdUtc,
                        null,
                        null,
                        [itemId]),
                    [
                        new ItemDocument(
                            1,
                            resolvedProjectId,
                            versionId,
                            itemId,
                            "VS-1001",
                            "feature",
                            "feature",
                            "Create signed workspace persistence",
                            null,
                            false,
                            [],
                            createdUtc,
                            createdUtc,
                            userId,
                            "Flavio"),
                    ]),
            ]);
    }
}
