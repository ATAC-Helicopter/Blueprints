using System.Text.Json;
using Blueprints.App.Models;

namespace Blueprints.App.Services;

public sealed class FileSystemVaultSyncExchangeRootAdapter : IVaultSyncExchangeRootAdapter
{
    public const string RegistrationMarkerFileName = ".blueprints-exchange.json";
    public const long MaximumRegistrationMarkerBytes = 64 * 1024;
    public static readonly TimeSpan MaximumApprovalLifetime = TimeSpan.FromMinutes(10);
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly IVaultSyncStatusReader _statusReader;
    private readonly HashSet<Guid> _consumedApprovals = [];
    private readonly Lock _approvalLock = new();

    public FileSystemVaultSyncExchangeRootAdapter()
        : this(new FileSystemVaultSyncStatusReader())
    {
    }

    public FileSystemVaultSyncExchangeRootAdapter(IVaultSyncStatusReader statusReader)
    {
        _statusReader = statusReader;
    }

    public VaultSyncExchangeRootIntent PrepareIntent(
        string configuredMetadataRoot,
        Guid projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredMetadataRoot);
        if (projectId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A real Blueprints project is required before registering an exchange root.");
        }

        var status = _statusReader.Inspect(configuredMetadataRoot);
        if (!status.MetadataStoreFound)
        {
            throw new InvalidOperationException(
                "VaultSync metadata must be detected before registering an exchange root.");
        }

        var metadataFile = new FileInfo(Path.GetFullPath(status.MetadataStorePath));
        var metadataDirectory = metadataFile.Directory;
        var vaultSyncDirectory = metadataDirectory?.Parent;
        var destinationDirectory = vaultSyncDirectory?.Parent;
        if (metadataDirectory is null ||
            vaultSyncDirectory is null ||
            destinationDirectory is null ||
            !metadataDirectory.Name.Equals("meta", StringComparison.OrdinalIgnoreCase) ||
            !vaultSyncDirectory.Name.Equals(".vaultsync", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Exchange registration requires metadata at <destination>/.vaultsync/meta/vaultsync.meta.db.");
        }

        var destinationRoot = Path.GetFullPath(destinationDirectory.FullName);
        var exchangeRoot = Path.GetFullPath(
            Path.Combine(
                destinationRoot,
                ".blueprints",
                "projects",
                projectId.ToString("D").ToLowerInvariant()));
        EnsureContained(destinationRoot, exchangeRoot);

        return new VaultSyncExchangeRootIntent(
            projectId,
            destinationRoot,
            exchangeRoot);
    }

    public VaultSyncExchangeRootApproval Approve(
        VaultSyncExchangeRootIntent intent,
        DateTimeOffset nowUtc)
    {
        ValidateIntent(intent);
        return new VaultSyncExchangeRootApproval(
            Guid.NewGuid(),
            intent,
            nowUtc,
            nowUtc.Add(MaximumApprovalLifetime));
    }

    public VaultSyncExchangeRootRegistration Register(
        VaultSyncExchangeRootIntent intent,
        VaultSyncExchangeRootApproval approval,
        DateTimeOffset nowUtc)
    {
        ValidateIntent(intent);
        Authorize(intent, approval, nowUtc);

        var blueprintsRoot = Path.Combine(intent.DestinationRoot, ".blueprints");
        var projectsRoot = Path.Combine(blueprintsRoot, "projects");
        RejectReparsePoint(blueprintsRoot);
        RejectReparsePoint(projectsRoot);
        RejectReparsePoint(intent.ExchangeRoot);

        var markerPath = Path.Combine(
            intent.ExchangeRoot,
            RegistrationMarkerFileName);
        if (File.Exists(markerPath))
        {
            ValidateExistingMarker(markerPath, intent);
            return new VaultSyncExchangeRootRegistration(
                intent.ExchangeRoot,
                markerPath,
                true);
        }

        if (Directory.Exists(intent.ExchangeRoot) &&
            Directory.EnumerateFileSystemEntries(intent.ExchangeRoot).Any())
        {
            throw new InvalidOperationException(
                "The intended VaultSync exchange root already contains unregistered content.");
        }

        var createdExchangeRoot = !Directory.Exists(intent.ExchangeRoot);
        Directory.CreateDirectory(intent.ExchangeRoot);
        try
        {
            WriteMarkerAtomically(
                markerPath,
                new RegistrationMarker(
                    1,
                    intent.ProjectId,
                    nowUtc));
        }
        catch
        {
            if (createdExchangeRoot &&
                Directory.Exists(intent.ExchangeRoot) &&
                !Directory.EnumerateFileSystemEntries(intent.ExchangeRoot).Any())
            {
                Directory.Delete(intent.ExchangeRoot);
            }

            throw;
        }

        return new VaultSyncExchangeRootRegistration(
            intent.ExchangeRoot,
            markerPath,
            false);
    }

