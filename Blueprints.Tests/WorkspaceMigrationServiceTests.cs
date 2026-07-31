using System.Text.Json;
using Blueprints.Security.Models;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class WorkspaceMigrationServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "WorkspaceMigrations",
        Guid.NewGuid().ToString("N"));
    private static readonly SignatureKeyMaterial TestKey =
        new("test", new byte[32]);

    [Fact]
    public void MigrateIfNeeded_CurrentSchemaDoesNotRewriteWorkspace()
    {
        var workspaceRoot = CreateWorkspace(schemaVersion: 1);
        var projectPath = Path.Combine(workspaceRoot, "project", "project.json");
        var original = File.ReadAllText(projectPath);
        var service = new WorkspaceMigrationService(
            new FileSystemWorkspaceTransactionService());

        var result = service.MigrateIfNeeded(workspaceRoot, TestKey);

        Assert.False(result.WasMigrated);
        Assert.Null(result.BackupPath);
        Assert.Equal(original, File.ReadAllText(projectPath));
    }

    [Fact]
    public void MigrateIfNeeded_FutureSchemaExplainsRequiredUpgrade()
    {
        var workspaceRoot = CreateWorkspace(schemaVersion: 2);
        var service = new WorkspaceMigrationService(
            new FileSystemWorkspaceTransactionService());

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.MigrateIfNeeded(workspaceRoot, TestKey));

        Assert.Contains("Update Blueprints", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, WorkspaceSchemaInspector.ReadSchemaVersion(workspaceRoot));
    }

    [Fact]
    public void MigrateIfNeeded_AppliesOrderedMigrationAndKeepsBackup()
    {
        var workspaceRoot = CreateWorkspace(schemaVersion: 1);
        var service = new WorkspaceMigrationService(
            new FileSystemWorkspaceTransactionService(),
            [new TestMigration()],
            targetSchemaVersion: 2);

        var result = service.MigrateIfNeeded(workspaceRoot, TestKey);

        Assert.True(result.WasMigrated);
        Assert.Equal([2], result.AppliedSchemaVersions);
        Assert.Equal(2, WorkspaceSchemaInspector.ReadSchemaVersion(workspaceRoot));
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
    }

    [Fact]
    public void MigrateIfNeeded_WhenMigrationFails_RestoresWorkspaceAndRemovesBackup()
    {
        var workspaceRoot = CreateWorkspace(schemaVersion: 1);
        var service = new WorkspaceMigrationService(
            new FileSystemWorkspaceTransactionService(),
            [new FailingMigration()],
            targetSchemaVersion: 2);

        Assert.Throws<IOException>(() =>
            service.MigrateIfNeeded(workspaceRoot, TestKey));

        Assert.Equal(1, WorkspaceSchemaInspector.ReadSchemaVersion(workspaceRoot));
        var parent = Path.GetDirectoryName(workspaceRoot)!;
        var backupRoot = Path.Combine(
            parent,
            $".{Path.GetFileName(workspaceRoot)}.blueprints-migration-backups");
        Assert.Empty(Directory.EnumerateFiles(backupRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private string CreateWorkspace(int schemaVersion)
    {
        var root = Path.Combine(_testRoot, $"workspace-{schemaVersion}");
        var projectRoot = Path.Combine(root, "project");
        Directory.CreateDirectory(projectRoot);
        File.WriteAllText(
            Path.Combine(projectRoot, "project.json"),
            JsonSerializer.Serialize(new { schemaVersion, name = "Migration test" }));
        return root;
    }

    private sealed class TestMigration : IWorkspaceMigration
    {
        public int SourceSchemaVersion => 1;

        public int TargetSchemaVersion => 2;

        public void Apply(string stagedWorkspaceRoot, SignatureKeyMaterial signingKey)
        {
            var path = Path.Combine(stagedWorkspaceRoot, "project", "project.json");
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new { schemaVersion = 2, name = "Migrated" }));
        }
    }

    private sealed class FailingMigration : IWorkspaceMigration
    {
        public int SourceSchemaVersion => 1;

        public int TargetSchemaVersion => 2;

        public void Apply(string stagedWorkspaceRoot, SignatureKeyMaterial signingKey)
        {
            var path = Path.Combine(stagedWorkspaceRoot, "project", "project.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = 2 }));
            throw new IOException("Simulated migration interruption.");
        }
    }
}
