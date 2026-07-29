using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Blueprints.App.Models;
using Blueprints.App.ViewModels;

namespace Blueprints.App.Views.Components;

public partial class BlueprintCanvasSurface : UserControl
{
    private const double NodeWidth = 220;
    private const double VersionNodeHeight = 104;
    private const double ItemNodeHeight = 82;
    private readonly Dictionary<Guid, Point> _positions = [];
    private readonly List<ConnectionVisual> _connections = [];
    private MainWindowViewModel? _viewModel;
    private Control? _draggedNode;
    private Point _dragOffset;
    private double _zoom = 1;

    public BlueprintCanvasSurface()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachViewModel();
    }

    public void ZoomIn() => SetZoom(_zoom + 0.1);

    public void ZoomOut() => SetZoom(_zoom - 0.1);

    public void ResetView()
    {
        _positions.Clear();
        SetZoom(1);
        RenderGraph();
        Viewport.Offset = Vector.Zero;
    }

    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, 0.6, 1.5);
        ZoomHost.Width = Surface.Width * _zoom;
        ZoomHost.Height = Surface.Height * _zoom;
        Surface.RenderTransform = new ScaleTransform(_zoom, _zoom);
        Surface.RenderTransformOrigin = RelativePoint.TopLeft;
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

    private void HandleVersionsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) =>
        RenderGraph();

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(MainWindowViewModel.SelectedVersion)
            or nameof(MainWindowViewModel.SelectedItem))
        {
            UpdateSelectionVisuals();
        }
        else if (eventArgs.PropertyName is nameof(MainWindowViewModel.VersionCount)
            or nameof(MainWindowViewModel.ItemCount))
        {
            RenderGraph();
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

            var selected = tag.Type switch
            {
                "version" => _viewModel.SelectedVersion?.VersionId == id,
                "item" => _viewModel.SelectedItem?.ItemId == id,
                _ => false,
            };
            border.Background = Brush.Parse(
                selected
                    ? "#12669A"
                    : tag.Type == "version"
                        ? "#0B3D62"
                        : "#0A3656");
            border.BorderBrush = Brush.Parse(
                selected
                    ? "#FFFFFF"
                    : tag.Type == "version"
                        ? "#4EA9CC"
                        : "#397F9F");
        }
    }

    private void RenderGraph()
    {
        Surface.Children.Clear();
        _connections.Clear();
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
        var projectPoint = new Point(48, Math.Max(310, Surface.Height / 2 - 70));
        Place(projectNode, projectPoint);
        Surface.Children.Add(projectNode);

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
            AddConnection(projectNode, versionNode, "#52C7E8", 2);

            foreach (var item in version.Items)
            {
                var itemPoint = GetPosition(
                    item.ItemId,
                    new Point(700, 70 + globalItemIndex * 108));
                var itemNode = CreateItemNode(version, item);
                Place(itemNode, itemPoint);
                Surface.Children.Add(itemNode);
                AddConnection(versionNode, itemNode, item.IsDone ? "#5DD6B2" : "#73AFCF", item.IsDone ? 2 : 1.25);
                globalItemIndex++;
            }
        }

        foreach (var connection in _connections)
        {
            Surface.Children.Insert(FindGridVisualCount(), connection.Line);
            UpdateConnection(connection);
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

        return CreateNodeShell(content, 250, 140, "#0A2035", "#65D2EF", "project", null);
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
                _draggedNode = node;
                var pointer = eventArgs.GetPosition(Surface);
                _dragOffset = new Point(pointer.X - Canvas.GetLeft(node), pointer.Y - Canvas.GetTop(node));
                eventArgs.Pointer.Capture(node);
            },
            RoutingStrategies.Tunnel);
        node.PointerMoved += (_, eventArgs) =>
        {
            if (_draggedNode != node || !eventArgs.GetCurrentPoint(node).Properties.IsLeftButtonPressed)
            {
                return;
            }

            var pointer = eventArgs.GetPosition(Surface);
            var point = new Point(
                Math.Clamp(pointer.X - _dragOffset.X, 0, Surface.Width - node.Bounds.Width),
                Math.Clamp(pointer.Y - _dragOffset.Y, 0, Surface.Height - node.Bounds.Height));
            Place(node, point);
            _positions[nodeId] = point;
            UpdateConnections(node);
        };
        node.PointerReleased += (_, eventArgs) =>
        {
            if (_draggedNode == node)
            {
                _draggedNode = null;
                eventArgs.Pointer.Capture(null);
            }
        };
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
