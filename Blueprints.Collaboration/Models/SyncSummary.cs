using Blueprints.Collaboration.Enums;

namespace Blueprints.Collaboration.Models;

public sealed record SyncSummary(
    SyncHealth Health,
    int PendingOutgoingChanges,
    int PendingIncomingChanges,
    int ConflictCount,
    int LastPulledManifestVersion = 0,
    int LastPushedManifestVersion = 0,
    DateTimeOffset? LastSuccessfulTrustValidationUtc = null);
