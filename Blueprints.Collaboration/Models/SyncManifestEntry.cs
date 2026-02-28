namespace Blueprints.Collaboration.Models;

public sealed record SyncManifestEntry(
    string DocumentPath,
    string DocumentHash,
    string SignaturePath,
    string SignatureHash);
