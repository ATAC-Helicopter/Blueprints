using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class FileSystemVaultSyncStatusReader : IVaultSyncStatusReader
{
    public const string MetadataFileName = "vaultsync.meta.db";
    public const string StatusFileName = "blueprints.status.json";
    public const long MaximumStatusDocumentBytes = 1024 * 1024;
    private const int MaximumWarnings = 32;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16,
    };

    public VaultSyncStatusSummary Inspect(string configuredRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredRoot);

        string normalizedRoot;
        try
        {
            normalizedRoot = Path.GetFullPath(configuredRoot.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Missing(configuredRoot.Trim(), $"The configured VaultSync path is invalid: {exception.Message}");
        }

        var metadataPath = ResolveMetadataPath(normalizedRoot);
        if (metadataPath is null)
        {
            return Missing(
                normalizedRoot,
                $"No {MetadataFileName} store was found below the configured location.");
        }

        DateTimeOffset metadataWriteUtc;
        try
        {
            metadataWriteUtc = File.GetLastWriteTimeUtc(metadataPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return Invalid(
                metadataPath,
                string.Empty,
                null,
                $"VaultSync metadata could not be inspected: {exception.Message}");
        }

        var statusPath = Path.Combine(
            Path.GetDirectoryName(metadataPath) ?? normalizedRoot,
            StatusFileName);
        if (!File.Exists(statusPath))
        {
            return new VaultSyncStatusSummary(
                true,
                metadataPath,
                statusPath,
                metadataWriteUtc,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                null,
                "Unknown",
                0,
                [$"{StatusFileName} is not available; detailed backup health was not inferred from SQLite."],
                "VaultSync metadata detected; detailed backup health is unavailable.");
        }

        try
        {
            var statusInfo = new FileInfo(statusPath);
            if (statusInfo.Length > MaximumStatusDocumentBytes)
            {
                return Invalid(
                    metadataPath,
                    statusPath,
                    metadataWriteUtc,
                    $"The VaultSync health document exceeds the {MaximumStatusDocumentBytes / 1024} KiB read limit.");
            }

            using var stream = File.OpenRead(statusPath);
            var document = JsonSerializer.Deserialize<VaultSyncHealthDocument>(
                stream,
                SerializerOptions);
            if (document is null || document.SchemaVersion != 1)
            {
                return Invalid(
                    metadataPath,
                    statusPath,
                    metadataWriteUtc,
                    "The VaultSync health document must use schemaVersion 1.");
            }

            var warnings = (document.Warnings ?? [])
                .Where(static warning => !string.IsNullOrWhiteSpace(warning))
                .Select(static warning => warning.Trim())
                .ToList();
            if (document.DestinationReachable is not true)
            {
                warnings.Add("The VaultSync destination is not reported as reachable.");
            }

            if (document.BackupIndexConsistent is not true)
            {
                warnings.Add("The VaultSync backup index is not reported as consistent.");
            }

            if (document.LatestBackupUtc is null)
            {
                warnings.Add("VaultSync did not report a latest backup.");
            }

            if (document.LatestSnapshotUtc is null)
            {
                warnings.Add("VaultSync did not report a latest snapshot.");
            }

            if (document.LatestVerificationUtc is null)
            {
                warnings.Add("VaultSync did not report a latest verification.");
            }

            if (!IsReady(document.RestoreReadiness))
            {
                warnings.Add($"VaultSync restore readiness is {Bounded(document.RestoreReadiness, "Unknown")}.");
            }

            if (document.MetadataConflictCount > 0)
            {
                warnings.Add($"{document.MetadataConflictCount} VaultSync metadata conflicts require review.");
            }

            var boundedWarnings = warnings
                .Distinct(StringComparer.Ordinal)
                .Take(MaximumWarnings)
                .ToArray();

            return new VaultSyncStatusSummary(
                true,
                metadataPath,
                statusPath,
                metadataWriteUtc,
                Bounded(document.ProjectExternalId),
                Bounded(document.ProjectName),
                Bounded(document.DestinationAlias),
                document.DestinationReachable,
                document.LatestSnapshotUtc,
                document.LatestBackupUtc,
                document.LatestVerificationUtc,
                document.BackupIndexConsistent,
                Bounded(document.RestoreReadiness, "Unknown"),
                Math.Max(0, document.MetadataConflictCount),
                boundedWarnings,
                BuildSummary(document));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Invalid(
                metadataPath,
                statusPath,
                metadataWriteUtc,
                $"VaultSync health could not be read: {exception.Message}");
        }
    }

    private static string? ResolveMetadataPath(string configuredRoot)
    {
        if (File.Exists(configuredRoot))
        {
            return string.Equals(
                Path.GetFileName(configuredRoot),
                MetadataFileName,
                StringComparison.OrdinalIgnoreCase)
                ? configuredRoot
                : null;
        }

        if (!Directory.Exists(configuredRoot))
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(configuredRoot, ".vaultsync", "meta", MetadataFileName),
            Path.Combine(configuredRoot, "meta", MetadataFileName),
            Path.Combine(configuredRoot, MetadataFileName),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static VaultSyncStatusSummary Missing(string target, string summary) =>
        new(
            false,
            target,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            "Unavailable",
            0,
            [summary],
            summary);

    private static VaultSyncStatusSummary Invalid(
        string metadataPath,
        string statusPath,
        DateTimeOffset? metadataWriteUtc,
        string summary) =>
        new(
            true,
            metadataPath,
            statusPath,
            metadataWriteUtc,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            "Unavailable",
            0,
            [summary],
            summary);

    private static string BuildSummary(VaultSyncHealthDocument document)
    {
        var readiness = Bounded(document.RestoreReadiness, "Unknown");
        var snapshot = document.LatestSnapshotUtc is null
            ? "no snapshot reported"
            : $"latest snapshot {document.LatestSnapshotUtc:yyyy-MM-dd HH:mm} UTC";
        var backup = document.LatestBackupUtc is null
            ? "no backup reported"
            : $"latest backup {document.LatestBackupUtc:yyyy-MM-dd HH:mm} UTC";
        var verification = document.LatestVerificationUtc is null
            ? "no verification reported"
            : $"verified {document.LatestVerificationUtc:yyyy-MM-dd HH:mm} UTC";
        return $"Restore readiness: {readiness}. {snapshot}; {backup}; {verification}.";
    }

    private static bool IsReady(string? restoreReadiness) =>
        restoreReadiness?.Trim() is { } readiness &&
        (readiness.Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
         readiness.Equals("Healthy", StringComparison.OrdinalIgnoreCase));

    private static string Bounded(string? value, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, 256)];
    }

    private sealed record VaultSyncHealthDocument
    {
        public int SchemaVersion { get; init; }

        public string? ProjectExternalId { get; init; }

        public string? ProjectName { get; init; }

        public string? DestinationAlias { get; init; }

        public bool? DestinationReachable { get; init; }

        public DateTimeOffset? LatestSnapshotUtc { get; init; }

        public DateTimeOffset? LatestBackupUtc { get; init; }

        public DateTimeOffset? LatestVerificationUtc { get; init; }

        public bool? BackupIndexConsistent { get; init; }

        public string? RestoreReadiness { get; init; }

        public int MetadataConflictCount { get; init; }

        public IReadOnlyList<string>? Warnings { get; init; }
    }
}
