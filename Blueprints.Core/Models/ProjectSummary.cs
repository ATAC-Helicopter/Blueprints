using Blueprints.Core.Enums;

namespace Blueprints.Core.Models;

public sealed record ProjectSummary(
    string Name,
    string Code,
    TrustState TrustState,
    string SharedFolderPath);
