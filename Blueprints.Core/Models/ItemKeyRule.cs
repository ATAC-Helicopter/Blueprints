using Blueprints.Core.Enums;

namespace Blueprints.Core.Models;

public sealed record ItemKeyRule(
    string Prefix,
    ItemKeyScope Scope);
