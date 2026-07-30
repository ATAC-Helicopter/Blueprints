namespace Blueprints.App.Models;

public sealed record ConflictFieldComparison(
    string Field,
    string LocalValue,
    string SharedValue,
    bool IsDifferent)
{
    public string Status => IsDifferent ? "CHANGED" : "SAME";
}
