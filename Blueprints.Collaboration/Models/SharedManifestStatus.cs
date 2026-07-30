namespace Blueprints.Collaboration.Models;

public sealed record SharedManifestStatus(
    bool Exists,
    bool SignatureValid,
    int? ManifestVersion,
    string BatchId,
    string Summary);
