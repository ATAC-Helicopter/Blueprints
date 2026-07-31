using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.App.ViewModels;
using Blueprints.Collaboration.Services;
using Blueprints.Core.Enums;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class MainWindowWorkflowTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "MainWindowWorkflow",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Commands_CreateEditFreezeReleasePreviewAndExport()
    {
        var viewModel = new MainWindowViewModel(
            CreateCoordinator(),
            new IntegrationStatusService());
        var localRoot = Path.Combine(_rootDirectory, "local", "BP");
        var sharedRoot = Path.Combine(_rootDirectory, "shared", "BP");

        viewModel.IdentitySetupName = "Release Admin";
        viewModel.CreateLocalIdentityCommand.Execute(null);
        viewModel.CreateProjectName = "Blueprints";
        viewModel.CreateProjectCode = "BP";
        viewModel.CreateVersioningScheme = "SemVer";
        viewModel.CreateLocalWorkspaceRoot = localRoot;
        viewModel.CreateSharedWorkspaceRoot = sharedRoot;
        viewModel.CreateProjectCommand.Execute(null);

        Assert.True(viewModel.HasActiveSession);
        Assert.Equal("Release Admin", viewModel.Identity.DisplayName);

        viewModel.NewVersionName = "0.3.0";
        viewModel.CreateVersionCommand.Execute(null);
        var created = Assert.Single(viewModel.Versions);
        viewModel.SelectedVersion = created;
        viewModel.VersionEditorNotes = "Collaboration milestone";
        viewModel.SaveVersionDetailsCommand.Execute(null);

        viewModel.VersionEditorStatus = ReleaseStatus.Frozen;
        viewModel.SaveVersionDetailsCommand.Execute(null);
        Assert.Equal(ReleaseStatus.Frozen, viewModel.SelectedVersion?.Status);

        viewModel.ReleaseSelectedVersionCommand.Execute(null);
        Assert.Equal(ReleaseStatus.Released, viewModel.SelectedVersion?.Status);

        viewModel.ChangelogCompactMode = true;
        viewModel.PreviewSelectedVersionChangelogCommand.Execute(null);
        Assert.Contains("# Blueprints 0.3.0", viewModel.ChangelogPreview, StringComparison.Ordinal);
        Assert.Empty(viewModel.LastChangelogExportPath);

        viewModel.ExportSelectedVersionChangelogCommand.Execute(null);
        Assert.True(File.Exists(viewModel.LastChangelogExportPath));
        Assert.Contains("Exported changelog", viewModel.WorkspaceMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Commands_RegisterVaultSyncExchangeRootOnlyAfterSecondExplicitAction()
    {
        var destinationRoot = Path.Combine(_rootDirectory, "vaultsync-destination");
        var metadataDirectory = Path.Combine(destinationRoot, ".vaultsync", "meta");
        Directory.CreateDirectory(metadataDirectory);
        File.WriteAllBytes(
            Path.Combine(
                metadataDirectory,
                FileSystemVaultSyncStatusReader.MetadataFileName),
            []);
        var settingsStore = new FileSystemIntegrationSettingsStore(
            Path.Combine(_rootDirectory, "integrations.json"));
        var integrationService = new IntegrationStatusService(
            settingsStore,
            new TestLocalGitRepositoryInspector());
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();
        var viewModel = new MainWindowViewModel(
            CreateCoordinator(),
            integrationService,
            vaultSyncExchangeRootAdapter: adapter);
        viewModel.IdentitySetupName = "Exchange Admin";
        viewModel.CreateLocalIdentityCommand.Execute(null);
        viewModel.CreateProjectName = "Blueprints";
        viewModel.CreateProjectCode = "BP";
        viewModel.CreateLocalWorkspaceRoot = Path.Combine(_rootDirectory, "local", "BP");
        viewModel.CreateSharedWorkspaceRoot = Path.Combine(_rootDirectory, "shared", "BP");
        viewModel.CreateProjectCommand.Execute(null);
        viewModel.VaultSyncMetadataRoot = destinationRoot;
        viewModel.SaveVaultSyncMetadataRootCommand.Execute(null);
        var expectedRoot = adapter.PrepareIntent(
            destinationRoot,
            viewModel.CurrentProject.ProjectId).ExchangeRoot;

        Assert.True(viewModel.CanRegisterVaultSyncExchangeRoot);
        viewModel.RegisterVaultSyncExchangeRootCommand.Execute(null);

        Assert.False(Directory.Exists(expectedRoot));
        Assert.Contains("again", viewModel.IntegrationMessage, StringComparison.OrdinalIgnoreCase);

        viewModel.RegisterVaultSyncExchangeRootCommand.Execute(null);

        Assert.True(Directory.Exists(expectedRoot));
        Assert.True(
            File.Exists(
                Path.Combine(
                    expectedRoot,
                    FileSystemVaultSyncExchangeRootAdapter.RegistrationMarkerFileName)));
        Assert.Equal(
            expectedRoot,
            settingsStore.Load().RegisteredVaultSyncExchangeRoot);
        Assert.NotEqual(expectedRoot, viewModel.SharedSyncPath);

        viewModel.VaultSyncMetadataRoot = string.Empty;
        viewModel.SaveVaultSyncMetadataRootCommand.Execute(null);

        Assert.Empty(settingsStore.Load().RegisteredVaultSyncExchangeRoot);
    }

    private ProjectWorkspaceCoordinatorService CreateCoordinator()
    {
        var identityRoot = Path.Combine(_rootDirectory, "identities");
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);
        var identityService = new IdentityService(
            identityRoot,
            new FileSystemIdentityStore(
                identityRoot,
                new Ed25519KeyPairGenerator(),
                new TestPrivateKeyProtector()));
        var snapshotBuilder = new WorkspaceExchangeSnapshotBuilder();
        var stateStore = new FileSystemSyncStateStore();
        var analyzer = new WorkspaceSyncAnalyzer(snapshotBuilder);
        var auditLog = new FileSystemAuditLogService(signedStore);
        var syncService = new FileSystemWorkspaceSyncService(
            snapshotBuilder,
            analyzer,
            new FileSystemSyncManifestStore(signedStore, snapshotBuilder),
            stateStore,
            new WorkspaceExchangeValidator(new Ed25519SignatureService()),
            auditLog);
        return new ProjectWorkspaceCoordinatorService(
            identityService,
            workspaceStore,
            stateStore,
            analyzer,
            syncService,
            new RecentProjectsStore(Path.Combine(_rootDirectory, "recent.json")),
            auditLog,
            new SharedFolderSafetyInspector());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private sealed class TestPrivateKeyProtector : IPrivateKeyProtector
    {
        public string ProviderName => "Test";

        public byte[] Protect(ReadOnlySpan<byte> privateKeyBytes) =>
            privateKeyBytes.ToArray();

        public byte[] Unprotect(ReadOnlySpan<byte> protectedBytes) =>
            protectedBytes.ToArray();
    }

    private sealed class TestLocalGitRepositoryInspector : ILocalGitRepositoryInspector
    {
        public LocalGitRepositoryStatus Inspect(string repositoryPath) =>
            new(
                false,
                repositoryPath,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                [],
                "Not configured.");
    }
}
