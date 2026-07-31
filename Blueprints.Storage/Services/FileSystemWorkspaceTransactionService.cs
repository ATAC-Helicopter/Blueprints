using System.Text.Json;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Models;

namespace Blueprints.Storage.Services;

public sealed class FileSystemWorkspaceTransactionService : IWorkspaceTransactionService
{
    private const int MarkerSchemaVersion = 1;
    private const long MaximumMarkerBytes = 32 * 1024;
    private static readonly JsonSerializerOptions MarkerSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly Action<WorkspaceTransactionPhase>? _checkpoint;

    public FileSystemWorkspaceTransactionService(
        Action<WorkspaceTransactionPhase>? checkpoint = null)
    {
        _checkpoint = checkpoint;
    }

    public void Recover(string workspaceRoot)
    {
        var paths = ResolvePaths(workspaceRoot);
        if (!File.Exists(paths.MarkerPath))
        {
            return;
        }

        var marker = ReadAndValidateMarker(paths);
        if (!Directory.Exists(paths.WorkspaceRoot))
        {
            if (Directory.Exists(marker.BackupRoot))
            {
                Directory.Move(marker.BackupRoot, paths.WorkspaceRoot);
            }
            else if (Directory.Exists(marker.StagingRoot)
                     && string.Equals(marker.State, "promoting", StringComparison.Ordinal))
            {
                Directory.Move(marker.StagingRoot, paths.WorkspaceRoot);
            }
        }

        DeleteDirectoryIfPresent(marker.StagingRoot);
        DeleteDirectoryIfPresent(marker.BackupRoot);
        File.Delete(paths.MarkerPath);
    }

    public void Execute(string workspaceRoot, Action<string> writeToStagedWorkspace)
    {
        ArgumentNullException.ThrowIfNull(writeToStagedWorkspace);
        var paths = ResolvePaths(workspaceRoot);
        Recover(paths.WorkspaceRoot);

        if (Directory.Exists(paths.StagingRoot)
            || Directory.Exists(paths.BackupRoot)
            || File.Exists(paths.MarkerPath)
            || File.Exists(paths.MarkerPath + ".tmp"))
        {
            throw new InvalidOperationException(
                "Untracked workspace transaction artifacts already exist. Move them aside and inspect their contents before retrying.");
        }

        if (Directory.Exists(paths.WorkspaceRoot))
        {
            CopyDirectory(paths.WorkspaceRoot, paths.StagingRoot);
        }
        else
        {
            Directory.CreateDirectory(paths.StagingRoot);
        }

        try
        {
            writeToStagedWorkspace(paths.StagingRoot);
            WriteMarker(paths, "staged");
            _checkpoint?.Invoke(WorkspaceTransactionPhase.Staged);

            if (Directory.Exists(paths.WorkspaceRoot))
            {
                Directory.Move(paths.WorkspaceRoot, paths.BackupRoot);
                WriteMarker(paths, "backedUp");
                _checkpoint?.Invoke(WorkspaceTransactionPhase.OriginalBackedUp);
            }

            WriteMarker(paths, "promoting");
            Directory.Move(paths.StagingRoot, paths.WorkspaceRoot);
            WriteMarker(paths, "committed");
            _checkpoint?.Invoke(WorkspaceTransactionPhase.Promoted);

            DeleteDirectoryIfPresent(paths.BackupRoot);
            File.Delete(paths.MarkerPath);
        }
        catch
        {
            RestorePreviousWorkspace(paths);
            throw;
        }
    }

    private static void RestorePreviousWorkspace(TransactionPaths paths)
    {
        if (Directory.Exists(paths.BackupRoot))
        {
            if (Directory.Exists(paths.WorkspaceRoot))
            {
                DeleteDirectoryIfPresent(paths.WorkspaceRoot);
            }

            Directory.Move(paths.BackupRoot, paths.WorkspaceRoot);
        }

        DeleteDirectoryIfPresent(paths.StagingRoot);
        if (File.Exists(paths.MarkerPath))
        {
            File.Delete(paths.MarkerPath);
        }
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        var source = new DirectoryInfo(sourceRoot);
        if (source.LinkTarget is not null)
        {
            throw new InvalidOperationException(
                "A workspace root cannot be a symbolic link during an atomic update.");
        }

        Directory.CreateDirectory(destinationRoot);
        foreach (var entry in source.EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null)
            {
                throw new InvalidOperationException(
                    $"Workspace transactions do not follow linked entries: {entry.FullName}");
            }

            var destination = Path.Combine(destinationRoot, entry.Name);
            if (entry is DirectoryInfo directory)
            {
                CopyDirectory(directory.FullName, destination);
            }
            else if (entry is FileInfo file)
            {
                file.CopyTo(destination, overwrite: false);
            }
        }
    }

    private static TransactionMarker ReadAndValidateMarker(TransactionPaths paths)
    {
        var markerInfo = new FileInfo(paths.MarkerPath);
        if (markerInfo.Length > MaximumMarkerBytes)
        {
            throw new InvalidOperationException("The workspace transaction marker is too large.");
        }

        var marker = JsonSerializer.Deserialize<TransactionMarker>(
            File.ReadAllText(paths.MarkerPath),
            MarkerSerializerOptions)
            ?? throw new InvalidOperationException("The workspace transaction marker is invalid.");

        if (marker.SchemaVersion != MarkerSchemaVersion
            || !PathsEqual(marker.WorkspaceRoot, paths.WorkspaceRoot)
            || !PathsEqual(marker.StagingRoot, paths.StagingRoot)
            || !PathsEqual(marker.BackupRoot, paths.BackupRoot))
        {
            throw new InvalidOperationException(
                "The workspace transaction marker does not match the requested workspace.");
        }

        return marker;
    }

    private static void WriteMarker(TransactionPaths paths, string state)
    {
        var marker = new TransactionMarker(
            MarkerSchemaVersion,
            paths.WorkspaceRoot,
            paths.StagingRoot,
            paths.BackupRoot,
            state,
            DateTimeOffset.UtcNow);
        var temporaryPath = paths.MarkerPath + ".tmp";
        if (File.Exists(temporaryPath))
        {
            throw new InvalidOperationException(
                "A temporary workspace transaction marker already exists.");
        }

        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(marker, MarkerSerializerOptions));
        File.Move(temporaryPath, paths.MarkerPath, overwrite: true);
    }

    private static TransactionPaths ResolvePaths(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var fullRoot = Path.GetFullPath(workspaceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(fullRoot)
            ?? throw new InvalidOperationException("The workspace must have a parent directory.");
        var name = Path.GetFileName(fullRoot);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("The workspace path must name a directory.");
        }

        Directory.CreateDirectory(parent);
        return new TransactionPaths(
            fullRoot,
            Path.Combine(parent, $".{name}.blueprints-staging"),
            Path.Combine(parent, $".{name}.blueprints-backup"),
            Path.Combine(parent, $".{name}.blueprints-transaction.json"));
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed record TransactionPaths(
        string WorkspaceRoot,
        string StagingRoot,
        string BackupRoot,
        string MarkerPath);

    private sealed record TransactionMarker(
        int SchemaVersion,
        string WorkspaceRoot,
        string StagingRoot,
        string BackupRoot,
        string State,
        DateTimeOffset UpdatedUtc);
}
