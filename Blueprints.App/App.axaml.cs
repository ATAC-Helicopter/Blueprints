using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.App.ViewModels;
using Blueprints.App.Views;
using Blueprints.Collaboration.Services;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Services;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Services;

namespace Blueprints.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var identityService = CreateIdentityService();
            var coordinatorService = CreateProjectCoordinator(identityService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    coordinatorService,
                    new IntegrationStatusService()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IIdentityService CreateIdentityService() =>
        new IdentityService(
            AppEnvironment.GetIdentityRoot(),
            new FileSystemIdentityStore(
                AppEnvironment.GetIdentityRoot(),
                new Ed25519KeyPairGenerator(),
                PrivateKeyProtectorFactory.Create()));

    private static LocalWorkspaceService CreateWorkspaceService(IProjectWorkspaceStore workspaceStore) =>
        new(AppEnvironment.GetWorkspaceRoot(), workspaceStore);

    private static ProjectWorkspaceCoordinatorService CreateProjectCoordinator(
        IIdentityService identityService)
    {
        ISignedDocumentStore signedDocumentStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        IProjectWorkspaceStore workspaceStore = new FileSystemProjectWorkspaceStore(signedDocumentStore);
        var snapshotBuilder = new WorkspaceExchangeSnapshotBuilder();
        var syncStateStore = new FileSystemSyncStateStore();
        var syncAnalyzer = new WorkspaceSyncAnalyzer(snapshotBuilder);
        var auditLogService = new FileSystemAuditLogService(signedDocumentStore);
        var workspaceSyncService = new FileSystemWorkspaceSyncService(
            snapshotBuilder,
            syncAnalyzer,
            new FileSystemSyncManifestStore(signedDocumentStore, snapshotBuilder),
            syncStateStore,
            new WorkspaceExchangeValidator(new Ed25519SignatureService()),
            auditLogService);

        return new ProjectWorkspaceCoordinatorService(
            identityService,
            workspaceStore,
            syncStateStore,
            syncAnalyzer,
            workspaceSyncService,
            new RecentProjectsStore(AppEnvironment.GetRecentProjectsPath()),
            auditLogService,
            new SharedFolderSafetyInspector());
    }
}
