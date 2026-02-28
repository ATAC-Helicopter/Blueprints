namespace Blueprints.Core.Models;

public sealed record MemberDocument(
    int SchemaVersion,
    Guid ProjectId,
    int MembershipRevision,
    IReadOnlyList<ProjectMember> Members);
