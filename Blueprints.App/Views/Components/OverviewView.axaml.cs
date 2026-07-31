using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Blueprints.App.Models;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views.Components;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
        BlueprintSurface.HistoryStateChanged += (_, _) => UpdateHistoryButtons();
        BlueprintSurface.SelectionStateChanged += (_, _) => UpdateSelectionSummary();
        BlueprintSurface.ZoomChanged += (_, _) => UpdateZoomSummary();
        BlueprintSurface.ViewModeChanged += (_, _) => UpdateViewModeButtons();
        BlueprintSurface.ConnectModeChanged += (_, _) => UpdateConnectButton();
        BlueprintSurface.SearchRequested += (_, _) => SearchBox.Focus();
        DataContextChanged += HandleDataContextChanged;
        UpdateHistoryButtons();
        UpdateSelectionSummary();
        UpdateZoomSummary();
        UpdateViewModeButtons();
        UpdateConnectButton();
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

    private void ZoomSelectionClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ZoomToSelection();

    private void FocusClick(object? sender, RoutedEventArgs eventArgs)
    {
        BlueprintSurface.ToggleFocusMode();
        FocusButton.Classes.Set("active", BlueprintSurface.IsFocusMode);
        FocusButton.Content = BlueprintSurface.IsFocusMode ? "Exit focus" : "Focus";
    }

    private void PlanModeClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.SetViewMode(CanvasViewMode.Plan);

    private void DependenciesModeClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.SetViewMode(CanvasViewMode.Dependencies);

    private void ReleaseNotesModeClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.SetViewMode(CanvasViewMode.ReleaseNotes);

    private void ConnectClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ToggleConnectMode();

    private void ToggleMiniMapClick(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.ToggleMiniMap();

    private void SearchTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (sender is TextBox textBox)
        {
            BlueprintSurface.SetSearch(textBox.Text);
        }
    }

    private void LifecycleFilterChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            BlueprintSurface.SetLifecycleFilter(item.Tag?.ToString());
        }
    }

    private void VersionFilterChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            BlueprintSurface.SetVersionFilter(item.Tag?.ToString());
        }
    }

    private void ItemTypeFilterChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            BlueprintSurface.SetItemTypeFilter(item.Tag?.ToString());
        }
    }

    private void CategoryFilterChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            BlueprintSurface.SetCategoryFilter(item.Tag?.ToString());
        }
    }

    private void WarningsOnlyChanged(object? sender, RoutedEventArgs eventArgs) =>
        BlueprintSurface.SetWarningsOnly(WarningsOnlyFilter.IsChecked == true);

    private void UpdateHistoryButtons()
    {
        UndoButton.IsEnabled = BlueprintSurface.CanUndo;
        RedoButton.IsEnabled = BlueprintSurface.CanRedo;
    }

    private void UpdateSelectionSummary() =>
        CanvasSelectionSummary.Text = BlueprintSurface.SelectionSummary;

    private void UpdateZoomSummary() =>
        ZoomLevelText.Text = BlueprintSurface.ZoomSummary;

    private void UpdateViewModeButtons()
    {
        PlanModeButton.Classes.Set("active", BlueprintSurface.ViewMode == CanvasViewMode.Plan);
        DependenciesModeButton.Classes.Set("active", BlueprintSurface.ViewMode == CanvasViewMode.Dependencies);
        ReleaseNotesModeButton.Classes.Set("active", BlueprintSurface.ViewMode == CanvasViewMode.ReleaseNotes);
    }

    private void UpdateConnectButton()
    {
        ConnectButton.Classes.Set("active", BlueprintSurface.IsConnectMode);
        ConnectButton.Content = BlueprintSurface.IsConnectMode ? "Connecting…" : "Connect";
    }

    private void RefreshVersionFilter()
    {
        while (VersionFilter.Items.Count > 1)
        {
            VersionFilter.Items.RemoveAt(VersionFilter.Items.Count - 1);
        }
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }
        foreach (var version in viewModel.Versions)
        {
            VersionFilter.Items.Add(new ComboBoxItem
            {
                Content = version.Name,
                Tag = version.Name,
            });
        }
        PopulateFilter(ItemTypeFilter, viewModel.AvailableItemTypes);
        PopulateFilter(CategoryFilter, viewModel.AvailableCategories);
    }

    private static void PopulateFilter(ComboBox comboBox, IEnumerable<string> values)
    {
        while (comboBox.Items.Count > 1)
        {
            comboBox.Items.RemoveAt(comboBox.Items.Count - 1);
        }
        foreach (var value in values)
        {
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = value,
                Tag = value,
            });
        }
    }

    private void HandleDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Versions.CollectionChanged -= HandleVersionsChanged;
            viewModel.Versions.CollectionChanged += HandleVersionsChanged;
        }
        RefreshVersionFilter();
    }

    private void HandleVersionsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) =>
        RefreshVersionFilter();
}
