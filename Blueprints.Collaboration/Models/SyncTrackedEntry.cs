namespace Blueprints.Collaboration.Models;

public sealed record SyncTrackedEntry(
    string DocumentPath,
    string DocumentHash,
    string SignatureHash);
