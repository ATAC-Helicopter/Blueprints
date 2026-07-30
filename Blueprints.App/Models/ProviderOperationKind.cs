namespace Blueprints.App.Models;

public enum ProviderOperationKind
{
    ReadSource = 0,
    CreateIssue = 1,
    UpdateIssue = 2,
    UpdateProject = 3,
    CreateDraftRelease = 4,
    PublishRelease = 5,
}
