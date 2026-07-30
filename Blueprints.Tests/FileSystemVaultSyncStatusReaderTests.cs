using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class FileSystemVaultSyncStatusReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Inspect_DetectsMetadataWithoutReadingSqlite()
    {
        var metadataPath = CreateMetadataStore();
        File.WriteAllText(metadataPath, "not a SQLite database");

        var status = new FileSystemVaultSyncStatusReader().Inspect(_root);

        Assert.True(status.MetadataStoreFound);
        Assert.Equal(metadataPath, status.MetadataStorePath);
        Assert.Equal("Unknown", status.RestoreReadiness);
        Assert.Contains(
            "not available",
            status.Warnings.Single(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_ReadsBoundedPassiveHealthDocument()
    {
        var metadataPath = CreateMetadataStore();
        var statusPath = Path.Combine(
            Path.GetDirectoryName(metadataPath)!,
            FileSystemVaultSyncStatusReader.StatusFileName);
        File.WriteAllText(
            statusPath,
            """
            {
              "schemaVersion": 1,
              "projectExternalId": "project-42",
              "projectName": "Blueprints",
              "destinationAlias": "NAS",
              "destinationReachable": true,
              "latestSnapshotUtc": "2026-07-30T20:00:00Z",
              "latestBackupUtc": "2026-07-30T21:00:00Z",
              "latestVerificationUtc": "2026-07-30T22:00:00Z",
              "backupIndexConsistent": true,
              "restoreReadiness": "Ready",
              "metadataConflictCount": 0,
              "warnings": []
            }
            """);

        var status = new FileSystemVaultSyncStatusReader().Inspect(_root);

        Assert.True(status.MetadataStoreFound);
        Assert.Equal("project-42", status.ProjectExternalId);
        Assert.Equal("Blueprints", status.ProjectName);
        Assert.Equal("NAS", status.DestinationAlias);
        Assert.True(status.DestinationReachable);
        Assert.True(status.BackupIndexConsistent);
        Assert.Equal("Ready", status.RestoreReadiness);
        Assert.Empty(status.Warnings);
        Assert.Contains("latest backup", status.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("vaultsync")]
    [InlineData("meta")]
    [InlineData("database")]
    public void Inspect_AcceptsEachDocumentedConfigurationLevel(string level)
    {
        var metadataPath = CreateMetadataStore();
        var configuredPath = level switch
        {
            "vaultsync" => Path.Combine(_root, ".vaultsync"),
            "meta" => Path.GetDirectoryName(metadataPath)!,
            _ => metadataPath,
        };

        var status = new FileSystemVaultSyncStatusReader().Inspect(configuredPath);

        Assert.True(status.MetadataStoreFound);
        Assert.Equal(metadataPath, status.MetadataStorePath);
    }

    [Fact]
    public void Inspect_RejectsUnsupportedOrMalformedHealthDocuments()
    {
        var metadataPath = CreateMetadataStore();
        var statusPath = Path.Combine(
            Path.GetDirectoryName(metadataPath)!,
            FileSystemVaultSyncStatusReader.StatusFileName);
        File.WriteAllText(statusPath, """{"schemaVersion":2,"restoreReadiness":"Ready"}""");

        var status = new FileSystemVaultSyncStatusReader().Inspect(_root);

        Assert.True(status.MetadataStoreFound);
        Assert.Equal("Unavailable", status.RestoreReadiness);
        Assert.Contains("schemaVersion 1", status.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspect_WarnsWhenRequiredHealthEvidenceIsMissing()
    {
        var metadataPath = CreateMetadataStore();
        var statusPath = Path.Combine(
            Path.GetDirectoryName(metadataPath)!,
            FileSystemVaultSyncStatusReader.StatusFileName);
        File.WriteAllText(
            statusPath,
            """{"schemaVersion":1,"restoreReadiness":"Ready"}""");

        var status = new FileSystemVaultSyncStatusReader().Inspect(_root);

        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("destination", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("backup", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("verification", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("index", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inspect_RejectsOversizedHealthDocumentsBeforeParsing()
    {
        var metadataPath = CreateMetadataStore();
        var statusPath = Path.Combine(
            Path.GetDirectoryName(metadataPath)!,
            FileSystemVaultSyncStatusReader.StatusFileName);
        using (var stream = File.Create(statusPath))
        {
            stream.SetLength(FileSystemVaultSyncStatusReader.MaximumStatusDocumentBytes + 1);
        }

        var status = new FileSystemVaultSyncStatusReader().Inspect(_root);

        Assert.Equal("Unavailable", status.RestoreReadiness);
        Assert.Contains("read limit", status.Summary, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateMetadataStore()
    {
        var metadataDirectory = Path.Combine(_root, ".vaultsync", "meta");
        Directory.CreateDirectory(metadataDirectory);
        var metadataPath = Path.Combine(
            metadataDirectory,
            FileSystemVaultSyncStatusReader.MetadataFileName);
        File.WriteAllBytes(metadataPath, []);
        return metadataPath;
    }
}
