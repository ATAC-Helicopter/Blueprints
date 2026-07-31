using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Core.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class ItemDocumentValidatorTests
{
    [Fact]
    public void Validate_AllowsLegacyItemWithoutWorkflowState()
    {
        var item = CreateItem(null, false);

        ItemDocumentValidator.Validate(item, item.ProjectId, item.VersionId);

        Assert.Equal(WorkItemLifecycle.Planned, item.EffectiveWorkflowState);
    }

    [Fact]
    public void Deserialize_LegacyItemWithoutWorkflowState_RemainsCompatible()
    {
        var projectId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var json = $$"""
            {
              "schemaVersion":1,
              "projectId":"{{projectId}}",
              "versionId":"{{versionId}}",
              "itemId":"{{itemId}}",
              "itemKey":"BP-1",
              "itemKeyTypeId":"feature",
              "categoryId":"added",
              "title":"Legacy",
              "description":null,
              "isDone":true,
              "tags":[],
              "createdUtc":"2026-01-01T00:00:00+00:00",
              "updatedUtc":"2026-01-01T00:00:00+00:00",
              "lastModifiedByUserId":"{{userId}}",
              "lastModifiedByName":"Tester"
            }
            """;

        var item = new CanonicalJsonSerializer().Deserialize<ItemDocument>(json);

        Assert.Null(item.WorkflowState);
        Assert.Equal(WorkItemLifecycle.Complete, item.EffectiveWorkflowState);
        ItemDocumentValidator.Validate(item, projectId, versionId);
    }

    [Fact]
    public void Validate_RejectsInvalidOrContradictoryWorkflowState()
    {
        var invalid = CreateItem((WorkItemLifecycle)99, false);
        var contradictory = CreateItem(WorkItemLifecycle.Complete, false);

        Assert.Throws<InvalidOperationException>(
            () => ItemDocumentValidator.Validate(invalid, invalid.ProjectId, invalid.VersionId));
        Assert.Throws<InvalidOperationException>(
            () => ItemDocumentValidator.Validate(
                contradictory,
                contradictory.ProjectId,
                contradictory.VersionId));
    }

    private static ItemDocument CreateItem(WorkItemLifecycle? state, bool isDone)
    {
        var now = DateTimeOffset.UtcNow;
        return new ItemDocument(
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BP-1",
            "feature",
            "added",
            "Test",
            null,
            isDone,
            [],
            now,
            now,
            Guid.NewGuid(),
            "Tester",
            state);
    }
}
