using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
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

namespace Blueprints.App.Views.Components;

public partial class BlueprintCanvasSurface : UserControl
{
    private const double NodeWidth = 220;
    private const double VersionNodeHeight = 104;
    private const double ItemNodeHeight = 82;
    private readonly Dictionary<Guid, Point> _positions = [];
    private readonly Dictionary<Guid, Control> _nodes = [];
    private readonly HashSet<Guid> _selectedNodeIds = [];
    private readonly List<ConnectionVisual> _connections = [];
    private readonly List<Line> _alignmentGuideVisuals = [];
    private readonly CanvasLayoutHistory _layoutHistory = new();
    private MainWindowViewModel? _viewModel;
    private Control? _draggedNode;
    private IReadOnlyList<CanvasNodeLayoutEdit>? _dragStartLayout;
    private IReadOnlyDictionary<Guid, Point> _dragStartPositions = new Dictionary<Guid, Point>();
    private Point _dragStartPointer;
    private double _zoom = 1;
    private bool _isRestoringLayout;
    private bool _isPersistingLayout;
    private readonly DispatcherTimer _viewportSaveTimer;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;
    private bool _isBoxSelecting;
    private Point _boxSelectionStart;
    private Rectangle? _boxSelectionVisual;
    private IReadOnlySet<Guid> _selectionBeforeBox = new HashSet<Guid>();

    public event EventHandler? HistoryStateChanged;

    public event EventHandler? SelectionStateChanged;

    public bool CanUndo => _viewModel?.CanMutateWorkspace == true && _layoutHistory.CanUndo;

    public bool CanRedo => _viewModel?.CanMutateWorkspace == true && _layoutHistory.CanRedo;

    public string SelectionSummary =>
        _selectedNodeIds.Count switch
        {
            0 => "No canvas nodes selected",
            1 => "1 canvas node selected",
            _ => $"{_selectedNodeIds.Count} canvas nodes selected",
        };

