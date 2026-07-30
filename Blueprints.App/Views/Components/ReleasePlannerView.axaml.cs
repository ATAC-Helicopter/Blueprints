using Avalonia.Controls;
using Blueprints.App.Models;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views.Components;

public partial class ReleasePlannerView : UserControl
{
    public ReleasePlannerView()
    {
        InitializeComponent();
    }

    private void SelectReleaseItem_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WorkspaceItemCard item } &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectItemNodeCommand.Execute(item);
        }
    }
}
