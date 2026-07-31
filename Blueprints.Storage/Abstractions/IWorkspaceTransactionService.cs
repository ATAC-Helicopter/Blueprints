namespace Blueprints.Storage.Abstractions;

public interface IWorkspaceTransactionService
{
    void Recover(string workspaceRoot);

    void Execute(string workspaceRoot, Action<string> writeToStagedWorkspace);
}
