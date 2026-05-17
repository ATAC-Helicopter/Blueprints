using System.Security.Cryptography;
using System.Text;
using Blueprints.Collaboration.Models;
using Blueprints.Security.Models;
using Blueprints.Storage.Abstractions;

namespace Blueprints.Collaboration.Services;

public sealed class FileSystemAuditLogService
{
    private const int CurrentSchemaVersion = 1;
    private readonly ISignedDocumentStore _signedDocumentStore;

    public FileSystemAuditLogService(ISignedDocumentStore signedDocumentStore)
    {
        _signedDocumentStore = signedDocumentStore;
    }

    public AuditLogEntry Append(
        string workspaceRoot,
        Guid projectId,
        string operation,
        string summary,
        Guid authorUserId,
        string authorDisplayName,
        int membershipRevisionSeen,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorDisplayName);
        ArgumentNullException.ThrowIfNull(signingKey);

        var timestampUtc = DateTimeOffset.UtcNow;
        var changeId = $"{timestampUtc:yyyyMMddTHHmmssfffZ}_{authorUserId:N}_{Guid.NewGuid():N}";
        var entry = new AuditLogEntry(
            CurrentSchemaVersion,
            changeId,
            projectId,
            operation,
            summary,
            timestampUtc,
            authorUserId,
            authorDisplayName,
            membershipRevisionSeen,
            GetLatestEntryHash(workspaceRoot));

        _signedDocumentStore.Write(GetEntryPath(workspaceRoot, changeId), entry, signingKey);
        return entry;
    }

    public AuditLogValidationResult Validate(string workspaceRoot, SignaturePublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(publicKey);

        var entries = new List<(string Path, AuditLogEntry Entry, string Hash)>();
        var invalidPaths = new List<string>();
        var logRoot = GetLogRoot(workspaceRoot);

        if (!Directory.Exists(logRoot))
        {
            return new AuditLogValidationResult(true, 0, null, [], "No audit entries exist yet.");
        }

        foreach (var entryPath in Directory.EnumerateFiles(logRoot, "*.json").OrderBy(static path => path, StringComparer.Ordinal))
        {
            try
            {
                var read = _signedDocumentStore.Read<AuditLogEntry>(entryPath, publicKey);
                if (!read.IsSignatureValid)
                {
                    invalidPaths.Add(ToRelativePath(workspaceRoot, entryPath));
                    continue;
                }

                entries.Add((entryPath, read.Document, ComputeHash(entryPath)));
            }
            catch (Exception exception) when (exception is FileNotFoundException or InvalidOperationException)
            {
                invalidPaths.Add(ToRelativePath(workspaceRoot, entryPath));
            }
        }

        var orderedEntries = entries
            .OrderBy(static entry => entry.Entry.TimestampUtc)
            .ThenBy(static entry => entry.Entry.ChangeId, StringComparer.Ordinal)
            .ToArray();

        string? previousHash = null;
        foreach (var entry in orderedEntries)
        {
            if (!string.Equals(entry.Entry.PreviousEntryHash, previousHash, StringComparison.Ordinal))
            {
                invalidPaths.Add(ToRelativePath(workspaceRoot, entry.Path));
            }

            previousHash = entry.Hash;
        }

        var distinctInvalidPaths = invalidPaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        var isValid = distinctInvalidPaths.Length == 0;
        return new AuditLogValidationResult(
            isValid,
            orderedEntries.Length,
            previousHash,
            distinctInvalidPaths,
            isValid
                ? $"Validated {orderedEntries.Length} audit entries."
                : $"Audit log validation failed for {distinctInvalidPaths.Length} entries.");
    }

    private static string? GetLatestEntryHash(string workspaceRoot)
    {
        var logRoot = GetLogRoot(workspaceRoot);
        if (!Directory.Exists(logRoot))
        {
            return null;
        }

        var latestEntryPath = Directory.EnumerateFiles(logRoot, "*.json")
            .OrderBy(static path => path, StringComparer.Ordinal)
            .LastOrDefault();
        return latestEntryPath is null ? null : ComputeHash(latestEntryPath);
    }

    private static string GetEntryPath(string workspaceRoot, string changeId) =>
        Path.Combine(GetLogRoot(workspaceRoot), $"{changeId}.json");

    private static string GetLogRoot(string workspaceRoot) =>
        Path.Combine(workspaceRoot, "log");

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string ToRelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
