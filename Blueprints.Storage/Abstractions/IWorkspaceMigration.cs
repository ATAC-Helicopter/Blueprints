using Blueprints.Security.Models;

namespace Blueprints.Storage.Abstractions;

public interface IWorkspaceMigration
{
    int SourceSchemaVersion { get; }

    int TargetSchemaVersion { get; }

    void Apply(string stagedWorkspaceRoot, SignatureKeyMaterial signingKey);
}
