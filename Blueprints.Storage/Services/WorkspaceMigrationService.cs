using System.IO.Compression;
using Blueprints.Security.Models;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Models;

namespace Blueprints.Storage.Services;

public sealed class WorkspaceMigrationService
{
    public const int MinimumSupportedSchemaVersion = 1;
    public const int CurrentSchemaVersion = 1;

    private readonly IWorkspaceTransactionService _transactionService;
    private readonly IReadOnlyDictionary<int, IWorkspaceMigration> _migrations;
    private readonly int _targetSchemaVersion;

    public WorkspaceMigrationService(
        IWorkspaceTransactionService transactionService,
        IEnumerable<IWorkspaceMigration>? migrations = null,
        int targetSchemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(transactionService);
        if (targetSchemaVersion < MinimumSupportedSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSchemaVersion));
        }

        _transactionService = transactionService;
        _targetSchemaVersion = targetSchemaVersion;
        _migrations = (migrations ?? [])
            .ToDictionary(static migration => migration.SourceSchemaVersion);
        ValidateMigrationChain();
    }

    public WorkspaceMigrationResult MigrateIfNeeded(
        string workspaceRoot,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(signingKey);
        _transactionService.Recover(workspaceRoot);

        var originalVersion = WorkspaceSchemaInspector.ReadSchemaVersion(workspaceRoot);
        if (originalVersion > _targetSchemaVersion)
        {
            throw new InvalidOperationException(
                $"This workspace uses schema {originalVersion}, but this Blueprints version supports up to schema {_targetSchemaVersion}. Update Blueprints before opening it.");
        }

        if (originalVersion < MinimumSupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Workspace schema {originalVersion} is no longer supported.");
        }

        if (originalVersion == _targetSchemaVersion)
        {
            return new WorkspaceMigrationResult(
                originalVersion,
                originalVersion,
                [],
                null);
        }

        var backupPath = CreateBackup(workspaceRoot, originalVersion);
        var appliedVersions = new List<int>();
        try
        {
            _transactionService.Execute(workspaceRoot, stagedRoot =>
            {
                var currentVersion = originalVersion;
                while (currentVersion < _targetSchemaVersion)
                {
                    if (!_migrations.TryGetValue(currentVersion, out var migration))
                    {
                        throw new InvalidOperationException(
                            $"No migration is available from workspace schema {currentVersion}.");
                    }

                    migration.Apply(stagedRoot, signingKey);
                    var migratedVersion = WorkspaceSchemaInspector.ReadSchemaVersion(stagedRoot);
                    if (migratedVersion != migration.TargetSchemaVersion)
                    {
                        throw new InvalidOperationException(
                            $"Migration from schema {currentVersion} produced schema {migratedVersion} instead of {migration.TargetSchemaVersion}.");
                    }

                    currentVersion = migratedVersion;
                    appliedVersions.Add(currentVersion);
                }
            });
        }
        catch
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            throw;
        }

        return new WorkspaceMigrationResult(
            originalVersion,
            _targetSchemaVersion,
            appliedVersions.ToArray(),
            backupPath);
    }

    private string CreateBackup(string workspaceRoot, int schemaVersion)
    {
        var fullRoot = Path.GetFullPath(workspaceRoot);
        var parent = Path.GetDirectoryName(fullRoot)
            ?? throw new InvalidOperationException("The workspace must have a parent directory.");
        var workspaceName = Path.GetFileName(fullRoot);
        var backupRoot = Path.Combine(
            parent,
            $".{workspaceName}.blueprints-migration-backups");
        if (Directory.Exists(backupRoot)
            && new DirectoryInfo(backupRoot).LinkTarget is not null)
        {
            throw new InvalidOperationException(
                "The migration backup directory cannot be a symbolic link.");
        }

        Directory.CreateDirectory(backupRoot);
        var backupPath = Path.Combine(
            backupRoot,
            $"schema-{schemaVersion}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}.zip");
        ZipFile.CreateFromDirectory(
            fullRoot,
            backupPath,
            CompressionLevel.Optimal,
            includeBaseDirectory: true);
        return backupPath;
    }

    private void ValidateMigrationChain()
    {
        foreach (var migration in _migrations.Values)
        {
            if (migration.SourceSchemaVersion < MinimumSupportedSchemaVersion
                || migration.TargetSchemaVersion != migration.SourceSchemaVersion + 1
                || migration.TargetSchemaVersion > _targetSchemaVersion)
            {
                throw new InvalidOperationException(
                    "Workspace migrations must advance exactly one supported schema version.");
            }
        }

        for (var version = MinimumSupportedSchemaVersion;
             version < _targetSchemaVersion;
             version++)
        {
            if (!_migrations.ContainsKey(version))
            {
                throw new InvalidOperationException(
                    $"The workspace migration chain is missing schema {version}.");
            }
        }
    }
}
