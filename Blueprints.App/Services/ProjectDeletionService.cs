using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class ProjectDeletionService
{
    private const long MaximumIdentityDocumentBytes = 4 * 1024 * 1024;
    private static readonly string[] LocalBlueprintsEntries =
        ["project", "versions", "log", "sync", "exports", ".blueprints"];
    private static readonly string[] SharedBlueprintsEntries =
        ["project", "versions", "log", "manifest", "packs"];

    private readonly RecentProjectsStore _recentProjectsStore;

    public ProjectDeletionService(RecentProjectsStore recentProjectsStore)
    {
        _recentProjectsStore = recentProjectsStore;
    }

    public ProjectDeletionResult Delete(RecentProjectReference project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var localRoot = ValidateRoot(project.LocalWorkspaceRoot, "local workspace");
        var sharedRoot = ValidateRoot(project.SharedWorkspaceRoot, "shared workspace");
        EnsureRootsDoNotOverlap(localRoot, sharedRoot);
        EnsureOutsideGitWorktree(localRoot, "local workspace");
        EnsureOutsideGitWorktree(sharedRoot, "shared workspace");

        var projectId = ReadAndValidateLocalIdentity(localRoot, project);
        ValidateSharedIdentity(sharedRoot, projectId);

        DeleteKnownEntries(localRoot, LocalBlueprintsEntries);
        DeleteKnownEntries(sharedRoot, SharedBlueprintsEntries);
        var localRootRemoved = DeleteRootIfEmpty(localRoot);
        var sharedRootRemoved = DeleteRootIfEmpty(sharedRoot);
        _recentProjectsStore.Remove(project);

        return new ProjectDeletionResult(project.Name, localRootRemoved, sharedRootRemoved);
    }

    private static string ValidateRoot(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var filesystemRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullPath)!);
        var userRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

        if (PathsEqual(fullPath, filesystemRoot) || PathsEqual(fullPath, userRoot))
        {
            throw new InvalidOperationException($"The {label} path is too broad to delete safely.");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The {label} no longer exists: {fullPath}");
        }

        if (new DirectoryInfo(fullPath).LinkTarget is not null)
        {
            throw new InvalidOperationException($"The {label} cannot be a symbolic link.");
        }

        return fullPath;
    }

    private static void EnsureRootsDoNotOverlap(string localRoot, string sharedRoot)
    {
        if (PathsEqual(localRoot, sharedRoot)
            || IsWithin(localRoot, sharedRoot)
            || IsWithin(sharedRoot, localRoot))
        {
            throw new InvalidOperationException(
                "The local and shared Blueprints workspaces overlap, so deletion was stopped.");
        }
    }

    private static void EnsureOutsideGitWorktree(string path, string label)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                throw new InvalidOperationException(
                    $"The {label} is inside a Git worktree. Blueprints will not delete files there.");
            }
        }
    }

    private static Guid ReadAndValidateLocalIdentity(
        string localRoot,
        RecentProjectReference project)
    {
        var identity = ReadIdentity(Path.Combine(localRoot, "project", "project.json"));
        if (!string.Equals(identity.Name, project.Name, StringComparison.Ordinal)
            || !string.Equals(identity.ProjectCode, project.ProjectCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The local workspace identity does not match the selected recent project. Nothing was deleted.");
        }

        return identity.ProjectId;
    }

    private static void ValidateSharedIdentity(string sharedRoot, Guid expectedProjectId)
    {
        var projectPath = Path.Combine(sharedRoot, "project", "project.json");
        var manifestPath = Path.Combine(sharedRoot, "manifest", "sync-manifest.json");
        if (File.Exists(projectPath))
        {
            if (ReadIdentity(projectPath).ProjectId != expectedProjectId)
            {
                throw new InvalidOperationException(
                    "The shared workspace belongs to a different project. Nothing was deleted.");
            }

            return;
        }

        if (File.Exists(manifestPath))
        {
            if (ReadProjectId(manifestPath) != expectedProjectId)
            {
                throw new InvalidOperationException(
                    "The shared workspace manifest belongs to a different project. Nothing was deleted.");
            }

            return;
        }

        if (SharedBlueprintsEntries.Any(name =>
                Directory.Exists(Path.Combine(sharedRoot, name))
                || File.Exists(Path.Combine(sharedRoot, name))))
        {
            throw new InvalidOperationException(
                "The shared workspace has content but no matching Blueprints project identity. Nothing was deleted.");
        }
    }

    private static (Guid ProjectId, string Name, string ProjectCode) ReadIdentity(string path)
    {
        using var document = ReadBoundedJson(path);
        var root = document.RootElement;
        if (!root.TryGetProperty("projectId", out var idElement)
            || !idElement.TryGetGuid(out var projectId)
            || projectId == Guid.Empty
            || !root.TryGetProperty("name", out var nameElement)
            || !root.TryGetProperty("projectCode", out var codeElement))
        {
            throw new InvalidOperationException(
                "The Blueprints project identity document is invalid. Nothing was deleted.");
        }

        return (projectId, nameElement.GetString() ?? string.Empty, codeElement.GetString() ?? string.Empty);
    }

    private static Guid ReadProjectId(string path)
    {
        using var document = ReadBoundedJson(path);
        if (!document.RootElement.TryGetProperty("projectId", out var idElement)
            || !idElement.TryGetGuid(out var projectId)
            || projectId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The Blueprints shared manifest identity is invalid. Nothing was deleted.");
        }

        return projectId;
    }

    private static JsonDocument ReadBoundedJson(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new InvalidOperationException(
                "The selected folder is not a Blueprints project workspace. Nothing was deleted.");
        }

        if (info.Length > MaximumIdentityDocumentBytes)
        {
            throw new InvalidOperationException(
                "The Blueprints identity document is too large to validate safely. Nothing was deleted.");
        }

        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
    }

    private static void DeleteKnownEntries(string root, IEnumerable<string> entryNames)
    {
        foreach (var name in entryNames)
        {
            var path = Path.Combine(root, name);
            if (Directory.Exists(path))
            {
                DeleteDirectoryWithoutFollowingLinks(path);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void DeleteDirectoryWithoutFollowingLinks(string path)
    {
        var directory = new DirectoryInfo(path);
        if (directory.LinkTarget is not null)
        {
            directory.Delete();
            return;
        }

        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo childDirectory)
            {
                DeleteDirectoryWithoutFollowingLinks(childDirectory.FullName);
            }
            else
            {
                entry.Delete();
            }
        }

        directory.Delete();
    }

    private static bool DeleteRootIfEmpty(string root)
    {
        if (Directory.EnumerateFileSystemEntries(root).Any())
        {
            return false;
        }

        Directory.Delete(root);
        return true;
    }

    private static bool IsWithin(string path, string possibleParent)
    {
        var relative = Path.GetRelativePath(possibleParent, path);
        return relative != "."
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
}
