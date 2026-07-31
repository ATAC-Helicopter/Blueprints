using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views.Components;

public partial class IntegrationsView : UserControl
{
    public IntegrationsView()
    {
        InitializeComponent();
    }

    private async void BrowseRepository_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel
            && await PickFolderAsync("Choose a local Git repository") is { } path)
        {
            viewModel.LinkLocalGitRepositoryCommand.Execute(path);
        }
    }

    private async void BrowseCloneDestination_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel
            && await PickFolderAsync("Choose where the repository should be cloned") is { } path)
        {
            viewModel.CloneDestinationParent = path;
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
