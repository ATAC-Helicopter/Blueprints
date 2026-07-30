using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Blueprints.App.Views.Components;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
        BlueprintSurface.HistoryStateChanged += (_, _) => UpdateHistoryButtons();
        UpdateHistoryButtons();
    }

    private void UndoClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.UndoLayout();

    private void RedoClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.RedoLayout();

    private void ZoomInClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ZoomIn();

    private void ZoomOutClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ZoomOut();

    private void FitViewClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.FitView();

    private void AutoArrangeClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.AutoArrange();

    private void SaveLayoutClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.SaveLayout();

    private void UpdateHistoryButtons()
    {
        UndoButton.IsEnabled = BlueprintSurface.CanUndo;
        RedoButton.IsEnabled = BlueprintSurface.CanRedo;
    }
}
