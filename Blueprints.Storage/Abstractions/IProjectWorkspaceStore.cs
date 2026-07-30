using Blueprints.Core.Models;
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

    ProjectWorkspaceLoadResult Load(
        string workspaceRoot,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys);

    void SaveCanvasLayout(
        string workspaceRoot,
        CanvasLayoutDocument layout,
        SignatureKeyMaterial signingKey);
}