    public BlueprintCanvasSurface()
    {
        InitializeComponent();
        _viewportSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650),
        };
        _viewportSaveTimer.Tick += HandleViewportSaveTimerTick;
        Viewport.ScrollChanged += HandleViewportScrollChanged;
        Surface.PointerPressed += HandleSurfacePointerPressed;
        Surface.PointerMoved += HandleSurfacePointerMoved;
        Surface.PointerReleased += HandleSurfacePointerReleased;
        Surface.PointerWheelChanged += HandleSurfacePointerWheelChanged;
        KeyDown += HandleKeyDown;
        DataContextChanged += HandleDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachViewModel();
    }

    public void ZoomIn() => SetZoom(_zoom + 0.1, persist: true);

    public void ZoomOut() => SetZoom(_zoom - 0.1, persist: true);

    public void FitView()
    {
        SetZoom(0.8);
        Viewport.Offset = Vector.Zero;
        PersistViewState();
    }

    public void AutoArrange()
    {
        if (_viewModel?.CanMutateWorkspace != true)
        {
            return;
        }

        var previous = CaptureLayout();
        _positions.Clear();
        SetZoom(1);
        RenderGraph();
        _layoutHistory.Record(previous, CaptureLayout());
        NotifyHistoryStateChanged();
        Viewport.Offset = Vector.Zero;
        PersistLayout();
        PersistViewState();
    }

    public void SaveLayout() => PersistLayout();

    public void UndoLayout()
    {
        if (_viewModel?.CanMutateWorkspace != true
            || !_layoutHistory.TryUndo(CaptureLayout(), out var previous))
        {
            return;
        }

        ApplyLayout(previous);
        PersistLayout();
        NotifyHistoryStateChanged();
    }

    public void RedoLayout()
    {
        if (_viewModel?.CanMutateWorkspace != true
            || !_layoutHistory.TryRedo(CaptureLayout(), out var next))
        {
            return;
        }

        ApplyLayout(next);
        PersistLayout();
        NotifyHistoryStateChanged();
    }

    private void SetZoom(double value, bool persist = false)
    {
        _zoom = Math.Clamp(value, 0.6, 1.5);
        ZoomHost.Width = Surface.Width * _zoom;
        ZoomHost.Height = Surface.Height * _zoom;
        Surface.RenderTransform = new ScaleTransform(_zoom, _zoom);
        Surface.RenderTransformOrigin = RelativePoint.TopLeft;
        if (persist)
        {
            PersistViewState();
        }

        RenderMiniMap();
    }

    private void HandleDataContextChanged(object? sender, EventArgs eventArgs)
    {
        DetachViewModel();
        ClearLayoutHistory();
        _selectedNodeIds.Clear();
        NotifySelectionStateChanged();
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
            _viewModel.Versions.CollectionChanged += HandleVersionsChanged;
        }

        RestoreLayout();
        RenderGraph();
        RestoreViewState();
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
        _viewportSaveTimer.Stop();
        _isRestoringLayout = true;
        if (!_isPersistingLayout)
        {
            ClearLayoutHistory();
        }

        RenderGraph();
        Dispatcher.UIThread.Post(
            () => _isRestoringLayout = false,
            DispatcherPriority.Loaded);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainWindowViewModel.SelectedVersion)
            or nameof(MainWindowViewModel.SelectedItem))
        {
            UpdateSelectionVisuals();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.CanvasLayout))
        {
            if (!_isPersistingLayout)
            {
                ClearLayoutHistory();
            }

            RestoreLayout();
            RenderGraph();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.CanvasViewState))
        {
            RestoreViewState();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.VersionCount)
            or nameof(MainWindowViewModel.ItemCount))
        {
            RenderGraph();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.CanMutateWorkspace))
        {
            NotifyHistoryStateChanged();
        }
    }

    private void UpdateSelectionVisuals()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var border in Surface.Children.OfType<Border>())
        {
            if (border.Tag is not NodeTag tag || tag.Id is not Guid id)
            {
                continue;
            }

            var selected = _selectedNodeIds.Contains(id)
                || tag.Type switch
                {
                    "version" => _viewModel.SelectedVersion?.VersionId == id,
                    "item" => _viewModel.SelectedItem?.ItemId == id,
                    _ => false,
                };
            border.Background = Brush.Parse(
                selected
                    ? "#12669A"
                    : tag.Type switch
                    {
                        "project" => "#0A2035",
                        "version" => "#0B3D62",
                        _ => "#0A3656",
                    });
            border.BorderBrush = Brush.Parse(
                selected
                    ? "#FFFFFF"
                    : tag.Type switch
                    {
                        "project" => "#65D2EF",
                        "version" => "#4EA9CC",
                        _ => "#397F9F",
                    });
        }
    }

    private void RenderGraph()
    {
        Surface.Children.Clear();
        _nodes.Clear();
        _connections.Clear();
        _alignmentGuideVisuals.Clear();
        DrawGrid();

        if (_viewModel is null)
        {
            return;
        }

        var totalItems = _viewModel.Versions.Sum(static version => version.Items.Count);
        Surface.Height = Math.Max(900, 180 + Math.Max(_viewModel.Versions.Count * 220, totalItems * 108));
        Surface.Width = 1260;
        SetZoom(_zoom);

        var projectNode = CreateProjectNode();
        var projectPoint = GetPosition(
            _viewModel.CurrentProject.ProjectId,
            new Point(48, Math.Max(310, Surface.Height / 2 - 70)));
        Place(projectNode, projectPoint);
        Surface.Children.Add(projectNode);
        RegisterNode(projectNode);

        var globalItemIndex = 0;
        for (var versionIndex = 0; versionIndex < _viewModel.Versions.Count; versionIndex++)
        {
            var version = _viewModel.Versions[versionIndex];
            var versionPoint = GetPosition(
                version.VersionId,
                new Point(360, 90 + versionIndex * 220));
            var versionNode = CreateVersionNode(version);
            Place(versionNode, versionPoint);
            Surface.Children.Add(versionNode);
            RegisterNode(versionNode);
            AddConnection(projectNode, versionNode, "#52C7E8", 2);

            foreach (var item in version.Items)
            {
                var itemPoint = GetPosition(
                    item.ItemId,
                    new Point(700, 70 + globalItemIndex * 108));
                var itemNode = CreateItemNode(version, item);
                Place(itemNode, itemPoint);
                Surface.Children.Add(itemNode);
                RegisterNode(itemNode);
                AddConnection(versionNode, itemNode, item.IsDone ? "#5DD6B2" : "#73AFCF", item.IsDone ? 2 : 1.25);
                globalItemIndex++;
            }
        }

        foreach (var connection in _connections)
        {
            Surface.Children.Insert(FindGridVisualCount(), connection.Line);
            UpdateConnection(connection);
        }

        var selectionChanged = _selectedNodeIds.RemoveWhere(id => !_nodes.ContainsKey(id)) > 0;
        UpdateSelectionVisuals();
        RenderMiniMap();
        if (selectionChanged)
        {
            NotifySelectionStateChanged();
        }

        if (_viewModel.Versions.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "CREATE THE FIRST VERSION TO START DRAWING",
                FontFamily = FontFamily.Parse("Cascadia Mono, SFMono-Regular, Consolas, monospace"),
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#76CDE8"),
            };
            Place(empty, new Point(360, 330));
            Surface.Children.Add(empty);
        }
    }

    private void RegisterNode(Control node)
    {
        if (node.Tag is NodeTag { Id: Guid nodeId })
        {
            _nodes[nodeId] = node;
        }
    }

    private int FindGridVisualCount()
    {
        var count = 0;
        foreach (var child in Surface.Children)
        {
            if (child is not Line line || line.Tag as string != "grid")
            {
                break;
            }

            count++;
        }

        return count;
    }

    private void DrawGrid()
    {
        for (var x = 0; x <= Surface.Width; x += 40)
        {
            Surface.Children.Add(CreateGridLine(new Point(x, 0), new Point(x, Surface.Height), x % 200 == 0));
        }

        for (var y = 0; y <= Surface.Height; y += 40)
        {
            Surface.Children.Add(CreateGridLine(new Point(0, y), new Point(Surface.Width, y), y % 200 == 0));
        }
    }

    private static Line CreateGridLine(Point start, Point end, bool major) =>
        new()
        {
            StartPoint = start,
            EndPoint = end,
            Stroke = Brush.Parse(major ? "#1A587A" : "#10415F"),
            StrokeThickness = major ? 1 : 0.5,
            IsHitTestVisible = false,
            Tag = "grid",
        };

    private Control CreateProjectNode()
    {
        var content = new StackPanel { Spacing = 7 };
        content.Children.Add(CreateLabel("PROJECT ORIGIN", "#65D2EF"));
        content.Children.Add(CreateTitle(_viewModel?.CurrentProject.Name ?? "Project", 22));
        content.Children.Add(CreateLabel(
            $"{_viewModel?.CurrentProject.Code}  /  {_viewModel?.VersioningScheme}",
            "#A7CEE0"));
        content.Children.Add(CreateLabel(
            $"{_viewModel?.VersionCount ?? 0} versions  ·  {_viewModel?.ItemCount ?? 0} items",
            "#A7CEE0"));

        var node = CreateNodeShell(
            content,
            250,
            140,
            "#0A2035",
            "#65D2EF",
            "project",
            _viewModel?.CurrentProject.ProjectId);
        if (_viewModel?.CurrentProject.ProjectId is Guid projectId && projectId != Guid.Empty)
        {
            AddDragHandlers(node, projectId);
        }

        return node;
    }

    private Control CreateVersionNode(WorkspaceVersionCard version)
    {
        var selected = _viewModel?.SelectedVersion?.VersionId == version.VersionId;
        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(CreateLabel($"VERSION  /  {version.Status}", selected ? "#FFFFFF" : "#65D2EF"));
        content.Children.Add(CreateTitle(version.Name, 20));
        content.Children.Add(CreateLabel(
            $"{version.CompletedItemCount}/{version.ItemCount} complete",
            version.CompletedItemCount == version.ItemCount && version.ItemCount > 0 ? "#64DBB6" : "#A7CEE0"));

        var node = CreateNodeShell(
            content,
            NodeWidth,
            VersionNodeHeight,
            selected ? "#12669A" : "#0B3D62",
            selected ? "#FFFFFF" : "#4EA9CC",
            "version",
            version.VersionId);
        node.AddHandler(
            PointerPressedEvent,
            (_, _) => _viewModel?.SelectVersionNodeCommand.Execute(version),
            RoutingStrategies.Tunnel);
        AddDragHandlers(node, version.VersionId);
        return node;
    }

    private Control CreateItemNode(WorkspaceVersionCard version, WorkspaceItemCard item)
    {
        var selected = _viewModel?.SelectedItem?.ItemId == item.ItemId;
        var content = new StackPanel { Spacing = 4 };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(CreateLabel(item.ItemKey, selected ? "#FFFFFF" : "#6ED1ED"));
        var state = CreateLabel(item.IsDone ? "DONE" : "OPEN", item.IsDone ? "#64DBB6" : "#F2C66D");
        Grid.SetColumn(state, 1);
        heading.Children.Add(state);
        content.Children.Add(heading);
        content.Children.Add(CreateTitle(item.Title, 15));
        content.Children.Add(CreateLabel($"{item.ItemTypeId}  /  {item.CategoryId}", "#9FC5D8"));

        var node = CreateNodeShell(
            content,
            NodeWidth,
            ItemNodeHeight,
            selected ? "#12669A" : "#0A3656",
            selected ? "#FFFFFF" : item.IsDone ? "#4BB99B" : "#397F9F",
            "item",
            item.ItemId);
        node.AddHandler(
            PointerPressedEvent,
            (_, _) => _viewModel?.SelectItemNodeCommand.Execute(item),
            RoutingStrategies.Tunnel);
        AddDragHandlers(node, item.ItemId);
        return node;
    }

    private static Border CreateNodeShell(
        Control content,
        double width,
        double height,
        string background,
        string border,
        string nodeType,
        Guid? nodeId) =>
        new()
        {
            Width = width,
            Height = height,
            Padding = new Thickness(15, 12),
            Background = Brush.Parse(background),
            BorderBrush = Brush.Parse(border),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(3),
            Child = content,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = new NodeTag(nodeType, nodeId),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 12,
                OffsetX = 0,
                OffsetY = 4,
                Color = Color.Parse("#55000000"),
            }),
        };

    private static TextBlock CreateLabel(string text, string foreground) =>
        new()
        {
            Text = text,
            FontFamily = FontFamily.Parse("Cascadia Mono, SFMono-Regular, Consolas, monospace"),
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.8,
            Foreground = Brush.Parse(foreground),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

    private static TextBlock CreateTitle(string text, double size) =>
        new()
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

    private void AddDragHandlers(Control node, Guid nodeId)
    {
        node.AddHandler(
            PointerPressedEvent,
            (_, eventArgs) =>
            {
                if (!eventArgs.GetCurrentPoint(node).Properties.IsLeftButtonPressed)
                {
                    return;
                }

                UpdateNodeSelection(nodeId, eventArgs.KeyModifiers);
                Focus();
                eventArgs.Handled = true;
                if (_viewModel?.CanMutateWorkspace != true || !_selectedNodeIds.Contains(nodeId))
                {
                    return;
                }

                _draggedNode = node;
                _dragStartLayout = CaptureLayout();
                _dragStartPointer = eventArgs.GetPosition(Surface);
                _dragStartPositions = _selectedNodeIds
                    .Where(_positions.ContainsKey)
                    .ToDictionary(id => id, id => _positions[id]);
                eventArgs.Pointer.Capture(node);
            },
            RoutingStrategies.Tunnel);
        node.PointerMoved += (_, eventArgs) =>
        {
            if (_viewModel?.CanMutateWorkspace != true
                || _draggedNode != node
                || !eventArgs.GetCurrentPoint(node).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var pointer = eventArgs.GetPosition(Surface);
            var requestedDelta = pointer - _dragStartPointer;
            var selectedBounds = GetNodeBounds(_dragStartPositions);
            var constrained = CanvasSelectionService.ConstrainMove(
                selectedBounds,
                requestedDelta.X,
                requestedDelta.Y,
                Surface.Width,
                Surface.Height);
            MoveNodesFromOrigins(_dragStartPositions, constrained.DeltaX, constrained.DeltaY);
            RenderAlignmentGuides();
        };
        node.PointerReleased += (_, eventArgs) =>
        {
            if (_draggedNode == node)
            {
                _draggedNode = null;
                eventArgs.Pointer.Capture(null);
                if (_dragStartLayout is not null
                    && _layoutHistory.Record(_dragStartLayout, CaptureLayout()))
                {
                    NotifyHistoryStateChanged();
                    PersistLayout();
                }

                _dragStartLayout = null;
                _dragStartPositions = new Dictionary<Guid, Point>();
                ClearAlignmentGuides();
            }
        };
    }

    private void UpdateNodeSelection(Guid nodeId, KeyModifiers modifiers)
    {
        var additive = modifiers.HasFlag(KeyModifiers.Control)
            || modifiers.HasFlag(KeyModifiers.Shift);
        if (additive)
        {
            if (!_selectedNodeIds.Add(nodeId))
            {
                _selectedNodeIds.Remove(nodeId);
            }
        }
        else if (!_selectedNodeIds.Contains(nodeId))
        {
            _selectedNodeIds.Clear();
            _selectedNodeIds.Add(nodeId);
        }

        UpdateSelectionVisuals();
        NotifySelectionStateChanged();
    }

    private IReadOnlyList<CanvasNodeBounds> GetNodeBounds(
        IReadOnlyDictionary<Guid, Point>? positions = null) =>
        _nodes
            .Where(pair => positions is null || positions.ContainsKey(pair.Key))
            .Select(pair =>
            {
                var point = positions is null ? _positions[pair.Key] : positions[pair.Key];
                return new CanvasNodeBounds(
                    pair.Key,
                    point.X,
                    point.Y,
                    pair.Value.Width,
                    pair.Value.Height);
            })
            .ToArray();

    private void MoveNodesFromOrigins(
        IReadOnlyDictionary<Guid, Point> origins,
        double deltaX,
        double deltaY)
    {
        foreach (var (id, origin) in origins)
        {
            if (!_nodes.TryGetValue(id, out var selectedNode))
            {
                continue;
            }

            var point = new Point(origin.X + deltaX, origin.Y + deltaY);
            Place(selectedNode, point);
            _positions[id] = point;
            UpdateConnections(selectedNode);
        }

        RenderMiniMap();
    }

    private void RestoreLayout()
    {
        _viewportSaveTimer.Stop();
        _isRestoringLayout = true;
        if (_viewModel?.CanvasLayout is not { } layout)
        {
            _positions.Clear();
            Dispatcher.UIThread.Post(
                () => _isRestoringLayout = false,
                DispatcherPriority.Loaded);
            return;
        }

        _positions.Clear();
        foreach (var node in layout.Nodes)
        {
            _positions[node.EntityId] = new Point(node.X, node.Y);
        }

        Dispatcher.UIThread.Post(
            () => _isRestoringLayout = false,
            DispatcherPriority.Loaded);
    }

    private void RestoreViewState()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewportSaveTimer.Stop();
        _isRestoringLayout = true;
        SetZoom(_viewModel.CanvasViewState.Zoom);
        Dispatcher.UIThread.Post(
            () =>
            {
                Viewport.Offset = new Vector(
                    _viewModel.CanvasViewState.HorizontalOffset,
                    _viewModel.CanvasViewState.VerticalOffset);
                _isRestoringLayout = false;
            },
            DispatcherPriority.Loaded);
    }

    private void HandleViewportScrollChanged(object? sender, ScrollChangedEventArgs eventArgs)
    {
        RenderMiniMap();
        if (_isRestoringLayout || _viewModel is null)
        {
            return;
        }

        _viewportSaveTimer.Stop();
        _viewportSaveTimer.Start();
    }

    private void HandleViewportSaveTimerTick(object? sender, EventArgs eventArgs)
    {
        _viewportSaveTimer.Stop();
        PersistViewState();
    }

    private void HandleSurfacePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        var properties = eventArgs.GetCurrentPoint(Surface).Properties;
        Focus();
        if (properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStart = eventArgs.GetPosition(Viewport);
            _panOrigin = Viewport.Offset;
            Surface.Cursor = new Cursor(StandardCursorType.SizeAll);
            eventArgs.Pointer.Capture(Surface);
            eventArgs.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        var additive = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _selectionBeforeBox = additive
            ? _selectedNodeIds.ToHashSet()
            : new HashSet<Guid>();
        if (!additive)
        {
            _selectedNodeIds.Clear();
        }

        _isBoxSelecting = true;
        _boxSelectionStart = eventArgs.GetPosition(Surface);
        _boxSelectionVisual = new Rectangle
        {
            Fill = Brush.Parse("#3365D2EF"),
            Stroke = Brush.Parse("#8BE6F7"),
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
        };
        Place(_boxSelectionVisual, _boxSelectionStart);
        Surface.Children.Add(_boxSelectionVisual);
        eventArgs.Pointer.Capture(Surface);
        UpdateSelectionVisuals();
        NotifySelectionStateChanged();
        eventArgs.Handled = true;
    }

    private void HandleSurfacePointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (_isPanning)
        {
            var current = eventArgs.GetPosition(Viewport);
            var delta = current - _panStart;
            Viewport.Offset = new Vector(
                Math.Max(0, _panOrigin.X - delta.X),
                Math.Max(0, _panOrigin.Y - delta.Y));
            eventArgs.Handled = true;
            return;
        }

        if (!_isBoxSelecting || _boxSelectionVisual is null)
        {
            return;
        }

        var currentSelectionPoint = eventArgs.GetPosition(Surface);
        var selection = new CanvasSelectionBounds(
            _boxSelectionStart.X,
            _boxSelectionStart.Y,
            currentSelectionPoint.X,
            currentSelectionPoint.Y);
        Canvas.SetLeft(_boxSelectionVisual, selection.Left);
        Canvas.SetTop(_boxSelectionVisual, selection.Top);
        _boxSelectionVisual.Width = selection.Right - selection.Left;
        _boxSelectionVisual.Height = selection.Bottom - selection.Top;

        _selectedNodeIds.Clear();
        _selectedNodeIds.UnionWith(_selectionBeforeBox);
        _selectedNodeIds.UnionWith(
            CanvasSelectionService.SelectIntersecting(GetNodeBounds(), selection));
        UpdateSelectionVisuals();
        NotifySelectionStateChanged();
        eventArgs.Handled = true;
    }

    private void HandleSurfacePointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (_isPanning)
        {
            _isPanning = false;
            Surface.Cursor = Cursor.Default;
            eventArgs.Pointer.Capture(null);
            PersistViewState();
            eventArgs.Handled = true;
            return;
        }

        if (!_isBoxSelecting)
        {
            return;
        }

        _isBoxSelecting = false;
        if (_boxSelectionVisual is not null)
        {
            Surface.Children.Remove(_boxSelectionVisual);
            _boxSelectionVisual = null;
        }

        eventArgs.Pointer.Capture(null);
        _selectionBeforeBox = new HashSet<Guid>();
        eventArgs.Handled = true;
    }

    private void HandleSurfacePointerWheelChanged(object? sender, PointerWheelEventArgs eventArgs)
    {
        if (!eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        SetZoom(_zoom + Math.Sign(eventArgs.Delta.Y) * 0.1, persist: true);
        eventArgs.Handled = true;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var control = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (control && eventArgs.Key == Key.S)
        {
            SaveLayout();
        }
        else if (control
                 && eventArgs.Key == Key.Z
                 && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            RedoLayout();
        }
        else if (control && eventArgs.Key == Key.Z)
        {
            UndoLayout();
        }
        else if (control && eventArgs.Key == Key.Y)
        {
            RedoLayout();
        }
        else if (control && eventArgs.Key == Key.D0)
        {
            FitView();
        }
        else if (control && eventArgs.Key is Key.Add or Key.OemPlus)
        {
            ZoomIn();
        }
        else if (control && eventArgs.Key is Key.Subtract or Key.OemMinus)
        {
            ZoomOut();
        }
        else if (control && eventArgs.Key == Key.A)
        {
            _selectedNodeIds.Clear();
            _selectedNodeIds.UnionWith(_nodes.Keys);
            UpdateSelectionVisuals();
            NotifySelectionStateChanged();
        }
        else if (!control && eventArgs.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var distance = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
            var delta = eventArgs.Key switch
            {
                Key.Left => new Vector(-distance, 0),
                Key.Right => new Vector(distance, 0),
                Key.Up => new Vector(0, -distance),
                _ => new Vector(0, distance),
            };
            MoveSelectedNodes(delta);
        }
        else if (eventArgs.Key == Key.Escape)
        {
            _selectedNodeIds.Clear();
            UpdateSelectionVisuals();
            NotifySelectionStateChanged();
        }
        else
        {
            return;
        }

        eventArgs.Handled = true;
    }

    private void MoveSelectedNodes(Vector requestedDelta)
    {
        if (_viewModel?.CanMutateWorkspace != true || _selectedNodeIds.Count == 0)
        {
            return;
        }

        var previous = CaptureLayout();
        var origins = _selectedNodeIds
            .Where(id => _nodes.ContainsKey(id) && _positions.ContainsKey(id))
            .ToDictionary(id => id, id => _positions[id]);
        var selectedBounds = GetNodeBounds(origins);
        var constrained = CanvasSelectionService.ConstrainMove(
            selectedBounds,
            requestedDelta.X,
            requestedDelta.Y,
            Surface.Width,
            Surface.Height);
        MoveNodesFromOrigins(origins, constrained.DeltaX, constrained.DeltaY);
        if (_layoutHistory.Record(previous, CaptureLayout()))
        {
            NotifyHistoryStateChanged();
            PersistLayout();
        }
    }

    private void RenderAlignmentGuides()
    {
        ClearAlignmentGuides();
        var allBounds = GetNodeBounds();
        var moving = allBounds
            .Where(node => _selectedNodeIds.Contains(node.EntityId))
            .ToArray();
        var stationary = allBounds
            .Where(node => !_selectedNodeIds.Contains(node.EntityId))
            .ToArray();
        var guides = CanvasSelectionService.FindAlignmentGuides(moving, stationary);
        foreach (var x in guides.Vertical)
        {
            AddAlignmentGuide(new Point(x, 0), new Point(x, Surface.Height));
        }

        foreach (var y in guides.Horizontal)
        {
            AddAlignmentGuide(new Point(0, y), new Point(Surface.Width, y));
        }
    }

    private void AddAlignmentGuide(Point start, Point end)
    {
        var line = new Line
        {
            StartPoint = start,
            EndPoint = end,
            Stroke = Brush.Parse("#F2C66D"),
            StrokeThickness = 1,
            StrokeDashArray = [5, 4],
            IsHitTestVisible = false,
        };
        _alignmentGuideVisuals.Add(line);
        Surface.Children.Add(line);
    }

    private void ClearAlignmentGuides()
    {
        foreach (var line in _alignmentGuideVisuals)
        {
            Surface.Children.Remove(line);
        }

        _alignmentGuideVisuals.Clear();
    }

    private void RenderMiniMap()
    {
        if (MiniMapSurface is null || Surface is null)
        {
            return;
        }

        MiniMapSurface.Children.Clear();
        if (Surface.Width <= 0 || Surface.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(MiniMapSurface.Width / Surface.Width, MiniMapSurface.Height / Surface.Height);
        foreach (var (id, node) in _nodes)
        {
            if (!_positions.TryGetValue(id, out var point))
            {
                continue;
            }

            var tag = node.Tag as NodeTag;
            var preview = new Rectangle
            {
                Width = Math.Max(3, node.Width * scale),
                Height = Math.Max(2, node.Height * scale),
                Fill = Brush.Parse(
                    _selectedNodeIds.Contains(id)
                        ? "#F2C66D"
                        : tag?.Type switch
                        {
                            "project" => "#65D2EF",
                            "version" => "#4EA9CC",
                            _ => "#5DD6B2",
                        }),
                IsHitTestVisible = false,
            };
            Place(preview, new Point(point.X * scale, point.Y * scale));
            MiniMapSurface.Children.Add(preview);
        }

        var viewport = new Rectangle
        {
            Width = Math.Clamp(Viewport.Viewport.Width / _zoom * scale, 2, MiniMapSurface.Width),
            Height = Math.Clamp(Viewport.Viewport.Height / _zoom * scale, 2, MiniMapSurface.Height),
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Fill = Brush.Parse("#16FFFFFF"),
            IsHitTestVisible = false,
        };
        Place(
            viewport,
            new Point(
                Math.Clamp(Viewport.Offset.X / _zoom * scale, 0, MiniMapSurface.Width - viewport.Width),
                Math.Clamp(Viewport.Offset.Y / _zoom * scale, 0, MiniMapSurface.Height - viewport.Height)));
        MiniMapSurface.Children.Add(viewport);
    }

    private void PersistLayout()
    {
        if (_isRestoringLayout || _viewModel?.CanMutateWorkspace != true)
        {
            return;
        }

        var nodes = new List<CanvasNodeLayoutEdit>();
        AddLayoutNode(nodes, "project", _viewModel.CurrentProject.ProjectId);
        foreach (var version in _viewModel.Versions)
        {
            AddLayoutNode(nodes, "version", version.VersionId);
            foreach (var item in version.Items)
            {
                AddLayoutNode(nodes, "item", item.ItemId);
            }
        }

        _isPersistingLayout = true;
        try
        {
            _viewModel.SaveCanvasLayoutCommand.Execute(
                new CanvasLayoutEditRequest(nodes));
        }
        finally
        {
            _isPersistingLayout = false;
        }
    }

    private IReadOnlyList<CanvasNodeLayoutEdit> CaptureLayout()
    {
        if (_viewModel is null)
        {
            return [];
        }

        var nodes = new List<CanvasNodeLayoutEdit>();
        AddLayoutNode(nodes, "project", _viewModel.CurrentProject.ProjectId);
        foreach (var version in _viewModel.Versions)
        {
            AddLayoutNode(nodes, "version", version.VersionId);
            foreach (var item in version.Items)
            {
                AddLayoutNode(nodes, "item", item.ItemId);
            }
        }

        return nodes;
    }

    private void ApplyLayout(IReadOnlyList<CanvasNodeLayoutEdit> layout)
    {
        _positions.Clear();
        foreach (var node in layout)
        {
            _positions[node.EntityId] = new Point(node.X, node.Y);
        }

        RenderGraph();
    }

    private void ClearLayoutHistory()
    {
        _layoutHistory.Clear();
        NotifyHistoryStateChanged();
    }

    private void NotifyHistoryStateChanged() =>
        HistoryStateChanged?.Invoke(this, EventArgs.Empty);

    private void NotifySelectionStateChanged() =>
        SelectionStateChanged?.Invoke(this, EventArgs.Empty);

    private void PersistViewState()
    {
        if (_isRestoringLayout || _viewModel is null)
        {
            return;
        }

        _viewModel.SaveCanvasViewStateCommand.Execute(
            new CanvasViewState(
                _zoom,
                Math.Max(0, Viewport.Offset.X),
                Math.Max(0, Viewport.Offset.Y)));
    }

    private void AddLayoutNode(
        ICollection<CanvasNodeLayoutEdit> nodes,
        string nodeType,
        Guid entityId)
    {
        if (entityId == Guid.Empty || !_positions.TryGetValue(entityId, out var point))
        {
            return;
        }

        nodes.Add(new CanvasNodeLayoutEdit(nodeType, entityId, point.X, point.Y));
    }

    private void AddConnection(Control source, Control target, string color, double thickness)
    {
        _connections.Add(
            new ConnectionVisual(
                source,
                target,
                new Line
                {
                    Stroke = Brush.Parse(color),
                    StrokeThickness = thickness,
                    Opacity = 0.85,
                    IsHitTestVisible = false,
                }));
    }

    private void UpdateConnections(Control node)
    {
        foreach (var connection in _connections.Where(connection => connection.Source == node || connection.Target == node))
        {
            UpdateConnection(connection);
        }
    }

    private static void UpdateConnection(ConnectionVisual connection)
    {
        connection.Line.StartPoint = new Point(
            Canvas.GetLeft(connection.Source) + connection.Source.Bounds.Width,
            Canvas.GetTop(connection.Source) + connection.Source.Bounds.Height / 2);
        connection.Line.EndPoint = new Point(
            Canvas.GetLeft(connection.Target),
            Canvas.GetTop(connection.Target) + connection.Target.Bounds.Height / 2);
    }

    private Point GetPosition(Guid id, Point fallback)
    {
        if (_positions.TryGetValue(id, out var position))
        {
            return position;
        }

        _positions[id] = fallback;
        return fallback;
    }

    private static void Place(Control control, Point point)
    {
        Canvas.SetLeft(control, point.X);
        Canvas.SetTop(control, point.Y);
    }

    private sealed record ConnectionVisual(Control Source, Control Target, Line Line);

    private sealed record NodeTag(string Type, Guid? Id);
}
