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

    private async void BrowseJoinInvitation_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickInvitationAsync() is { } path)
        {
            viewModel.JoinInvitationFilePath = path;
        }
    }

    private async void BrowseJoinLocal_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickFolderAsync("Choose a new empty local workspace") is { } path)
        {
            viewModel.JoinLocalWorkspaceRoot = path;
        }
    }

    private async void BrowseJoinShared_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickFolderAsync("Override the invitation's shared exchange folder") is { } path)
        {
            viewModel.JoinSharedWorkspaceRoot = path;
        }
    }

    private async void ExportSetupIdentityInvitation_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider is null)
            {
                return;
            }

            var file = await storageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Export signed identity invitation",
                    SuggestedFileName = "identity.blueprints-identity.json",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("Blueprints identity invitation")
                        {
                            Patterns = ["*.blueprints-identity.json"],
                        },
                    ],
                });
            if (file?.TryGetLocalPath() is { } path)
            {
                viewModel.ExportSetupIdentityInvitationCommand.Execute(path);
            }
        }
    }

    private async void ExportIdentityBackup_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Create encrypted identity backup",
                SuggestedFileName = "identity.blueprints-backup.json",
                FileTypeChoices =
                [
                    new FilePickerFileType("Blueprints encrypted identity backup")
                    {
                        Patterns = ["*.blueprints-backup.json"],
                    },
                ],
            });
        if (file?.TryGetLocalPath() is { } path)
        {
            viewModel.ExportIdentityBackupCommand.Execute(path);
        }
    }

    private async void ImportIdentityBackup_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Restore encrypted identity backup",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Blueprints encrypted identity backup")
                    {
                        Patterns = ["*.blueprints-backup.json"],
                    },
                ],
            });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            viewModel.ImportIdentityBackupCommand.Execute(path);
        }
    }

    private async Task<string?> PickInvitationAsync()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return null;
        }

        var files = await storageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Choose a signed Blueprints project invitation",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Blueprints project invitation")
                    {
                        Patterns = ["*.blueprints-project.json"],
                    },
                ],
            });
        return files.FirstOrDefault()?.TryGetLocalPath();
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
