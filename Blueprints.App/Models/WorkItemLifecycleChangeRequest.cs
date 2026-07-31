using Blueprints.Core.Enums;

namespace Blueprints.App.Models;

public sealed record WorkItemLifecycleChangeRequest(
    Guid VersionId,
    Guid ItemId,
    WorkItemLifecycle WorkflowState);
