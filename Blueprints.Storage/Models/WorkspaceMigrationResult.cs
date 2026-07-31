namespace Blueprints.Storage.Models;

public sealed record WorkspaceMigrationResult(
    int OriginalSchemaVersion,
    int CurrentSchemaVersion,
    IReadOnlyList<int> AppliedSchemaVersions,
    string? BackupPath)
{
    public bool WasMigrated => AppliedSchemaVersions.Count > 0;
}
