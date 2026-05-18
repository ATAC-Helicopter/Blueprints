using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.Core.Enums;

namespace Blueprints.Tests;

public sealed class VersionSourceChangeDiagnosticBuilderTests
{
    [Fact]
    public void Build_MatchesOnlyDoneItemsThatBelongToSelectedVersion()
    {
        var selectedVersion = new WorkspaceVersionCard(
            Guid.NewGuid(),
            "1.5.0",
            ReleaseStatus.InProgress,
            null,
            2,
            1,
            [
                new WorkspaceItemCard(
                    Guid.NewGuid(),
                    "BP-151",
                    "feature",
                    "added",
                    "Ship project workflow",
                    null,
                    true),
                new WorkspaceItemCard(
                    Guid.NewGuid(),
                    "BP-152",
                    "bug",
                    "fixed",
                    "Incomplete fix",
                    null,
                    false),
            ]);
        var sourceChanges = new[]
        {
            new SourceChangeSummary(
                "abcdef1234567890",
                "abcdef1",
                "BP-151 Ship project workflow",
                "Flavio",
                DateTimeOffset.Parse("2026-05-18T12:00:00Z"),
                ["BP-151"]),
            new SourceChangeSummary(
                "1111111111111111",
                "1111111",
                "BP-152 Incomplete fix",
                "Flavio",
                DateTimeOffset.Parse("2026-05-18T12:30:00Z"),
                ["BP-152"]),
            new SourceChangeSummary(
                "2222222222222222",
                "2222222",
                "Tidy unmatched change",
                "Flavio",
                DateTimeOffset.Parse("2026-05-18T13:00:00Z"),
                []),
        };

        var diagnostics = VersionSourceChangeDiagnosticBuilder.Build(selectedVersion, sourceChanges);

        Assert.Equal(3, diagnostics.Count);
        Assert.True(diagnostics[0].MatchesSelectedVersion);
        Assert.Equal("BP-151", diagnostics[0].MatchingItemKeys.Single());
        Assert.False(diagnostics[1].MatchesSelectedVersion);
        Assert.False(diagnostics[2].MatchesSelectedVersion);
    }
}
