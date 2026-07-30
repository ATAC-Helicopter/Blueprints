namespace Blueprints.App.Models;

public sealed record ApprovedSourceImportRequest(
    IReadOnlyList<ApprovedSourceImportItem> Items);
