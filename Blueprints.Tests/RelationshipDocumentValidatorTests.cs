using Blueprints.Core.Models;
using Blueprints.Core.Services;

namespace Blueprints.Tests;

public sealed class RelationshipDocumentValidatorTests
{
    [Fact]
    public void Validate_AcceptsTypedDirectionalAndUndirectedRelationships()
    {
        var projectId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var document = CreateDocument(
            projectId,
            [
                new RelationshipTypeDefinition("blocks", "Blocks", null, "#E05A47", true),
                new RelationshipTypeDefinition("related", "Related", null, "#52C7E8", false),
            ],
            [
                Edge("blocks", "item", itemId, "version", versionId),
                Edge("related", "project", projectId, "item", itemId),
            ]);

        RelationshipDocumentValidator.Validate(document, projectId);
        RelationshipDocumentValidator.ValidateEntityReferences(
            document,
            projectId,
            new HashSet<Guid> { versionId },
            new HashSet<Guid> { itemId });
    }

    [Fact]
    public void Validate_RejectsUnknownTypesSelfLinksAndDuplicateUndirectedEdges()
    {
        var projectId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var type = new RelationshipTypeDefinition(
            "related",
            "Related",
            null,
            "#52C7E8",
            false);

        Assert.Throws<InvalidOperationException>(
            () => RelationshipDocumentValidator.Validate(
                CreateDocument(projectId, [type], [Edge("missing", "item", first, "item", second)]),
                projectId));
        Assert.Throws<InvalidOperationException>(
            () => RelationshipDocumentValidator.Validate(
                CreateDocument(projectId, [type], [Edge("related", "item", first, "item", first)]),
                projectId));
        Assert.Throws<InvalidOperationException>(
            () => RelationshipDocumentValidator.Validate(
                CreateDocument(
                    projectId,
                    [type],
                    [
                        Edge("related", "item", first, "item", second),
                        Edge("related", "item", second, "item", first),
                    ]),
                projectId));
    }

    [Fact]
    public void ValidateEntityReferences_RejectsEntitiesOutsideTheWorkspace()
    {
        var projectId = Guid.NewGuid();
        var document = CreateDocument(
            projectId,
            [new RelationshipTypeDefinition("blocks", "Blocks", null, "#E05A47", true)],
            [Edge("blocks", "item", Guid.NewGuid(), "project", projectId)]);

        RelationshipDocumentValidator.Validate(document, projectId);
        Assert.Throws<InvalidOperationException>(
            () => RelationshipDocumentValidator.ValidateEntityReferences(
                document,
                projectId,
                new HashSet<Guid>(),
                new HashSet<Guid>()));
    }

    [Theory]
    [InlineData("Uppercase")]
    [InlineData("contains spaces")]
    [InlineData("1starts-with-number")]
    [InlineData("this-relationship-type-id-is-far-too-long")]
    public void Validate_RejectsInvalidTypeIds(string typeId)
    {
        var projectId = Guid.NewGuid();
        var document = CreateDocument(
            projectId,
            [new RelationshipTypeDefinition(typeId, "Type", null, "#52C7E8", true)],
            []);

        Assert.Throws<InvalidOperationException>(
            () => RelationshipDocumentValidator.Validate(document, projectId));
    }

    [Fact]
    public void Validate_RejectsNullFieldsAsValidationErrors()
    {
        var projectId = Guid.NewGuid();
        var document = CreateDocument(
            projectId,
            [new RelationshipTypeDefinition(null!, "Type", null, null!, true)],
            []);

        Assert.Throws<InvalidOperationException>(
            () => RelationshipDocumentValidator.Validate(document, projectId));
    }

    private static RelationshipDocument CreateDocument(
        Guid projectId,
        IReadOnlyList<RelationshipTypeDefinition> types,
        IReadOnlyList<RelationshipEdge> relationships) =>
        new(
            1,
            projectId,
            1,
            types,
            relationships,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "Editor");

    private static RelationshipEdge Edge(
        string typeId,
        string sourceType,
        Guid sourceId,
        string targetType,
        Guid targetId) =>
        new(
            Guid.NewGuid(),
            typeId,
            new RelationshipEndpoint(sourceType, sourceId),
            new RelationshipEndpoint(targetType, targetId),
            null);
}
