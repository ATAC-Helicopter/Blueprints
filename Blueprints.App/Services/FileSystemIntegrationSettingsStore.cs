using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class FileSystemIntegrationSettingsStore : IIntegrationSettingsStore
{
    public const long MaximumSettingsBytes = 1024 * 1024;

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

        try
        {
            if (new FileInfo(_settingsPath).Length > MaximumSettingsBytes)
            {
                return IntegrationSettings.Empty;
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<IntegrationSettings>(json, SerializerOptions)
                ?? IntegrationSettings.Empty;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return IntegrationSettings.Empty;
        }
    }

    public void Save(IntegrationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var fullPath = Path.GetFullPath(_settingsPath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
