using Blueprints.App.Services;
using Blueprints.Collaboration.Services;
using Blueprints.Core.Enums;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class VaultSyncRestoreDrillTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "VaultSyncRestoreDrill",
        Guid.NewGuid().ToString("N"));
    private readonly SignatureKeyMaterial _signingKey;
    private readonly SignaturePublicKey _publicKey;
    private readonly FileSystemProjectWorkspaceStore _workspaceStore;
    private readonly FileSystemAuditLogService _auditLog;
    private readonly FileSystemWorkspaceSyncService _syncService;

    public VaultSyncRestoreDrillTests()
    {
        var keyPair = new Ed25519KeyPairGenerator().Generate("restore-admin");
        _signingKey = new SignatureKeyMaterial(
            keyPair.KeyId,
            keyPair.PrivateKeyBytes);
        _publicKey = new SignaturePublicKey(
            keyPair.KeyId,
            keyPair.PublicKeyBytes);
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        _workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        _auditLog = new FileSystemAuditLogService(signedStore);
        var snapshotBuilder = new WorkspaceExchangeSnapshotBuilder();
        _syncService = new FileSystemWorkspaceSyncService(
            snapshotBuilder,
            new WorkspaceSyncAnalyzer(snapshotBuilder),
            new FileSystemSyncManifestStore(signedStore, snapshotBuilder),
            new FileSystemSyncStateStore(),
            new WorkspaceExchangeValidator(new Ed25519SignatureService()),
            _auditLog);
    }

    [Fact]
    public void LocalAndVaultSyncExchangeBackups_CanBeRelocatedAndContinued()
    {
        var workspace = TestWorkspaceFactory.CreateWorkspaceSnapshot();
        var member = Assert.Single(workspace.Members.Members);
        var localRoot = Path.Combine(_rootDirectory, "active", "local");
        var destinationRoot = Path.Combine(_rootDirectory, "active", "destination");
        CreateMetadataStore(destinationRoot);
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();
        var intent = adapter.PrepareIntent(
            destinationRoot,
            workspace.Project.ProjectId);
        var now = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var registration = adapter.Register(
            intent,
            adapter.Approve(intent, now),
            now);

        _workspaceStore.Save(localRoot, workspace, _signingKey);
        _auditLog.Append(
            localRoot,
            workspace.Project.ProjectId,
            "project.create",
            "Created restore-drill project.",
            member.UserId,
            member.DisplayName,
            workspace.Members.MembershipRevision,
            _signingKey);
        var initialPush = _syncService.Push(
            new WorkspacePaths(localRoot, registration.ExchangeRoot),
            workspace.Project.ProjectId,
            _signingKey,
            _publicKey);
        Assert.True(initialPush.Success);

        var localBackup = Path.Combine(_rootDirectory, "backup", "local");
        var exchangeBackup = Path.Combine(_rootDirectory, "backup", "exchange");
        CopyDirectory(localRoot, localBackup);
        CopyDirectory(registration.ExchangeRoot, exchangeBackup);

        var restoredLocalRoot = Path.Combine(_rootDirectory, "restored", "local");
        CopyDirectory(localBackup, restoredLocalRoot);
        var restoredLocal = _workspaceStore.Load(restoredLocalRoot, _publicKey);
        Assert.Equal(TrustState.Trusted, restoredLocal.TrustReport.State);
        Assert.Equal(
            workspace.Project.ProjectId,
            restoredLocal.Workspace.Project.ProjectId);

        var restoredDestinationRoot = Path.Combine(
            _rootDirectory,
            "restored",
            "destination");
        CreateMetadataStore(restoredDestinationRoot);
        var restoredIntent = adapter.PrepareIntent(
            restoredDestinationRoot,
            workspace.Project.ProjectId);
        CopyDirectory(exchangeBackup, restoredIntent.ExchangeRoot);
        var restoredRegistration = adapter.Register(
            restoredIntent,
            adapter.Approve(restoredIntent, now.AddMinutes(1)),
            now.AddMinutes(1));
        Assert.True(restoredRegistration.AlreadyRegistered);

        var restoredPull = _syncService.Pull(
            new WorkspacePaths(
                restoredLocalRoot,
                restoredRegistration.ExchangeRoot),
            _publicKey);
        Assert.True(restoredPull.Success);

        SaveVersionNotes(
            restoredLocalRoot,
            "Continued after local and exchange restore.");
        var continuedPush = _syncService.Push(
            new WorkspacePaths(
                restoredLocalRoot,
                restoredRegistration.ExchangeRoot),
            workspace.Project.ProjectId,
            _signingKey,
            _publicKey);

        Assert.True(continuedPush.Success);
        Assert.True(
            File.Exists(
                Path.Combine(
                    restoredRegistration.ExchangeRoot,
                    FileSystemVaultSyncExchangeRootAdapter.RegistrationMarkerFileName)));
        var restoredExchange = _workspaceStore.Load(
            restoredRegistration.ExchangeRoot,
            _publicKey);
        Assert.Equal(TrustState.Trusted, restoredExchange.TrustReport.State);
        Assert.Equal(
            "Continued after local and exchange restore.",
            restoredExchange.Workspace.Versions.Single().Version.Notes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private void SaveVersionNotes(string workspaceRoot, string notes)
    {
        var current = _workspaceStore.Load(workspaceRoot, _publicKey);
        Assert.Equal(TrustState.Trusted, current.TrustReport.State);
        var version = Assert.Single(current.Workspace.Versions);
        var updated = current.Workspace with
        {
            Versions =
            [
                version with
                {
                    Version = version.Version with { Notes = notes },
                },
            ],
        };
        _workspaceStore.Save(workspaceRoot, updated, _signingKey);
        var member = Assert.Single(updated.Members.Members);
        _auditLog.Append(
            workspaceRoot,
            updated.Project.ProjectId,
            "version.save",
            "Continued the project after the restore drill.",
            member.UserId,
            member.DisplayName,
            updated.Members.MembershipRevision,
            _signingKey);
    }

    private static void CreateMetadataStore(string destinationRoot)
    {
        var metadataDirectory = Path.Combine(
            destinationRoot,
            ".vaultsync",
            "meta");
        Directory.CreateDirectory(metadataDirectory);
        File.WriteAllBytes(
            Path.Combine(
                metadataDirectory,
                FileSystemVaultSyncStatusReader.MetadataFileName),
            []);
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(
                    destinationRoot,
                    Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var destinationPath = Path.Combine(
                destinationRoot,
                Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath);
        }
    }
}
