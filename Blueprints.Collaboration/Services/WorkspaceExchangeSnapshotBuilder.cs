using System.Security.Cryptography;
using Blueprints.Collaboration.Models;

namespace Blueprints.Collaboration.Services;

public sealed class WorkspaceExchangeSnapshotBuilder
{
    public IReadOnlyList<SyncManifestEntry> Build(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var entries = new List<SyncManifestEntry>();

        foreach (var documentPath in EnumerateDocumentPaths(workspaceRoot))
        {
            var signaturePath = Path.ChangeExtension(documentPath, ".sig");
            if (!File.Exists(signaturePath))
            {
                throw new FileNotFoundException("Detached signature was not found for an exchangeable document.", signaturePath);
            }

            entries.Add(new SyncManifestEntry(
                Path.GetRelativePath(workspaceRoot, documentPath).Replace('\\', '/'),
                ComputeHash(documentPath),
                Path.GetRelativePath(workspaceRoot, signaturePath).Replace('\\', '/'),
                ComputeHash(signaturePath)));
        }

        return entries;
    }

    private static IEnumerable<string> EnumerateDocumentPaths(string workspaceRoot)
    {
        foreach (var relativeDirectory in new[] { "project", "versions", "log" })
        {
            var absoluteDirectory = Path.Combine(workspaceRoot, relativeDirectory);
            if (!Directory.Exists(absoluteDirectory))
            {
                continue;
            }

            foreach (var documentPath in Directory.EnumerateFiles(absoluteDirectory, "*.json", SearchOption.AllDirectories)
                         .OrderBy(static path => path, StringComparer.Ordinal))
            {
                if (string.Equals(Path.GetFileName(documentPath), "settings.local.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return documentPath;
            }
        }
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
