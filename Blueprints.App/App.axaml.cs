using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Runtime.Versioning;
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
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Blueprints v1.0 currently supports Windows only.");
            }

            var identityService = CreateWindowsIdentityService();
            var coordinatorService = CreateWindowsProjectCoordinator(identityService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(coordinatorService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    [SupportedOSPlatform("windows")]
    private static IIdentityService CreateWindowsIdentityService() =>
        new IdentityService(
            AppEnvironment.GetIdentityRoot(),
            new FileSystemIdentityStore(
                AppEnvironment.GetIdentityRoot(),
                new Ed25519KeyPairGenerator(),
                new DpapiPrivateKeyProtector()));

    [SupportedOSPlatform("windows")]
    private static LocalWorkspaceService CreateWindowsWorkspaceService(IProjectWorkspaceStore workspaceStore) =>
        new(AppEnvironment.GetWorkspaceRoot(), workspaceStore);

    [SupportedOSPlatform("windows")]
    private static ProjectWorkspaceCoordinatorService CreateWindowsProjectCoordinator(
        IIdentityService identityService)
    {
        ISignedDocumentStore signedDocumentStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        IProjectWorkspaceStore workspaceStore = new FileSystemProjectWorkspaceStore(signedDocumentStore);
        var snapshotBuilder = new WorkspaceExchangeSnapshotBuilder();
        var workspaceService = CreateWindowsWorkspaceService(workspaceStore);

        return new ProjectWorkspaceCoordinatorService(
            identityService,
            workspaceStore,
            new FileSystemSyncStateStore(),
            new WorkspaceSyncAnalyzer(snapshotBuilder),
            new RecentProjectsStore(AppEnvironment.GetRecentProjectsPath()));
    }
}
