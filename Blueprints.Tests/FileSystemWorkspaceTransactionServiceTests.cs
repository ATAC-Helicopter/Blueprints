using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class FileSystemWorkspaceTransactionServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "WorkspaceTransactions",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Execute_PromotesCompleteStagedWorkspace()
    {
        var workspaceRoot = Path.Combine(_testRoot, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(Path.Combine(workspaceRoot, "before.txt"), "before");

        var service = new FileSystemWorkspaceTransactionService();
        service.Execute(workspaceRoot, stagedRoot =>
        {
            File.Delete(Path.Combine(stagedRoot, "before.txt"));
            File.WriteAllText(Path.Combine(stagedRoot, "after.txt"), "after");
        });

        Assert.False(File.Exists(Path.Combine(workspaceRoot, "before.txt")));
        Assert.Equal("after", File.ReadAllText(Path.Combine(workspaceRoot, "after.txt")));
        AssertNoTransactionArtifacts(workspaceRoot);
    }

    [Fact]
    public void Execute_WhenStagedWriteFails_LeavesOriginalWorkspaceUntouched()
    {
        var workspaceRoot = Path.Combine(_testRoot, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(Path.Combine(workspaceRoot, "project.txt"), "trusted");
        var service = new FileSystemWorkspaceTransactionService();

        Assert.Throws<IOException>(() =>
            service.Execute(workspaceRoot, stagedRoot =>
            {
                File.WriteAllText(Path.Combine(stagedRoot, "project.txt"), "partial");
                throw new IOException("Simulated write interruption.");
            }));

        Assert.Equal("trusted", File.ReadAllText(Path.Combine(workspaceRoot, "project.txt")));
        AssertNoTransactionArtifacts(workspaceRoot);
    }

    [Theory]
    [InlineData(WorkspaceTransactionPhase.Staged)]
    [InlineData(WorkspaceTransactionPhase.OriginalBackedUp)]
    [InlineData(WorkspaceTransactionPhase.Promoted)]
    public void Execute_WhenPromotionIsInterrupted_RestoresOriginalWorkspace(
        WorkspaceTransactionPhase interruptedPhase)
    {
        var workspaceRoot = Path.Combine(_testRoot, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(Path.Combine(workspaceRoot, "project.txt"), "trusted");
        var service = new FileSystemWorkspaceTransactionService(phase =>
        {
            if (phase == interruptedPhase)
            {
                throw new IOException($"Interrupted at {phase}.");
            }
        });

        Assert.Throws<IOException>(() =>
            service.Execute(workspaceRoot, stagedRoot =>
                File.WriteAllText(Path.Combine(stagedRoot, "project.txt"), "replacement")));

        Assert.Equal("trusted", File.ReadAllText(Path.Combine(workspaceRoot, "project.txt")));
        AssertNoTransactionArtifacts(workspaceRoot);
    }

    [Fact]
    public void Execute_RejectsLinkedWorkspaceEntries()
    {
        var workspaceRoot = Path.Combine(_testRoot, "workspace");
        var externalRoot = Path.Combine(_testRoot, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);
        File.WriteAllText(Path.Combine(externalRoot, "outside.txt"), "outside");
        var linkPath = Path.Combine(workspaceRoot, "linked");

        try
        {
            Directory.CreateSymbolicLink(linkPath, externalRoot);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
                or IOException
                or PlatformNotSupportedException)
        {
            return;
        }

        var service = new FileSystemWorkspaceTransactionService();
        var error = Assert.Throws<InvalidOperationException>(() =>
            service.Execute(workspaceRoot, _ => { }));

        Assert.Contains("linked entries", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("outside", File.ReadAllText(Path.Combine(externalRoot, "outside.txt")));
    }

    [Fact]
    public void Recover_RejectsMarkerThatRedirectsBackupOutsideWorkspacePaths()
    {
        var workspaceRoot = Path.Combine(_testRoot, "workspace");
        var outsideRoot = Path.Combine(_testRoot, "must-not-delete");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllText(Path.Combine(outsideRoot, "evidence.txt"), "preserve");
        var markerPath = Path.Combine(
            _testRoot,
            ".workspace.blueprints-transaction.json");
        File.WriteAllText(
            markerPath,
            $$"""
            {
              "schemaVersion": 1,
              "workspaceRoot": "{{workspaceRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}}",
              "stagingRoot": "{{Path.Combine(_testRoot, ".workspace.blueprints-staging").Replace("\\", "\\\\", StringComparison.Ordinal)}}",
              "backupRoot": "{{outsideRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}}",
              "state": "backedUp",
              "updatedUtc": "2026-07-31T00:00:00Z"
            }
            """);
        var service = new FileSystemWorkspaceTransactionService();

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.Recover(workspaceRoot));

        Assert.Contains("does not match", error.Message, StringComparison.Ordinal);
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(outsideRoot, "evidence.txt")));
    }

    [Fact]
    public void Execute_DoesNotDeleteUntrackedReservedSibling()
    {
        var workspaceRoot = Path.Combine(_testRoot, "workspace");
        var reservedRoot = Path.Combine(
            _testRoot,
            ".workspace.blueprints-staging");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(reservedRoot);
        File.WriteAllText(Path.Combine(reservedRoot, "evidence.txt"), "preserve");
        var service = new FileSystemWorkspaceTransactionService();

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.Execute(workspaceRoot, _ => { }));

        Assert.Contains("Untracked", error.Message, StringComparison.Ordinal);
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(reservedRoot, "evidence.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static void AssertNoTransactionArtifacts(string workspaceRoot)
    {
        var parent = Path.GetDirectoryName(workspaceRoot)!;
        var name = Path.GetFileName(workspaceRoot);
        Assert.False(Directory.Exists(Path.Combine(parent, $".{name}.blueprints-staging")));
        Assert.False(Directory.Exists(Path.Combine(parent, $".{name}.blueprints-backup")));
        Assert.False(File.Exists(Path.Combine(parent, $".{name}.blueprints-transaction.json")));
    }
}
