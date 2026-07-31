using Blueprints.Core.Enums;

namespace Blueprints.Core.Models;

public sealed record ItemDocument(
    int SchemaVersion,
    Guid ProjectId,
    Guid VersionId,
    Guid ItemId,
    string ItemKey,
    string ItemKeyTypeId,
    string CategoryId,
    string Title,
    string? Description,
    bool IsDone,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    Guid LastModifiedByUserId,
    string LastModifiedByName,
    WorkItemLifecycle? WorkflowState = null)
{
    public WorkItemLifecycle EffectiveWorkflowState =>
        IsDone ? WorkItemLifecycle.Complete : WorkflowState ?? WorkItemLifecycle.Planned;
}
