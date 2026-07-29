using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views.Components;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
    }

    private async void BrowseCreateLocal_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickFolderAsync("Choose the editable local workspace") is { } path)
        {
            viewModel.CreateLocalWorkspaceRoot = path;
        }
    }

    private async void BrowseCreateShared_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickFolderAsync("Choose the shared exchange folder") is { } path)
        {
            viewModel.CreateSharedWorkspaceRoot = path;
        }
    }

    private async void BrowseOpenLocal_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickFolderAsync("Choose the existing local workspace") is { } path)
        {
            viewModel.OpenLocalWorkspaceRoot = path;
        }
    }

    private async void BrowseOpenShared_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickFolderAsync("Choose its shared exchange folder") is { } path)
        {
            viewModel.OpenSharedWorkspaceRoot = path;
        }
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return null;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
