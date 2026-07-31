using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views.Components;

public partial class TeamView : UserControl
{
    public TeamView()
    {
        InitializeComponent();
    }

    private async void ExportIdentityInvitation_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickSavePathAsync(
                "Export signed identity invitation",
                "identity.blueprints-identity.json",
                "Blueprints identity invitation",
                "*.blueprints-identity.json") is { } path)
        {
            viewModel.ExportIdentityInvitationCommand.Execute(path);
        }
    }

    private async void ImportIdentityInvitation_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickOpenPathAsync(
                "Import signed identity invitation",
                "Blueprints identity invitation",
                "*.blueprints-identity.json") is { } path)
        {
            viewModel.ImportIdentityInvitationCommand.Execute(path);
        }
    }

    private async void ExportIdentityBackup_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickSavePathAsync(
                "Create encrypted identity backup",
                "identity.blueprints-backup.json",
                "Blueprints encrypted identity backup",
                "*.blueprints-backup.json") is { } path)
        {
            viewModel.ExportIdentityBackupCommand.Execute(path);
        }
    }

    private async void ExportProjectInvitation_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            await PickSavePathAsync(
                "Export signed project invitation",
                "project.blueprints-project.json",
                "Blueprints project invitation",
                "*.blueprints-project.json") is { } path)
        {
            viewModel.ExportProjectInvitationCommand.Execute(path);
        }
    }

    private async Task<string?> PickOpenPathAsync(
        string title,
        string fileTypeName,
        string pattern)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return null;
        }

        var files = await storageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(fileTypeName) { Patterns = [pattern] },
                ],
            });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> PickSavePathAsync(
        string title,
        string suggestedFileName,
        string fileTypeName,
        string pattern)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
        {
            return null;
        }

        var file = await storageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedFileName,
                FileTypeChoices =
                [
                    new FilePickerFileType(fileTypeName) { Patterns = [pattern] },
                ],
            });
        return file?.TryGetLocalPath();
    }
}
