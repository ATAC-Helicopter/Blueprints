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
            var workspaceService = CreateWindowsWorkspaceService();
            var session = CreateSession(identityService, workspaceService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(session),
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
    private static LocalWorkspaceService CreateWindowsWorkspaceService()
    {
        ISignedDocumentStore signedDocumentStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        IProjectWorkspaceStore workspaceStore = new FileSystemProjectWorkspaceStore(signedDocumentStore);

        return new LocalWorkspaceService(AppEnvironment.GetWorkspaceRoot(), workspaceStore);
    }

    [SupportedOSPlatform("windows")]
    private static LocalWorkspaceSession CreateSession(
        IIdentityService identityService,
        LocalWorkspaceService workspaceService)
    {
        var identity = identityService.GetOrCreateDefaultIdentity("Local Admin");
        return workspaceService.GetOrCreateDefaultWorkspace(identity);
    }
}
