using Blueprints.Security.Models;
using Blueprints.Storage.Models;

namespace Blueprints.Storage.Abstractions;

public interface IProjectWorkspaceStore
{
    void Save(
        string workspaceRoot,
        ProjectWorkspaceSnapshot workspace,
        SignatureKeyMaterial signingKey);

    ProjectWorkspaceLoadResult Load(
        string workspaceRoot,
        SignaturePublicKey publicKey);
}
