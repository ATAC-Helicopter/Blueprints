using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Blueprints.App.Views.Components;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
    }

    private void ZoomInClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ZoomIn();

    private void ZoomOutClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ZoomOut();

    private void ResetViewClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ResetView();
}
