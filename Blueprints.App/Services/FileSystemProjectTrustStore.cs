using System.Text;
using System.Text.Json;
using Blueprints.App.Models;
using Blueprints.Core.Models;
using Blueprints.Security.Models;

namespace Blueprints.App.Services;

public sealed class FileSystemProjectTrustStore
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public IReadOnlyDictionary<string, SignaturePublicKey> LoadKeys(
        string localWorkspaceRoot,
        StoredIdentity currentIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(currentIdentity);

        var keys = new Dictionary<string, SignaturePublicKey>(StringComparer.Ordinal)
        {
            [currentIdentity.PublicKey.KeyId] = currentIdentity.PublicKey,
        };
        var path = GetTrustPath(localWorkspaceRoot);
        if (!File.Exists(path))
        {
            return keys;
        }

        var document = JsonSerializer.Deserialize<ProjectTrustDocument>(
            File.ReadAllText(path, Encoding.UTF8),
            SerializerOptions)
            ?? throw new InvalidOperationException("Project trust anchors could not be read.");
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported project trust-anchor schema {document.SchemaVersion}.");
        }

        foreach (var key in document.Keys)
        {
            try
            {
                keys[key.KeyId] = new SignaturePublicKey(
                    key.KeyId,
                    Convert.FromBase64String(key.PublicKeyBase64));
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"Trusted public key {key.KeyId} is not valid Base64.",
                    exception);
            }
        }

        return keys;
    }

    public IReadOnlyDictionary<string, SignaturePublicKey> LoadActiveContributorKeys(
        string localWorkspaceRoot,
        StoredIdentity currentIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(currentIdentity);

        var document = LoadDocument(localWorkspaceRoot);
        if (document is null)
        {
            return new Dictionary<string, SignaturePublicKey>(StringComparer.Ordinal)
            {
                [currentIdentity.PublicKey.KeyId] = currentIdentity.PublicKey,
            };
        }

        var keys = new Dictionary<string, SignaturePublicKey>(StringComparer.Ordinal);
        foreach (var key in document.Keys.Where(static key =>
                     key.IsActive &&
                     key.Role is not Blueprints.Core.Enums.MemberRole.Viewer))
        {
            try
            {
                keys[key.KeyId] = new SignaturePublicKey(
                    key.KeyId,
                    Convert.FromBase64String(key.PublicKeyBase64));
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"Trusted public key {key.KeyId} is not valid Base64.",
                    exception);
            }
        }

        return keys;
    }

    public void Initialize(
        string localWorkspaceRoot,
        Guid projectId,
        IEnumerable<TrustedProjectKey> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorkspaceRoot);
        ArgumentNullException.ThrowIfNull(keys);

        var existing = LoadDocument(localWorkspaceRoot);
        if (existing is not null && existing.ProjectId != projectId)
        {
            throw new InvalidOperationException(
                "The local trust-anchor file belongs to a different project.");
        }

        var merged = (existing?.Keys ?? [])
            .Concat(keys)
            .GroupBy(static key => key.KeyId, StringComparer.Ordinal)
            .Select(static group => group.Last())
            .OrderBy(static key => key.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static key => key.KeyId, StringComparer.Ordinal)
            .ToArray();
        Write(
            localWorkspaceRoot,
            new ProjectTrustDocument(
                CurrentSchemaVersion,
                projectId,
                merged,
                DateTimeOffset.UtcNow));
    }

    public void MergeVerifiedMembers(
        string localWorkspaceRoot,
        Guid projectId,
        IEnumerable<ProjectMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var trustedUtc = DateTimeOffset.UtcNow;
        Initialize(
            localWorkspaceRoot,
            projectId,
            members.Select(member => new TrustedProjectKey(
                member.UserId,
                member.DisplayName,
                ResolveKeyId(member),
                member.PublicKey,
                trustedUtc,
                member.Role,
                member.IsActive)));
    }

    private static string ResolveKeyId(ProjectMember member) =>
        string.IsNullOrWhiteSpace(member.KeyId)
            ? member.UserId.ToString("N")
            : member.KeyId;

    private static ProjectTrustDocument? LoadDocument(string localWorkspaceRoot)
    {
        var path = GetTrustPath(localWorkspaceRoot);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ProjectTrustDocument>(
                File.ReadAllText(path, Encoding.UTF8),
                SerializerOptions)
            : null;
    }

    private static void Write(
        string localWorkspaceRoot,
        ProjectTrustDocument document)
    {
        var path = GetTrustPath(localWorkspaceRoot);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Project trust-anchor path has no parent.");
        Directory.CreateDirectory(directory);
        var tempPath = path + ".tmp";
        File.WriteAllText(
            tempPath,
            JsonSerializer.Serialize(document, SerializerOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempPath, path, overwrite: true);
    }

    private static string GetTrustPath(string localWorkspaceRoot) =>
        Path.Combine(localWorkspaceRoot, ".blueprints", "trusted-project-keys.json");
}
