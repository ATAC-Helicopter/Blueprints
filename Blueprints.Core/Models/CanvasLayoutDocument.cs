namespace Blueprints.Core.Models;

public sealed record CanvasLayoutDocument(
    int SchemaVersion,
    Guid ProjectId,
    int Revision,
    IReadOnlyList<CanvasNodePosition> Nodes,
    DateTimeOffset UpdatedUtc,
    Guid LastModifiedByUserId,
    string LastModifiedByName);
