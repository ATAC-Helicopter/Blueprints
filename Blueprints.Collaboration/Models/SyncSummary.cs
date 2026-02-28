using Blueprints.Collaboration.Enums;

namespace Blueprints.Collaboration.Models;

public sealed record SyncSummary(
    SyncHealth Health,
    int PendingOutgoingChanges,
    int PendingIncomingChanges,
    int ConflictCount);