    private void Authorize(
        VaultSyncExchangeRootIntent intent,
        VaultSyncExchangeRootApproval approval,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.ApprovalId == Guid.Empty ||
            approval.Intent != intent ||
            approval.ApprovedUtc > nowUtc ||
            approval.ExpiresUtc <= nowUtc ||
            approval.ExpiresUtc - approval.ApprovedUtc > MaximumApprovalLifetime)
        {
            throw new InvalidOperationException(
                "VaultSync exchange-root approval is invalid, expired, or does not match the exact project and destination.");
        }

        lock (_approvalLock)
        {
            if (!_consumedApprovals.Add(approval.ApprovalId))
            {
                throw new InvalidOperationException(
                    "VaultSync exchange-root approval has already been consumed.");
            }
        }
    }

    private static void ValidateIntent(VaultSyncExchangeRootIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (intent.ProjectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(intent.DestinationRoot) ||
            string.IsNullOrWhiteSpace(intent.ExchangeRoot))
        {
            throw new InvalidOperationException(
                "VaultSync exchange registration requires an exact project, destination, and exchange root.");
        }

        var destinationRoot = Path.GetFullPath(intent.DestinationRoot);
        var expectedExchangeRoot = Path.GetFullPath(
            Path.Combine(
                destinationRoot,
                ".blueprints",
                "projects",
                intent.ProjectId.ToString("D").ToLowerInvariant()));
        if (!string.Equals(
                expectedExchangeRoot,
                Path.GetFullPath(intent.ExchangeRoot),
                PathComparison))
        {
            throw new InvalidOperationException(
                "The VaultSync exchange root does not match the project-specific contract.");
        }

        EnsureContained(destinationRoot, expectedExchangeRoot);
    }

    private static void ValidateExistingMarker(
        string markerPath,
        VaultSyncExchangeRootIntent intent)
    {
        try
        {
            if (new FileInfo(markerPath).Length > MaximumRegistrationMarkerBytes)
            {
                throw new InvalidOperationException(
                    "The existing VaultSync exchange marker exceeds the read limit.");
            }

            var marker = JsonSerializer.Deserialize<RegistrationMarker>(
                File.ReadAllText(markerPath),
                SerializerOptions);
            if (marker is null ||
                marker.SchemaVersion != 1 ||
                marker.ProjectId != intent.ProjectId)
            {
                throw new InvalidOperationException(
                    "The existing VaultSync exchange marker does not match this project.");
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The existing VaultSync exchange marker is malformed.",
                exception);
        }
    }

    private static void WriteMarkerAtomically(
        string markerPath,
        RegistrationMarker marker)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(markerPath)!,
            $".{Path.GetFileName(markerPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(marker, SerializerOptions));
            File.Move(temporaryPath, markerPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if (Directory.Exists(path) &&
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException(
                $"VaultSync exchange registration refuses the linked directory: {path}");
        }
    }

    private static void EnsureContained(string parentPath, string childPath)
    {
        var parentPrefix = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(parentPath)) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(childPath).StartsWith(
                parentPrefix,
                PathComparison))
        {
            throw new InvalidOperationException(
                "The VaultSync exchange root escapes the configured destination.");
        }
    }

    private sealed record RegistrationMarker(
        int SchemaVersion,
        Guid ProjectId,
        DateTimeOffset RegisteredUtc);
}
