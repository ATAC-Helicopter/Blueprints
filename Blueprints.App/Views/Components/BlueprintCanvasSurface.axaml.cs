using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Blueprints.App.Models;
using Blueprints.App.Services;
using Blueprints.App.ViewModels;
using Blueprints.Core.Enums;
using Blueprints.Core.Models;

namespace Blueprints.App.Views.Components;

public partial class BlueprintCanvasSurface : UserControl
{
    private const double DependencyNodeWidth = 250;
    private const double DependencyNodeHeight = 112;
    private const double FrameWidth = 1120;
    private const double FrameHeaderHeight = 86;
    private const double CardHeight = 132;
    private readonly Dictionary<(string Type, Guid Id), Control> _nodes = [];
    private readonly Dictionary<(string Type, Guid Id), Rect> _nodeBounds = [];
    private readonly Dictionary<Guid, Point> _positions = [];
    private readonly Dictionary<Guid, Size> _frameSizes = [];
    private readonly HashSet<Guid> _selectedNodeIds = [];
    private readonly HashSet<Guid> _collapsedVersionIds = [];
    private readonly CanvasLayoutHistory _layoutHistory = new();
    private readonly DispatcherTimer _viewportSaveTimer;
    private MainWindowViewModel? _viewModel;
    private CanvasViewMode _viewMode = CanvasViewMode.Plan;
    private string _searchText = string.Empty;
    private string _lifecycleFilter = "All";
    private string _versionFilter = "All";
    private string _itemTypeFilter = "All";
    private string _categoryFilter = "All";
    private bool _warningsOnly;
    private bool _focusMode;
    private string _preFocusSearch = string.Empty;
    private string _preFocusVersion = "All";
    private bool _minimapVisible = true;
    private bool _connectMode;
    private RelationshipEndpoint? _connectionSource;
    private Control? _draggedNode;
    private (string Type, Guid Id)? _draggedIdentity;
    private Point _dragStartPointer;
    private Point _dragStartPosition;
    private IReadOnlyList<CanvasNodeLayoutEdit>? _dragStartLayout;
    private WorkspaceVersionCard? _draggedItemVersion;
    private WorkspaceItemCard? _draggedItem;
    private double _zoom = 1;
    private bool _isRestoring;
    private bool _isPersisting;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;
    private bool _isBoxSelecting;
    private Point _boxStart;
    private Rectangle? _boxVisual;

    public event EventHandler? HistoryStateChanged;
    public event EventHandler? SelectionStateChanged;
    public event EventHandler? ZoomChanged;
    public event EventHandler? ViewModeChanged;
    public event EventHandler? ConnectModeChanged;

    public bool CanUndo => _viewModel?.CanMutateWorkspace == true && _layoutHistory.CanUndo;
    public bool CanRedo => _viewModel?.CanMutateWorkspace == true && _layoutHistory.CanRedo;
    public bool IsConnectMode => _connectMode;
    public bool IsFocusMode => _focusMode;
    public CanvasViewMode ViewMode => _viewMode;
    public string ZoomSummary => $"{_zoom:P0}";
    public string SelectionSummary => _selectedNodeIds.Count switch
    {
        0 when _viewModel?.SelectedRelationship is not null => "1 relationship selected",
        0 => "Nothing selected",
        1 => "1 entity selected",
        _ => $"{_selectedNodeIds.Count} entities selected",
    };

    public BlueprintCanvasSurface()
    {
        InitializeComponent();
        _viewportSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _viewportSaveTimer.Tick += (_, _) =>
        {
            _viewportSaveTimer.Stop();
            PersistViewState();
        };
        Viewport.ScrollChanged += (_, _) =>
        {
            RenderMiniMap();
            if (!_isRestoring)
            {
                _viewportSaveTimer.Stop();
                _viewportSaveTimer.Start();
            }
        };
        Surface.PointerPressed += HandleSurfacePointerPressed;
        Surface.PointerMoved += HandleSurfacePointerMoved;
        Surface.PointerReleased += HandleSurfacePointerReleased;
        Surface.PointerWheelChanged += HandlePointerWheel;
        MiniMapSurface.PointerPressed += HandleMiniMapPointerPressed;
        KeyDown += HandleKeyDown;
        DataContextChanged += HandleDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachViewModel();
    }

    public void ZoomIn() => SetZoom(_zoom + 0.1, true);
    public void ZoomOut() => SetZoom(_zoom - 0.1, true);

