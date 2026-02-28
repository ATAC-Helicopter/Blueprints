namespace Blueprints.Core.Models;

public sealed record SignedEntityEnvelope(
    string DocumentName,
    string Json,
    string Signature);
