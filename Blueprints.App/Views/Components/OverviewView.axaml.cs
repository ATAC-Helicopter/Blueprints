using Avalonia.Controls;
using Blueprints.App.Models;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views.Components;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
    }

    private void SelectWorkItem_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: WorkspaceItemCard item }
            && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectItemNodeCommand.Execute(item);
        }
    }
}
