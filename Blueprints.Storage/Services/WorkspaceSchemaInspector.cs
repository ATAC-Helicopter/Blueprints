using System.Text.Json;

namespace Blueprints.Storage.Services;

public static class WorkspaceSchemaInspector
{
    private const long MaximumProjectDocumentBytes = 4 * 1024 * 1024;

    public static int ReadSchemaVersion(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var projectPath = Path.Combine(workspaceRoot, "project", "project.json");
        var info = new FileInfo(projectPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException(
                "The workspace project document was not found.",
                projectPath);
        }

        if (info.Length > MaximumProjectDocumentBytes)
        {
            throw new InvalidOperationException(
                "The workspace project document exceeds the supported size.");
        }

        using var stream = File.OpenRead(projectPath);
        using var document = JsonDocument.Parse(
            stream,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
        if (!document.RootElement.TryGetProperty("schemaVersion", out var schemaElement)
            || !schemaElement.TryGetInt32(out var schemaVersion)
            || schemaVersion < 1)
        {
            throw new InvalidOperationException(
                "The workspace project document has an invalid schema version.");
        }

        return schemaVersion;
    }
}
