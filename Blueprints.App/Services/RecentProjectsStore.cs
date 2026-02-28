using System.Text;
using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class RecentProjectsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _recentProjectsPath;

    public RecentProjectsStore(string recentProjectsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recentProjectsPath);

        _recentProjectsPath = recentProjectsPath;
    }

    public IReadOnlyList<RecentProjectReference> Load()
    {
        if (!File.Exists(_recentProjectsPath))
        {
            return [];
        }

        var json = File.ReadAllText(_recentProjectsPath, Encoding.UTF8);
        var results = JsonSerializer.Deserialize<IReadOnlyList<RecentProjectReference>>(json, SerializerOptions);
        return results ?? [];
    }

    public void AddOrUpdate(RecentProjectReference project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var projects = Load()
            .Where(existing =>
                !string.Equals(existing.LocalWorkspaceRoot, project.LocalWorkspaceRoot, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.SharedWorkspaceRoot, project.SharedWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
            .Prepend(project)
            .Take(10)
            .ToArray();

        var directory = Path.GetDirectoryName(_recentProjectsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            _recentProjectsPath,
            JsonSerializer.Serialize(projects, SerializerOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
