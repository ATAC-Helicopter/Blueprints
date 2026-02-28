using System.Text;
using System.Text.Json;
using Blueprints.Collaboration.Models;

namespace Blueprints.Collaboration.Services;

public sealed class FileSystemSyncStateStore
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public SyncStateDocument Load(string localWorkspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);

        var statePath = GetStatePath(localWorkspaceRoot);
        if (!File.Exists(statePath))
        {
            return CreateDefault();
        }

        var json = File.ReadAllText(statePath, Encoding.UTF8);
        var state = JsonSerializer.Deserialize<SyncStateDocument>(json, SerializerOptions);
        return state ?? throw new InvalidOperationException("Failed to deserialize sync state.");
    }

    public void Save(string localWorkspaceRoot, SyncStateDocument state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(state);

        var statePath = GetStatePath(localWorkspaceRoot);
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            statePath,
            JsonSerializer.Serialize(state, SerializerOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public SyncStateDocument CreateDefault() =>
        new(
            CurrentSchemaVersion,
            0,
            0,
            null,
            [],
            [],
            []);

    private static string GetStatePath(string localWorkspaceRoot) =>
        Path.Combine(localWorkspaceRoot, "sync", "state.json");
}
