using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class FileSystemCanvasViewStateStore
{
    private const double MinimumZoom = 0.25;
    private const double MaximumZoom = 2.5;
    private const double MaximumOffset = 100_000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public CanvasViewState Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var path = GetPath(workspaceRoot);
        if (!File.Exists(path))
        {
            return CanvasViewState.Default;
        }

        try
        {
            var state = JsonSerializer.Deserialize<CanvasViewState>(
                File.ReadAllText(path),
                SerializerOptions);
            return state is null ? CanvasViewState.Default : Validate(state);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException)
        {
            return CanvasViewState.Default;
        }
    }

    public void Save(string workspaceRoot, CanvasViewState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(state);

        var validated = Validate(state);
        var path = GetPath(workspaceRoot);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(validated, SerializerOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static CanvasViewState Validate(CanvasViewState state)
    {
        ValidateRange(state.Zoom, MinimumZoom, MaximumZoom, "Canvas zoom");
        ValidateRange(state.HorizontalOffset, 0, MaximumOffset, "Canvas horizontal offset");
        ValidateRange(state.VerticalOffset, 0, MaximumOffset, "Canvas vertical offset");
        return state;
    }

    private static void ValidateRange(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{name} must be between {minimum} and {maximum}.");
        }
    }

    private static string GetPath(string workspaceRoot) =>
        Path.Combine(workspaceRoot, ".blueprints", "canvas-view.json");
}
