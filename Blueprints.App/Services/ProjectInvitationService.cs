using System.Text;
using System.Text.Json;
using Blueprints.App.Models;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;

namespace Blueprints.App.Services;

public sealed class ProjectInvitationService
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
    private readonly ISignatureService _signatureService;

    public ProjectInvitationService(ISignatureService signatureService)
    {
        _signatureService = signatureService;
    }

    public string Write(
        string filePath,
        ProjectInvitationPayload payload,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(signingKey);

        var normalized = payload with
        {
            SchemaVersion = CurrentSchemaVersion,
            CreatedUtc = DateTimeOffset.UtcNow,
        };
        var proof = _signatureService.Sign(
            SerializePayload(normalized),
            signingKey);
        WriteAtomically(
            filePath,
            JsonSerializer.Serialize(
                new ProjectInvitationFile(normalized, proof),
                SerializerOptions));
        return Path.GetFullPath(filePath);
    }

    public ProjectInvitationPayload Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var invitation = JsonSerializer.Deserialize<ProjectInvitationFile>(
            File.ReadAllText(filePath, Encoding.UTF8),
            SerializerOptions)
            ?? throw new InvalidOperationException("Project invitation could not be read.");
        var payload = invitation.Project;
        if (payload.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported project invitation schema {payload.SchemaVersion}.");
        }

        if (payload.ProjectId == Guid.Empty ||
            payload.InvitedUserId == Guid.Empty ||
            payload.InviterUserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.InvitedKeyId) ||
            string.IsNullOrWhiteSpace(payload.InviterKeyId))
        {
            throw new InvalidOperationException("Project invitation is missing required trust fields.");
        }

        if (payload.TrustedKeys.Count == 0 || payload.TrustedKeys.Count > 1000)
        {
            throw new InvalidOperationException("Project invitation has an invalid trusted-key count.");
        }

        if (payload.TrustedKeys
            .GroupBy(static key => key.KeyId, StringComparer.Ordinal)
            .Any(static group => group.Count() > 1))
        {
            throw new InvalidOperationException("Project invitation contains duplicate key IDs.");
        }

        try
        {
            var inviterKey = new SignaturePublicKey(
                payload.InviterKeyId,
                Convert.FromBase64String(payload.InviterPublicKeyBase64));
            if (!_signatureService.Verify(
                    SerializePayload(payload),
                    invitation.Proof,
                    inviterKey))
            {
                throw new InvalidOperationException("Project invitation proof is invalid.");
            }

            foreach (var trustedKey in payload.TrustedKeys)
            {
                _ = Convert.FromBase64String(trustedKey.PublicKeyBase64);
            }

            if (!payload.TrustedKeys.Any(key =>
                    string.Equals(key.KeyId, payload.InviterKeyId, StringComparison.Ordinal) &&
                    string.Equals(
                        key.PublicKeyBase64,
                        payload.InviterPublicKeyBase64,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Project invitation does not include its signer in the trusted-key set.");
            }
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Project invitation contains invalid key or proof data.",
                exception);
        }

        return payload;
    }

    private static byte[] SerializePayload(ProjectInvitationPayload payload) =>
        JsonSerializer.SerializeToUtf8Bytes(payload, PayloadSerializerOptions);

    private static void WriteAtomically(string filePath, string json)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Invitation path has no parent.");
        Directory.CreateDirectory(directory);
        var tempPath = fullPath + ".tmp";
        File.WriteAllText(
            tempPath,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempPath, fullPath, overwrite: true);
    }
}
