using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, HandleGlobalKeyDown, RoutingStrategies.Tunnel);
    }

    private void HandleGlobalKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || !viewModel.HasActiveSession
            || (eventArgs.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0)
        {
            return;
        }

        var command = eventArgs.Key switch
        {
            Key.D1 or Key.NumPad1 => viewModel.NavigateToOverviewCommand,
            Key.D2 or Key.NumPad2 => viewModel.NavigateToReleasesCommand,
            Key.D3 or Key.NumPad3 => viewModel.NavigateToIntegrationsCommand,
            Key.D4 or Key.NumPad4 => viewModel.NavigateToTeamCommand,
            Key.D5 or Key.NumPad5 => viewModel.NavigateToSyncCommand,
            Key.D6 or Key.NumPad6 => viewModel.NavigateToTrustCommand,
            _ => null,
        };
        if (command is null)
        {
            return;
        }

        command.Execute(null);
        eventArgs.Handled = true;
    }
}