    public void FitView()
    {
        var content = GetContentBounds();
        if (content.Width <= 0 || content.Height <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(320, Viewport.Bounds.Width - 64);
        var availableHeight = Math.Max(240, Viewport.Bounds.Height - 64);
        var fit = Math.Clamp(Math.Min(availableWidth / content.Width, availableHeight / content.Height), .25, 1.25);
        SetZoom(fit);
        Viewport.Offset = new Vector(
            Math.Max(0, content.X * fit - 24),
            Math.Max(0, content.Y * fit - 24));
        PersistViewState();
    }

    public void ZoomToSelection()
    {
        var selected = _nodeBounds
            .Where(pair => _selectedNodeIds.Contains(pair.Key.Id))
            .Select(static pair => pair.Value)
            .ToArray();
        if (selected.Length == 0)
        {
            FitView();
            return;
        }

        var bounds = Union(selected);
        var zoom = Math.Clamp(
            Math.Min(
                Math.Max(320, Viewport.Bounds.Width - 80) / bounds.Width,
                Math.Max(240, Viewport.Bounds.Height - 80) / bounds.Height),
            .25,
            2.5);
        SetZoom(zoom);
        Viewport.Offset = new Vector(
            Math.Max(0, bounds.Center.X * zoom - Viewport.Viewport.Width / 2),
            Math.Max(0, bounds.Center.Y * zoom - Viewport.Viewport.Height / 2));
        PersistViewState();
    }

    public void SetViewMode(CanvasViewMode mode)
    {
        if (mode == CanvasViewMode.Timeline)
        {
            return;
        }

        _viewMode = mode;
        RenderGraph();
        PersistViewState();
        ViewModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSearch(string? value)
    {
        _focusMode = false;
        _searchText = (value ?? string.Empty).Trim();
        RenderGraph();
        PersistViewState();
    }

    public void ToggleFocusMode()
    {
        if (_viewModel is null)
        {
            return;
        }
        if (_focusMode)
        {
            _focusMode = false;
            _searchText = _preFocusSearch;
            _versionFilter = _preFocusVersion;
        }
        else if (_viewModel.SelectedItem is { } item)
        {
            _preFocusSearch = _searchText;
            _preFocusVersion = _versionFilter;
            _focusMode = true;
            _searchText = item.ItemKey;
            _versionFilter = "All";
        }
        else if (_viewModel.SelectedVersion is { } version)
        {
            _preFocusSearch = _searchText;
            _preFocusVersion = _versionFilter;
            _focusMode = true;
            _searchText = string.Empty;
            _versionFilter = version.Name;
        }
        RenderGraph();
    }

    public void SetLifecycleFilter(string? value)
    {
        _lifecycleFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
        RenderGraph();
        PersistViewState();
    }

    public void SetVersionFilter(string? value)
    {
        _versionFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
        RenderGraph();
        PersistViewState();
    }

    public void SetItemTypeFilter(string? value)
    {
        _itemTypeFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
        RenderGraph();
        PersistViewState();
    }

    public void SetCategoryFilter(string? value)
    {
        _categoryFilter = string.IsNullOrWhiteSpace(value) ? "All" : value;
        RenderGraph();
        PersistViewState();
    }

    public void SetWarningsOnly(bool value)
    {
        _warningsOnly = value;
        RenderGraph();
        PersistViewState();
    }

    public void ToggleConnectMode()
    {
        _connectMode = !_connectMode && _viewModel?.CanMutateWorkspace == true;
        _connectionSource = null;
        RenderGraph();
        ConnectModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleMiniMap()
    {
        _minimapVisible = !_minimapVisible;
        MiniMapPanel.IsVisible = _minimapVisible;
        PersistViewState();
    }

    public void AutoArrange()
    {
        if (_viewModel?.CanMutateWorkspace != true)
        {
            return;
        }

        var previous = CaptureLayout();
        if (_viewMode == CanvasViewMode.Dependencies)
        {
            var dependencyIndex = 0;
            if (_viewModel.CurrentProject.ProjectId != Guid.Empty)
            {
                _positions[_viewModel.CurrentProject.ProjectId] =
                    DependencyPosition(dependencyIndex++);
            }
            foreach (var version in _viewModel.Versions)
            {
                _positions[version.VersionId] = DependencyPosition(dependencyIndex++);
                foreach (var item in version.Items)
                {
                    _positions[item.ItemId] = DependencyPosition(dependencyIndex++);
                }
            }
        }
        else
        {
            foreach (var version in _viewModel.Versions.Select((value, index) => (value, index)))
            {
                _positions[version.value.VersionId] = new Point(
                    80,
                    80 + version.index * 720);
            }
        }

        RenderGraph();
        _layoutHistory.Record(previous, CaptureLayout());
        PersistLayout();
        HistoryStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveLayout() => PersistLayout();

    public void UndoLayout()
    {
        if (_viewModel?.CanMutateWorkspace != true ||
            !_layoutHistory.TryUndo(CaptureLayout(), out var previous))
        {
            return;
        }

        ApplyLayout(previous);
        PersistLayout();
    }

    public void RedoLayout()
    {
        if (_viewModel?.CanMutateWorkspace != true ||
            !_layoutHistory.TryRedo(CaptureLayout(), out var next))
        {
            return;
        }

        ApplyLayout(next);
        PersistLayout();
    }

    private void HandleDataContextChanged(object? sender, EventArgs eventArgs)
    {
        DetachViewModel();
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
            _viewModel.Versions.CollectionChanged += HandleVersionsChanged;
        }

        RestoreLayout();
        RestoreViewState();
        RenderGraph();
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        _viewModel.Versions.CollectionChanged -= HandleVersionsChanged;
        _viewModel = null;
    }

    private void HandleVersionsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (!_isPersisting)
        {
            _layoutHistory.Clear();
        }
        RenderGraph();
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainWindowViewModel.CanvasLayout))
        {
            RestoreLayout();
            RenderGraph();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.RelationshipDocument)
                 or nameof(MainWindowViewModel.CanMutateWorkspace))
        {
            RenderGraph();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.SelectedItem)
                 or nameof(MainWindowViewModel.SelectedVersion))
        {
            UpdateSelectionVisuals();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.CanvasViewState))
        {
            RestoreViewState();
            RenderGraph();
        }
    }

    private void RenderGraph()
    {
        Surface.Children.Clear();
        _nodes.Clear();
        _nodeBounds.Clear();
        if (_viewModel is null)
        {
            return;
        }

        var projection = CanvasBoardProjectionService.Build(
            _viewMode,
            _viewModel.Versions,
            _viewModel.RelationshipDocument,
            new CanvasBoardFilter(
                _searchText,
                _versionFilter,
                _lifecycleFilter,
                _itemTypeFilter,
                _categoryFilter,
                _warningsOnly),
            _viewModel.CurrentProject);

        if (_viewMode == CanvasViewMode.Plan)
        {
            RenderPlan(projection);
        }
        else if (_viewMode == CanvasViewMode.Dependencies)
        {
            RenderDependencies(projection);
        }
        else
        {
            RenderReleaseNotes(projection);
        }

        DrawGrid();
        RenderRelationships(projection.Relationships);
        RenderEmptyState(projection);
        RenderMiniMap();
        SetZoom(_zoom);
        SelectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderPlan(CanvasBoardProjection projection)
    {
        var y = 70d;
        foreach (var frame in projection.Frames)
        {
            var savedPosition = GetPosition(frame.Version.VersionId, new Point(70, y));
            var position = new Point(savedPosition.X, Math.Max(savedPosition.Y, y));
            _positions[frame.Version.VersionId] = position;
            var collapsed = _collapsedVersionIds.Contains(frame.Version.VersionId);
            var maximumItems = frame.Columns.Max(static column => column.Items.Count);
            var naturalHeight = collapsed ? FrameHeaderHeight : Math.Max(360, 178 + maximumItems * (CardHeight + 14));
            var configured = _frameSizes.GetValueOrDefault(
                frame.Version.VersionId,
                new Size(FrameWidth, naturalHeight));
            var size = new Size(
                Math.Max(FrameWidth, configured.Width),
                collapsed ? FrameHeaderHeight : Math.Max(naturalHeight, configured.Height));
            var root = CreateVersionFrame(frame, size, collapsed, position);
            Place(root, position);
            Surface.Children.Add(root);
            _nodes[("version", frame.Version.VersionId)] = root;
            _nodeBounds[("version", frame.Version.VersionId)] = new Rect(position, size);
            y = Math.Max(y, position.Y + size.Height + 56);
        }

        SizeSurface();
    }

    private Control CreateVersionFrame(
        CanvasVersionFrame frame,
        Size size,
        bool collapsed,
        Point absolutePosition)
    {
        var root = new Border
        {
            Width = size.Width,
            Height = size.Height,
            Background = ResourceBrush("CanvasFrameBackgroundBrush", "#F9F9FC"),
            BorderBrush = IsSelected(frame.Version.VersionId)
                ? ResourceBrush("CanvasSelectionBrush", "#6254D9")
                : ResourceBrush("CanvasFrameBorderBrush", "#D6D8E2"),
            BorderThickness = new Thickness(IsSelected(frame.Version.VersionId) ? 2.5 : 1),
            CornerRadius = new CornerRadius(8),
            Tag = new NodeTag("version", frame.Version.VersionId),
        };
        AutomationProperties.SetName(root, $"Version {frame.Version.Name}, {frame.CompletionPercentage} percent complete");
        AutomationProperties.SetHelpText(root, $"{frame.ReadinessSummary}. Select to open version details.");

        var layout = new Grid { RowDefinitions = new RowDefinitions("86,*") };
        var header = new Border
        {
            Padding = new Thickness(18, 13),
            Background = ResourceBrush("CanvasFrameHeaderBrush", "#F1F0F8"),
            BorderBrush = ResourceBrush("CanvasFrameBorderBrush", "#D6D8E2"),
            BorderThickness = new Thickness(0, 0, 0, collapsed ? 0 : 1),
        };
        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var heading = new StackPanel { Spacing = 5 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        titleRow.Children.Add(Text(frame.Version.Name, 21, FontWeight.Bold));
        titleRow.Children.Add(Badge(frame.Version.Status.ToString(), "#E6E3FA", "#5548C7"));
        heading.Children.Add(titleRow);
        heading.Children.Add(Text(
            $"{frame.Version.CompletedItemCount}/{frame.Version.ItemCount} complete · {frame.CompletionPercentage}% · {frame.ReadinessSummary}",
            11,
            FontWeight.Medium,
            ResourceBrush("CanvasMutedBrush", "#66697A")));
        headerGrid.Children.Add(heading);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        Grid.SetColumn(actions, 1);
        var warning = Badge(
            frame.BlockerCount > 0 ? $"⚠ {frame.BlockerCount} blocked" : $"{frame.WarningCount} open",
            frame.BlockerCount > 0 ? "#FDE8E5" : "#ECEEF4",
            frame.BlockerCount > 0 ? "#A93A31" : "#606477");
        actions.Children.Add(warning);
        var inspect = SmallButton("Details", "Open version inspector");
        inspect.Click += (_, _) => SelectVersion(frame.Version);
        actions.Children.Add(inspect);
        if (_connectMode)
        {
            var connect = SmallButton("●", $"Use {frame.Version.Name} as a relationship endpoint");
            connect.Click += (_, eventArgs) =>
            {
                HandleConnectionEndpoint(new RelationshipEndpoint("version", frame.Version.VersionId));
                eventArgs.Handled = true;
            };
            actions.Children.Add(connect);
        }
        var collapse = SmallButton(collapsed ? "Expand" : "Collapse", collapsed ? "Expand version frame" : "Collapse version frame");
        collapse.Click += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            if (!_collapsedVersionIds.Add(frame.Version.VersionId))
            {
                _collapsedVersionIds.Remove(frame.Version.VersionId);
            }
            RenderGraph();
            PersistViewState();
        };
        actions.Children.Add(collapse);
        headerGrid.Children.Add(actions);
        header.Child = headerGrid;
        layout.Children.Add(header);

        if (!collapsed)
        {
            var columns = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
                Margin = new Thickness(14),
            };
            Grid.SetRow(columns, 1);
            for (var index = 0; index < frame.Columns.Count; index++)
            {
                var column = frame.Columns[index];
                var columnControl = CreateLifecycleColumn(
                    frame.Version,
                    column,
                    absolutePosition,
                    size.Width,
                    index);
                Grid.SetColumn(columnControl, index);
                columns.Children.Add(columnControl);
            }
            layout.Children.Add(columns);

            var resize = new Border
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = new Cursor(StandardCursorType.BottomRightCorner),
                Background = ResourceBrush("CanvasResizeBrush", "#CBC7E8"),
            };
            AddResizeHandlers(resize, root, frame.Version.VersionId);
            layout.Children.Add(resize);
        }

        root.Child = layout;
        header.PointerPressed += (_, eventArgs) =>
        {
            SelectVersion(frame.Version, eventArgs.KeyModifiers);
            BeginNodeDrag(root, ("version", frame.Version.VersionId), eventArgs);
        };
        AddNodeDragContinuation(root);
        return root;
    }

    private Control CreateLifecycleColumn(
        WorkspaceVersionCard version,
        CanvasLifecycleColumn column,
        Point framePosition,
        double frameWidth,
        int columnIndex)
    {
        var panel = new StackPanel { Spacing = 11 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(5, 2, 5, 0) };
        header.Children.Add(Text(column.DisplayName.ToUpperInvariant(), 10, FontWeight.Bold, LifecycleBrush(column.State)));
        var count = Text(column.Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), 11, FontWeight.Bold);
        Grid.SetColumn(count, 1);
        header.Children.Add(count);
        panel.Children.Add(header);

        foreach (var item in column.Items)
        {
            var card = CreateItemCard(version, item);
            panel.Children.Add(card);
            var localX = 14 + columnIndex * ((frameWidth - 28) / 4) + 7;
            var localY = FrameHeaderHeight + 62 + (panel.Children.Count - 2) * (CardHeight + 11);
            _nodes[("item", item.ItemId)] = card;
            _nodeBounds[("item", item.ItemId)] = new Rect(
                framePosition.X + localX,
                framePosition.Y + localY,
                (frameWidth - 28) / 4 - 22,
                CardHeight);
        }

        return new Border
        {
            Margin = new Thickness(5, 0),
            Padding = new Thickness(7),
            Background = ResourceBrush("CanvasColumnBackgroundBrush", "#F2F3F7"),
            BorderBrush = ResourceBrush("CanvasColumnBorderBrush", "#E1E2E9"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = panel,
        };
    }

    private Control CreateItemCard(WorkspaceVersionCard version, WorkspaceItemCard item)
    {
        var selected = IsSelected(item.ItemId) || _viewModel?.SelectedItem?.ItemId == item.ItemId;
        var blocked = IsBlocked(item.ItemId);
        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 7 };
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(_connectMode ? "*,Auto,Auto" : "*,Auto"),
            ColumnSpacing = 5,
        };
        heading.Children.Add(Text(item.ItemKey, 11, FontWeight.Bold, ResourceBrush("CanvasAccentBrush", "#6254D9")));
        var state = Badge(
            CanvasBoardProjectionService.Format(item.WorkflowState),
            LifecycleBackground(item.WorkflowState),
            LifecycleColor(item.WorkflowState));
        Grid.SetColumn(state, 1);
        heading.Children.Add(state);
        if (_connectMode)
        {
            var handle = Text("●", 15, FontWeight.Bold, ResourceBrush("CanvasAccentBrush", "#6254D9"));
            ToolTip.SetTip(handle, "Relationship connection handle");
            AutomationProperties.SetName(handle, $"Connect from {item.ItemKey}");
            Grid.SetColumn(handle, 2);
            heading.Children.Add(handle);
        }
        content.Children.Add(heading);

        var title = Text(item.Title, 14, FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.Wrap;
        title.MaxLines = 3;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(title, 1);
        content.Children.Add(title);

        var metadata = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        Grid.SetRow(metadata, 2);
        metadata.Children.Add(Badge(item.ItemTypeId, "#ECEEF3", "#55596B"));
        metadata.Children.Add(Badge(item.CategoryId, "#EEEAF9", "#6254A8"));
        if (item.SourceReference is not null)
        {
            metadata.Children.Add(Badge("source", "#E7F2F5", "#276A75"));
        }
        if (blocked)
        {
            metadata.Children.Add(Badge("⚠ blocked", "#FDE8E5", "#A93A31"));
        }
        content.Children.Add(metadata);

        var shell = new Border
        {
            Height = CardHeight,
            Padding = new Thickness(12, 10),
            Background = ResourceBrush("CanvasCardBackgroundBrush", "#FFFFFF"),
            BorderBrush = selected
                ? ResourceBrush("CanvasSelectionBrush", "#6254D9")
                : blocked
                    ? ResourceBrush("CanvasWarningBrush", "#C64D42")
                    : ResourceBrush("CanvasCardBorderBrush", "#D8DAE4"),
            BorderThickness = new Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = content,
            Tag = new NodeTag("item", item.ItemId),
        };
        AutomationProperties.SetName(shell, $"{item.ItemKey}, {item.Title}");
        AutomationProperties.SetHelpText(
            shell,
            $"{CanvasBoardProjectionService.Format(item.WorkflowState)}, {item.ItemTypeId}, changelog category {item.CategoryId}.");
        shell.PointerPressed += (_, eventArgs) =>
        {
            if (_connectMode)
            {
                HandleConnectionEndpoint(new RelationshipEndpoint("item", item.ItemId));
                eventArgs.Handled = true;
                return;
            }
            SelectItem(item, eventArgs.KeyModifiers);
            if (eventArgs.GetCurrentPoint(shell).Properties.IsLeftButtonPressed &&
                _viewModel?.CanEditItems == true)
            {
                _draggedItemVersion = version;
                _draggedItem = item;
                eventArgs.Pointer.Capture(shell);
            }
            eventArgs.Handled = true;
        };
        shell.PointerReleased += (_, eventArgs) =>
        {
            if (_draggedItem?.ItemId == item.ItemId && _draggedItemVersion is not null)
            {
                var point = eventArgs.GetPosition(Surface);
                var frameX = _positions.GetValueOrDefault(version.VersionId).X;
                var width = _frameSizes.GetValueOrDefault(version.VersionId, new Size(FrameWidth, 0)).Width;
                var targetIndex = Math.Clamp(
                    (int)((point.X - frameX - 14) / Math.Max(1, width - 28) * 4),
                    0,
                    3);
                var targetState = (WorkItemLifecycle)targetIndex;
                if (targetState != item.WorkflowState)
                {
                    _viewModel?.ChangeItemLifecycleCommand.Execute(
                        new WorkItemLifecycleChangeRequest(version.VersionId, item.ItemId, targetState));
                }
            }
            _draggedItem = null;
            _draggedItemVersion = null;
            eventArgs.Pointer.Capture(null);
            RenderGraph();
        };
        return shell;
    }

    private void RenderDependencies(CanvasBoardProjection projection)
    {
        foreach (var node in projection.DependencyNodes.Select((value, index) => (value, index)))
        {
            var fallback = DependencyPosition(node.index);
            var point = GetPosition(node.value.EntityId, fallback);
            var control = CreateDependencyNode(node.value);
            Place(control, point);
            Surface.Children.Add(control);
            _nodes[(node.value.NodeType, node.value.EntityId)] = control;
            _nodeBounds[(node.value.NodeType, node.value.EntityId)] =
                new Rect(point, new Size(DependencyNodeWidth, DependencyNodeHeight));
            AddFreeNodeDrag(control, (node.value.NodeType, node.value.EntityId));
        }
        SizeSurface();
    }

    private Control CreateDependencyNode(CanvasDependencyNode node)
    {
        var selected = IsSelected(node.EntityId);
        var content = new StackPanel { Spacing = 7 };
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(_connectMode ? "*,Auto,Auto" : "*,Auto"),
            ColumnSpacing = 5,
        };
        heading.Children.Add(Text(node.Key, 10, FontWeight.Bold, ResourceBrush("CanvasAccentBrush", "#6254D9")));
        if (node.WorkflowState is WorkItemLifecycle state)
        {
            var badge = Badge(CanvasBoardProjectionService.Format(state), LifecycleBackground(state), LifecycleColor(state));
            Grid.SetColumn(badge, 1);
            heading.Children.Add(badge);
        }
        if (_connectMode)
        {
            var handle = Text("●", 15, FontWeight.Bold, ResourceBrush("CanvasAccentBrush", "#6254D9"));
            Grid.SetColumn(handle, 2);
            heading.Children.Add(handle);
        }
        content.Children.Add(heading);
        var title = Text(node.Title, 15, FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.Wrap;
        title.MaxLines = 2;
        content.Children.Add(title);
        content.Children.Add(Text(node.Subtitle, 10, FontWeight.Normal, ResourceBrush("CanvasMutedBrush", "#66697A")));
        var shell = new Border
        {
            Width = DependencyNodeWidth,
            Height = DependencyNodeHeight,
            Padding = new Thickness(13, 11),
            Background = ResourceBrush("CanvasCardBackgroundBrush", "#FFFFFF"),
            BorderBrush = selected
                ? ResourceBrush("CanvasSelectionBrush", "#6254D9")
                : ResourceBrush("CanvasCardBorderBrush", "#D8DAE4"),
            BorderThickness = new Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = content,
            Tag = new NodeTag(node.NodeType, node.EntityId),
        };
        shell.PointerPressed += (_, eventArgs) =>
        {
            if (_connectMode)
            {
                HandleConnectionEndpoint(new RelationshipEndpoint(node.NodeType, node.EntityId));
                eventArgs.Handled = true;
                return;
            }
            SelectProjectedNode(node, eventArgs.KeyModifiers);
        };
        AutomationProperties.SetName(shell, $"{node.Key}, {node.Title}");
        return shell;
    }

    private void RenderReleaseNotes(CanvasBoardProjection projection)
    {
        var categories = projection.Frames
            .SelectMany(static frame => frame.Columns)
            .SelectMany(static column => column.Items)
            .GroupBy(static item => item.CategoryId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var x = 70d;
        foreach (var category in categories)
        {
            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(Text(category.Key.ToUpperInvariant(), 11, FontWeight.Bold, ResourceBrush("CanvasAccentBrush", "#6254D9")));
            foreach (var item in category)
            {
                panel.Children.Add(CreateReleaseNoteCard(item));
            }
            var root = new Border
            {
                Width = 330,
                Padding = new Thickness(14),
                Background = ResourceBrush("CanvasColumnBackgroundBrush", "#F2F3F7"),
                BorderBrush = ResourceBrush("CanvasColumnBorderBrush", "#E1E2E9"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = panel,
            };
            Place(root, new Point(x, 70));
            Surface.Children.Add(root);
            x += 360;
        }
        SizeSurface();
    }

    private Control CreateReleaseNoteCard(WorkspaceItemCard item)
    {
        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(Text($"{item.ItemKey} · {item.Title}", 13, FontWeight.SemiBold));
        content.Children.Add(Text(
            $"{item.ItemTypeId} · {CanvasBoardProjectionService.Format(item.WorkflowState)}",
            10,
            FontWeight.Normal,
            ResourceBrush("CanvasMutedBrush", "#66697A")));
        return new Border
        {
            Padding = new Thickness(11),
            Background = ResourceBrush("CanvasCardBackgroundBrush", "#FFFFFF"),
            BorderBrush = ResourceBrush("CanvasCardBorderBrush", "#D8DAE4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Child = content,
        };
    }

    private void RenderRelationships(IReadOnlyList<CanvasRelationshipProjection> relationships)
    {
        foreach (var relationship in relationships)
        {
            if (!_nodeBounds.TryGetValue(
                    (relationship.Edge.Source.NodeType, relationship.Edge.Source.EntityId),
                    out var source) ||
                !_nodeBounds.TryGetValue(
                    (relationship.Edge.Target.NodeType, relationship.Edge.Target.EntityId),
                    out var target))
            {
                continue;
            }

            var start = Anchor(source, target.Center);
            var end = Anchor(target, source.Center);
            var selected = _viewModel?.SelectedRelationship?.RelationshipId == relationship.Edge.RelationshipId;
            var related = _selectedNodeIds.Count == 0 ||
                _selectedNodeIds.Contains(relationship.Edge.Source.EntityId) ||
                _selectedNodeIds.Contains(relationship.Edge.Target.EntityId);
            Border? label = null;
            void ShowLabel()
            {
                if (label is not null)
                {
                    return;
                }
                label = Badge(
                    string.IsNullOrWhiteSpace(relationship.Edge.Label)
                        ? relationship.Type.Name
                        : relationship.Edge.Label!,
                    "#F7F7FA",
                    relationship.Type.ColorHex);
                label.IsHitTestVisible = false;
                Place(label, new Point((start.X + end.X) / 2 - 35, (start.Y + end.Y) / 2 - 12));
                Surface.Children.Add(label);
            }
            var line = new Line
            {
                StartPoint = start,
                EndPoint = end,
                Stroke = Brush.Parse(relationship.Type.ColorHex),
                StrokeThickness = selected ? 5 : 3,
                Opacity = related ? .9 : .16,
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = relationship.Edge,
            };
            line.PointerPressed += (_, eventArgs) =>
            {
                if (_viewModel is not null)
                {
                    _selectedNodeIds.Clear();
                    _viewModel.SelectedRelationship = relationship.Edge;
                    _viewModel.InspectorTabIndex = 1;
                    SelectionStateChanged?.Invoke(this, EventArgs.Empty);
                    RenderGraph();
                }
                eventArgs.Handled = true;
            };
            line.PointerEntered += (_, _) => ShowLabel();
            line.PointerExited += (_, _) =>
            {
                if (!selected && !(_selectedNodeIds.Count > 0 && related) && label is not null)
                {
                    Surface.Children.Remove(label);
                    label = null;
                }
            };
            AutomationProperties.SetName(
                line,
                $"{relationship.Type.Name} relationship{(string.IsNullOrWhiteSpace(relationship.Edge.Label) ? string.Empty : $", {relationship.Edge.Label}")}");
            Surface.Children.Insert(GridVisualCount(), line);

            Polygon? arrow = null;
            if (relationship.Type.IsDirectional)
            {
                arrow = CreateArrow(start, end, relationship.Type.ColorHex, related ? .9 : .16);
                Surface.Children.Insert(GridVisualCount(), arrow);
            }

            if (selected || related && _selectedNodeIds.Count > 0)
            {
                ShowLabel();
            }
        }
    }

    private void DrawGrid()
    {
        var insert = 0;
        for (var x = 0d; x <= Surface.Width; x += 32)
        {
            Surface.Children.Insert(insert++, GridLine(new Point(x, 0), new Point(x, Surface.Height), x % 160 == 0));
        }
        for (var y = 0d; y <= Surface.Height; y += 32)
        {
            Surface.Children.Insert(insert++, GridLine(new Point(0, y), new Point(Surface.Width, y), y % 160 == 0));
        }
    }

    private void RenderEmptyState(CanvasBoardProjection projection)
    {
        if (projection.Frames.Count > 0 || projection.DependencyNodes.Count > 0)
        {
            return;
        }
        var empty = new StackPanel { Spacing = 7 };
        empty.Children.Add(Text("No work matches this view", 20, FontWeight.Bold));
        empty.Children.Add(Text(
            _viewModel?.Versions.Count == 0
                ? "Add a version to begin shaping the blueprint."
                : "Clear search or filters to bring work back into view.",
            12,
            FontWeight.Normal,
            ResourceBrush("CanvasMutedBrush", "#66697A")));
        Place(empty, new Point(90, 100));
        Surface.Children.Add(empty);
    }

    private void HandleConnectionEndpoint(RelationshipEndpoint endpoint)
    {
        if (_viewModel?.CanMutateWorkspace != true || _viewModel.RelationshipTypes.Count == 0)
        {
            return;
        }
        if (_connectionSource is null)
        {
            _connectionSource = endpoint;
            return;
        }
        if (_connectionSource == endpoint)
        {
            return;
        }

        _viewModel.BeginNewRelationshipCommand.Execute(null);
        _viewModel.SelectedRelationshipSource = _viewModel.RelationshipEndpoints.FirstOrDefault(
            option => option.Endpoint == _connectionSource);
        _viewModel.SelectedRelationshipTarget = _viewModel.RelationshipEndpoints.FirstOrDefault(
            option => option.Endpoint == endpoint);
        _viewModel.InspectorTabIndex = 1;
        _connectionSource = null;
        _connectMode = false;
        ConnectModeChanged?.Invoke(this, EventArgs.Empty);
        RenderGraph();
    }

    private void SelectProjectedNode(CanvasDependencyNode node, KeyModifiers modifiers)
    {
        UpdateSelection(node.EntityId, modifiers);
        if (_viewModel is null)
        {
            return;
        }
        if (node.NodeType == "version")
        {
            var version = _viewModel.Versions.FirstOrDefault(value => value.VersionId == node.EntityId);
            if (version is not null)
            {
                _viewModel.SelectVersionNodeCommand.Execute(version);
            }
        }
        else if (node.NodeType == "item")
        {
            var item = _viewModel.Versions.SelectMany(static version => version.Items)
                .FirstOrDefault(value => value.ItemId == node.EntityId);
            if (item is not null)
            {
                _viewModel.SelectItemNodeCommand.Execute(item);
            }
        }
    }

    private void SelectVersion(WorkspaceVersionCard version, KeyModifiers modifiers = KeyModifiers.None)
    {
        UpdateSelection(version.VersionId, modifiers);
        _viewModel?.SelectVersionNodeCommand.Execute(version);
    }

    private void SelectItem(WorkspaceItemCard item, KeyModifiers modifiers)
    {
        UpdateSelection(item.ItemId, modifiers);
        _viewModel?.SelectItemNodeCommand.Execute(item);
    }

    private void UpdateSelection(Guid id, KeyModifiers modifiers)
    {
        var additive = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift);
        if (additive)
        {
            if (!_selectedNodeIds.Add(id))
            {
                _selectedNodeIds.Remove(id);
            }
        }
        else
        {
            _selectedNodeIds.Clear();
            _selectedNodeIds.Add(id);
        }
        UpdateSelectionVisuals();
        SelectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var (identity, control) in _nodes)
        {
            if (control is not Border border)
            {
                continue;
            }
            var selected = _selectedNodeIds.Contains(identity.Id) ||
                identity.Type == "version" && _viewModel?.SelectedVersion?.VersionId == identity.Id ||
                identity.Type == "item" && _viewModel?.SelectedItem?.ItemId == identity.Id;
            border.BorderThickness = new Thickness(selected ? 2.5 : 1);
            border.BorderBrush = selected
                ? ResourceBrush("CanvasSelectionBrush", "#6254D9")
                : identity.Type == "version"
                    ? ResourceBrush("CanvasFrameBorderBrush", "#D6D8E2")
                    : IsBlocked(identity.Id)
                        ? ResourceBrush("CanvasWarningBrush", "#C64D42")
                        : ResourceBrush("CanvasCardBorderBrush", "#D8DAE4");
        }
    }

    private void AddFreeNodeDrag(Control node, (string Type, Guid Id) identity)
    {
        node.PointerPressed += (_, eventArgs) =>
        {
            if (eventArgs.Handled)
            {
                return;
            }
            BeginNodeDrag(node, identity, eventArgs);
        };
        AddNodeDragContinuation(node);
    }

    private void BeginNodeDrag(Control node, (string Type, Guid Id) identity, PointerPressedEventArgs eventArgs)
    {
        if (_viewModel?.CanMutateWorkspace != true ||
            !eventArgs.GetCurrentPoint(node).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _draggedNode = node;
        _draggedIdentity = identity;
        _dragStartPointer = eventArgs.GetPosition(Surface);
        _dragStartPosition = _positions.GetValueOrDefault(identity.Id);
        _dragStartLayout = CaptureLayout();
        eventArgs.Pointer.Capture(node);
        eventArgs.Handled = true;
    }

    private void AddNodeDragContinuation(Control node)
    {
        node.PointerMoved += (_, eventArgs) =>
        {
            if (_draggedNode != node || _draggedIdentity is null ||
                !eventArgs.GetCurrentPoint(node).Properties.IsLeftButtonPressed)
            {
                return;
            }
            var delta = eventArgs.GetPosition(Surface) - _dragStartPointer;
            var position = new Point(
                Math.Max(0, _dragStartPosition.X + delta.X),
                Math.Max(0, _dragStartPosition.Y + delta.Y));
            _positions[_draggedIdentity.Value.Id] = position;
            Place(node, position);
        };
        node.PointerReleased += (_, eventArgs) =>
        {
            if (_draggedNode != node)
            {
                return;
            }
            _draggedNode = null;
            _draggedIdentity = null;
            eventArgs.Pointer.Capture(null);
            RenderGraph();
            if (_dragStartLayout is not null && _layoutHistory.Record(_dragStartLayout, CaptureLayout()))
            {
                PersistLayout();
                HistoryStateChanged?.Invoke(this, EventArgs.Empty);
            }
            _dragStartLayout = null;
        };
    }

    private void AddResizeHandlers(Control handle, Control frame, Guid versionId)
    {
        Point start = default;
        Size size = default;
        var resizing = false;
        handle.PointerPressed += (_, eventArgs) =>
        {
            if (_viewModel?.CanMutateWorkspace != true)
            {
                return;
            }
            resizing = true;
            start = eventArgs.GetPosition(Surface);
            size = new Size(frame.Width, frame.Height);
            eventArgs.Pointer.Capture(handle);
            eventArgs.Handled = true;
        };
        handle.PointerMoved += (_, eventArgs) =>
        {
            if (!resizing)
            {
                return;
            }
            var delta = eventArgs.GetPosition(Surface) - start;
            frame.Width = Math.Clamp(size.Width + delta.X, FrameWidth, 1800);
            frame.Height = Math.Clamp(size.Height + delta.Y, 360, 2400);
        };
        handle.PointerReleased += (_, eventArgs) =>
        {
            if (!resizing)
            {
                return;
            }
            resizing = false;
            _frameSizes[versionId] = new Size(frame.Width, frame.Height);
            eventArgs.Pointer.Capture(null);
            RenderGraph();
            eventArgs.Handled = true;
        };
    }

    private void HandleSurfacePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        Focus();
        var properties = eventArgs.GetCurrentPoint(Surface).Properties;
        if (properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStart = eventArgs.GetPosition(Viewport);
            _panOrigin = Viewport.Offset;
            eventArgs.Pointer.Capture(Surface);
            return;
        }
        if (!properties.IsLeftButtonPressed)
        {
            return;
        }
        _isBoxSelecting = true;
        _boxStart = eventArgs.GetPosition(Surface);
        if (!eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _selectedNodeIds.Clear();
        }
        _boxVisual = new Rectangle
        {
            Fill = ResourceBrush("CanvasSelectionFillBrush", "#226254D9"),
            Stroke = ResourceBrush("CanvasSelectionBrush", "#6254D9"),
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
        };
        Place(_boxVisual, _boxStart);
        Surface.Children.Add(_boxVisual);
        eventArgs.Pointer.Capture(Surface);
    }

    private void HandleSurfacePointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (_isPanning)
        {
            var delta = eventArgs.GetPosition(Viewport) - _panStart;
            Viewport.Offset = new Vector(
                Math.Max(0, _panOrigin.X - delta.X),
                Math.Max(0, _panOrigin.Y - delta.Y));
            return;
        }
        if (!_isBoxSelecting || _boxVisual is null)
        {
            return;
        }
        var point = eventArgs.GetPosition(Surface);
        var selection = new Rect(
            Math.Min(_boxStart.X, point.X),
            Math.Min(_boxStart.Y, point.Y),
            Math.Abs(point.X - _boxStart.X),
            Math.Abs(point.Y - _boxStart.Y));
        Place(_boxVisual, selection.Position);
        _boxVisual.Width = selection.Width;
        _boxVisual.Height = selection.Height;
        foreach (var pair in _nodeBounds.Where(pair => pair.Value.Intersects(selection)))
        {
            _selectedNodeIds.Add(pair.Key.Id);
        }
        SelectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleSurfacePointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (_isPanning)
        {
            _isPanning = false;
            PersistViewState();
        }
        if (_isBoxSelecting)
        {
            _isBoxSelecting = false;
            if (_boxVisual is not null)
            {
                Surface.Children.Remove(_boxVisual);
                _boxVisual = null;
            }
            RenderGraph();
        }
        eventArgs.Pointer.Capture(null);
    }

    private void HandlePointerWheel(object? sender, PointerWheelEventArgs eventArgs)
    {
        if (!eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }
        var pointer = eventArgs.GetPosition(Viewport);
        var content = new Point(
            (Viewport.Offset.X + pointer.X) / _zoom,
            (Viewport.Offset.Y + pointer.Y) / _zoom);
        SetZoom(_zoom + Math.Sign(eventArgs.Delta.Y) * .1);
        Viewport.Offset = new Vector(
            Math.Max(0, content.X * _zoom - pointer.X),
            Math.Max(0, content.Y * _zoom - pointer.Y));
        PersistViewState();
        eventArgs.Handled = true;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var command = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control) ||
            eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command && eventArgs.Key == Key.S) SaveLayout();
        else if (command && eventArgs.Key == Key.Z && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift)) RedoLayout();
        else if (command && eventArgs.Key == Key.Z) UndoLayout();
        else if (command && eventArgs.Key == Key.Y) RedoLayout();
        else if (command && eventArgs.Key == Key.D0) FitView();
        else if (command && eventArgs.Key is Key.Add or Key.OemPlus) ZoomIn();
        else if (command && eventArgs.Key is Key.Subtract or Key.OemMinus) ZoomOut();
        else if (command && eventArgs.Key == Key.F) SearchRequested?.Invoke(this, EventArgs.Empty);
        else if (command && eventArgs.Key == Key.L) ToggleConnectMode();
        else if (command && eventArgs.Key == Key.J) ZoomToSelection();
        else if (command && eventArgs.Key == Key.V &&
                 eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
            _viewModel?.CreateVersionCommand.Execute(null);
        else if (command && eventArgs.Key == Key.I &&
                 eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
            _viewModel?.BeginNewItemCommand.Execute(null);
        else if (command && eventArgs.Key == Key.D7) SetViewMode(CanvasViewMode.Plan);
        else if (command && eventArgs.Key == Key.D8) SetViewMode(CanvasViewMode.Dependencies);
        else if (command && eventArgs.Key == Key.D9) SetViewMode(CanvasViewMode.ReleaseNotes);
        else if (eventArgs.Key == Key.Enter && _selectedNodeIds.Count > 0) OpenSelectedInspector();
        else if (eventArgs.Key == Key.Escape)
        {
            _connectMode = false;
            _connectionSource = null;
            _selectedNodeIds.Clear();
            RenderGraph();
        }
        else if (!command && eventArgs.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveSelected(eventArgs.Key, eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1);
        }
        else return;
        eventArgs.Handled = true;
    }

    public event EventHandler? SearchRequested;

    private void OpenSelectedInspector()
    {
        if (_viewModel is null)
        {
            return;
        }
        var selected = _selectedNodeIds.FirstOrDefault();
        var node = _nodes.Keys.FirstOrDefault(key => key.Id == selected);
        if (node.Id != Guid.Empty)
        {
            var projected = _viewModel.Versions
                .SelectMany(version => version.Items.Select(item => (version, item)))
                .FirstOrDefault(pair => pair.item.ItemId == node.Id);
            if (projected.item is not null)
            {
                SelectItem(projected.item, KeyModifiers.None);
            }
        }
    }

    private void MoveSelected(Key key, double amount)
    {
        if (_viewModel?.CanMutateWorkspace != true || _selectedNodeIds.Count == 0)
        {
            return;
        }
        if (_viewMode == CanvasViewMode.Plan && (key is Key.Left or Key.Right))
        {
            var selectedItems = _viewModel.Versions
                .SelectMany(version => version.Items.Select(item => (version, item)))
                .Where(pair => _selectedNodeIds.Contains(pair.item.ItemId))
                .ToArray();
            foreach (var (version, item) in selectedItems)
            {
                var offset = key == Key.Left ? -1 : 1;
                var next = (WorkItemLifecycle)Math.Clamp(
                    (int)item.WorkflowState + offset,
                    (int)WorkItemLifecycle.Planned,
                    (int)WorkItemLifecycle.Complete);
                if (next != item.WorkflowState)
                {
                    _viewModel.ChangeItemLifecycleCommand.Execute(
                        new WorkItemLifecycleChangeRequest(version.VersionId, item.ItemId, next));
                }
            }
            return;
        }
        var previous = CaptureLayout();
        var delta = key switch
        {
            Key.Left => new Vector(-amount, 0),
            Key.Right => new Vector(amount, 0),
            Key.Up => new Vector(0, -amount),
            _ => new Vector(0, amount),
        };
        foreach (var id in _selectedNodeIds)
        {
            if (_positions.TryGetValue(id, out var point))
            {
                _positions[id] = new Point(Math.Max(0, point.X + delta.X), Math.Max(0, point.Y + delta.Y));
            }
        }
        RenderGraph();
        if (_layoutHistory.Record(previous, CaptureLayout()))
        {
            PersistLayout();
            HistoryStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleMiniMapPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(MiniMapSurface).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var point = eventArgs.GetPosition(MiniMapSurface);
        Viewport.Offset = new Vector(
            Math.Max(0, point.X / MiniMapSurface.Width * Surface.Width * _zoom - Viewport.Viewport.Width / 2),
            Math.Max(0, point.Y / MiniMapSurface.Height * Surface.Height * _zoom - Viewport.Viewport.Height / 2));
        PersistViewState();
    }

    private void RenderMiniMap()
    {
        if (MiniMapSurface is null)
        {
            return;
        }
        MiniMapPanel.IsVisible = _minimapVisible;
        MiniMapSurface.Children.Clear();
        var scale = Math.Min(MiniMapSurface.Width / Surface.Width, MiniMapSurface.Height / Surface.Height);
        var minimapNodes = CanvasMiniMapProjectionService.Project(
            _nodeBounds.Select(pair => new CanvasNodeBounds(
                pair.Key.Id,
                pair.Value.X,
                pair.Value.Y,
                pair.Value.Width,
                pair.Value.Height)).ToArray(),
            _selectedNodeIds,
            Surface.Width,
            Surface.Height,
            MiniMapSurface.Width,
            MiniMapSurface.Height);
        var versionIds = _nodeBounds.Keys
            .Where(static key => key.Type == "version")
            .Select(static key => key.Id)
            .ToHashSet();
        foreach (var node in minimapNodes)
        {
            var preview = new Rectangle
            {
                Width = node.Width,
                Height = node.Height,
                Fill = node.IsSelected
                    ? ResourceBrush("CanvasMinimapSelectionBrush", "#F1C96A")
                    : versionIds.Contains(node.EntityId)
                        ? ResourceBrush("CanvasMinimapFrameBrush", "#8D83DF")
                        : ResourceBrush("CanvasMinimapNodeBrush", "#56AFA2"),
                IsHitTestVisible = false,
            };
            Place(preview, new Point(node.X, node.Y));
            MiniMapSurface.Children.Add(preview);
        }
        var viewport = new Rectangle
        {
            Width = Math.Clamp(Viewport.Viewport.Width / _zoom * scale, 2, MiniMapSurface.Width),
            Height = Math.Clamp(Viewport.Viewport.Height / _zoom * scale, 2, MiniMapSurface.Height),
            Stroke = ResourceBrush("CanvasSelectionBrush", "#6254D9"),
            StrokeThickness = 1.5,
            Fill = ResourceBrush("CanvasViewportFillBrush", "#166254D9"),
            IsHitTestVisible = false,
        };
        Place(viewport, new Point(
            Math.Clamp(Viewport.Offset.X / _zoom * scale, 0, Math.Max(0, MiniMapSurface.Width - viewport.Width)),
            Math.Clamp(Viewport.Offset.Y / _zoom * scale, 0, Math.Max(0, MiniMapSurface.Height - viewport.Height))));
        MiniMapSurface.Children.Add(viewport);
    }

    private void RestoreLayout()
    {
        _positions.Clear();
        foreach (var node in _viewModel?.CanvasLayout?.Nodes ?? [])
        {
            _positions[node.EntityId] = new Point(node.X, node.Y);
        }
    }

    private void RestoreViewState()
    {
        if (_viewModel is null)
        {
            return;
        }
        var state = _viewModel.CanvasViewState;
        _isRestoring = true;
        _viewMode = state.ViewMode;
        _searchText = state.SearchText;
        _lifecycleFilter = state.LifecycleFilter;
        _versionFilter = state.VersionFilter;
        _itemTypeFilter = state.ItemTypeFilter;
        _categoryFilter = state.CategoryFilter;
        _warningsOnly = state.WarningsOnly;
        _minimapVisible = state.MinimapVisible;
        _collapsedVersionIds.Clear();
        _collapsedVersionIds.UnionWith(state.ParseCollapsedVersionIds());
        SetZoom(state.Zoom);
        Dispatcher.UIThread.Post(() =>
        {
            Viewport.Offset = new Vector(state.HorizontalOffset, state.VerticalOffset);
            _isRestoring = false;
        }, DispatcherPriority.Loaded);
    }

    private void PersistViewState()
    {
        if (_isRestoring || _viewModel is null)
        {
            return;
        }
        _viewModel.SaveCanvasViewStateCommand.Execute(new CanvasViewState(
            _zoom,
            Math.Max(0, Viewport.Offset.X),
            Math.Max(0, Viewport.Offset.Y),
            _viewMode,
            _searchText,
            _lifecycleFilter,
            _versionFilter,
            _minimapVisible,
            string.Join(',', _collapsedVersionIds.Order()),
            _itemTypeFilter,
            _categoryFilter,
            _warningsOnly));
    }

    private void PersistLayout()
    {
        if (_isRestoring || _viewModel?.CanMutateWorkspace != true)
        {
            return;
        }
        _isPersisting = true;
        try
        {
            _viewModel.SaveCanvasLayoutCommand.Execute(new CanvasLayoutEditRequest(CaptureLayout()));
        }
        finally
        {
            _isPersisting = false;
        }
    }

    private IReadOnlyList<CanvasNodeLayoutEdit> CaptureLayout()
    {
        if (_viewModel is null)
        {
            return [];
        }
        var result = new List<CanvasNodeLayoutEdit>();
        if (_viewModel.CurrentProject.ProjectId != Guid.Empty)
        {
            AddLayout(result, "project", _viewModel.CurrentProject.ProjectId);
        }
        foreach (var version in _viewModel.Versions)
        {
            AddLayout(result, "version", version.VersionId);
            foreach (var item in version.Items)
            {
                AddLayout(result, "item", item.ItemId);
            }
        }
        return result;
    }

    private void AddLayout(ICollection<CanvasNodeLayoutEdit> result, string type, Guid id)
    {
        if (!_positions.TryGetValue(id, out var position))
        {
            position = type == "version"
                ? new Point(70, 70 + result.Count * 80)
                : DependencyPosition(result.Count);
            _positions[id] = position;
        }
        result.Add(new CanvasNodeLayoutEdit(type, id, position.X, position.Y));
    }

    private void ApplyLayout(IReadOnlyList<CanvasNodeLayoutEdit> layout)
    {
        _positions.Clear();
        foreach (var node in layout)
        {
            _positions[node.EntityId] = new Point(node.X, node.Y);
        }
        RenderGraph();
        HistoryStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetZoom(double value, bool persist = false)
    {
        _zoom = Math.Clamp(value, .25, 2.5);
        ZoomHost.Width = Surface.Width * _zoom;
        ZoomHost.Height = Surface.Height * _zoom;
        Surface.RenderTransform = new ScaleTransform(_zoom, _zoom);
        Surface.RenderTransformOrigin = RelativePoint.TopLeft;
        RenderMiniMap();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        if (persist) PersistViewState();
    }

    private void SizeSurface()
    {
        var bounds = GetContentBounds();
        Surface.Width = Math.Max(1400, bounds.Right + 120);
        Surface.Height = Math.Max(900, bounds.Bottom + 120);
    }

    private Rect GetContentBounds() =>
        _nodeBounds.Count == 0
            ? new Rect(0, 0, 1200, 760)
            : Union(_nodeBounds.Values);

    private static Rect Union(IEnumerable<Rect> rectangles)
    {
        var values = rectangles.ToArray();
        var left = values.Min(static rect => rect.Left);
        var top = values.Min(static rect => rect.Top);
        var right = values.Max(static rect => rect.Right);
        var bottom = values.Max(static rect => rect.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    private Point GetPosition(Guid id, Point fallback)
    {
        if (_positions.TryGetValue(id, out var value))
        {
            return value;
        }
        _positions[id] = fallback;
        return fallback;
    }

    private bool IsSelected(Guid id) => _selectedNodeIds.Contains(id);

    private bool IsBlocked(Guid id) =>
        _viewModel?.RelationshipDocument is { } document &&
        document.Relationships.Any(edge =>
            edge.Target.EntityId == id &&
            document.Types.Any(type =>
                type.TypeId == edge.TypeId &&
                (type.TypeId.Contains("block", StringComparison.OrdinalIgnoreCase) ||
                 type.Name.Contains("block", StringComparison.OrdinalIgnoreCase))));

    private int GridVisualCount() =>
        Surface.Children.TakeWhile(child => child.Tag as string == "grid").Count();

    private static Point DependencyPosition(int index) =>
        new(80 + index % 4 * 310, 80 + index / 4 * 170);

    private static Point Anchor(Rect source, Point toward)
    {
        var dx = toward.X - source.Center.X;
        var dy = toward.Y - source.Center.Y;
        if (Math.Abs(dx) * source.Height > Math.Abs(dy) * source.Width)
        {
            return new Point(dx > 0 ? source.Right : source.Left, source.Center.Y);
        }
        return new Point(source.Center.X, dy > 0 ? source.Bottom : source.Top);
    }

    private static Polygon CreateArrow(Point start, Point end, string color, double opacity)
    {
        var direction = start - end;
        var length = Math.Max(1, Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
        var unit = new Vector(direction.X / length, direction.Y / length);
        var normal = new Vector(-unit.Y, unit.X);
        return new Polygon
        {
            Points =
            [
                end,
                end + unit * 14 + normal * 7,
                end + unit * 14 - normal * 7,
            ],
            Fill = Brush.Parse(color),
            Opacity = opacity,
            IsHitTestVisible = false,
        };
    }

    private static Line GridLine(Point start, Point end, bool major) =>
        new()
        {
            StartPoint = start,
            EndPoint = end,
            Stroke = Brush.Parse(major ? "#DFDFE8" : "#ECECF2"),
            StrokeThickness = major ? 1 : .5,
            IsHitTestVisible = false,
            Tag = "grid",
        };

    private static Border Badge(string value, string background, string foreground) =>
        new()
        {
            Background = Brush.Parse(background),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(7, 3),
            Child = Text(value, 9, FontWeight.SemiBold, Brush.Parse(foreground)),
        };

    private static Button SmallButton(string text, string tooltip)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(9, 5),
            MinHeight = 28,
            FontSize = 10,
        };
        ToolTip.SetTip(button, tooltip);
        AutomationProperties.SetName(button, tooltip);
        return button;
    }

    private static TextBlock Text(
        string value,
        double size,
        FontWeight weight,
        IBrush? foreground = null)
    {
        var text = new TextBlock
        {
            Text = value,
            FontSize = size,
            FontWeight = weight,
        };
        // Leaving Foreground unset lets the app theme supply a readable default.
        // Assigning null locally suppresses inheritance and made card titles vanish.
        if (foreground is not null)
        {
            text.Foreground = foreground;
        }

        return text;
    }

    private IBrush ResourceBrush(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brush.Parse(fallback);

    private IBrush LifecycleBrush(WorkItemLifecycle state) =>
        Brush.Parse(LifecycleColor(state));

    private static string LifecycleColor(WorkItemLifecycle state) => state switch
    {
        WorkItemLifecycle.Planned => "#5E6270",
        WorkItemLifecycle.InProgress => "#4C56B8",
        WorkItemLifecycle.Review => "#8A5D15",
        _ => "#25715F",
    };

    private static string LifecycleBackground(WorkItemLifecycle state) => state switch
    {
        WorkItemLifecycle.Planned => "#ECEEF2",
        WorkItemLifecycle.InProgress => "#E8E9FA",
        WorkItemLifecycle.Review => "#FAF0D9",
        _ => "#E1F3ED",
    };

    private static void Place(Control control, Point point)
    {
        Canvas.SetLeft(control, point.X);
        Canvas.SetTop(control, point.Y);
    }

    private sealed record NodeTag(string Type, Guid Id);
}
