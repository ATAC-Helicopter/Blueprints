using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Runtime.Versioning;
using Avalonia.Markup.Xaml;
using Blueprints.App.Services;
using Blueprints.App.ViewModels;
using Blueprints.App.Views;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Services;

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

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(identityService),
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
}
