using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class FileSystemIntegrationSettingsStore : IIntegrationSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public FileSystemIntegrationSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    public IntegrationSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return IntegrationSettings.Empty;
        }

        var json = File.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<IntegrationSettings>(json, SerializerOptions)
            ?? IntegrationSettings.Empty;
    }

    public void Save(IntegrationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
