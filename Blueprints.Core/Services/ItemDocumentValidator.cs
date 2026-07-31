using Blueprints.Core.Enums;
using Blueprints.Core.Models;

namespace Blueprints.Core.Services;

public static class ItemDocumentValidator
{
    public static void Validate(ItemDocument item, Guid projectId, Guid versionId)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Item schema {item.SchemaVersion} is not supported.");
        }
        if (item.ProjectId != projectId || item.VersionId != versionId ||
            item.ItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Item identity does not match its workspace location.");
        }
        if (item.WorkflowState is WorkItemLifecycle state && !Enum.IsDefined(state))
        {
            throw new InvalidOperationException("Item workflow state is not supported.");
        }
        if (item.WorkflowState == WorkItemLifecycle.Complete && !item.IsDone)
        {
            throw new InvalidOperationException("A Complete item must also be marked done.");
        }
        if (item.IsDone &&
            item.WorkflowState is not null and not WorkItemLifecycle.Complete)
        {
            throw new InvalidOperationException("A done item cannot use an incomplete workflow state.");
        }
    }
}
