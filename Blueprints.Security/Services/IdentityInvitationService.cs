using System.Text;
using System.Text.Json;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;

namespace Blueprints.Security.Services;

public sealed class IdentityInvitationService
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

    public IdentityInvitationService(ISignatureService signatureService)
    {
        _signatureService = signatureService;
    }

    public string Write(string filePath, StoredIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(identity);

        var payload = new IdentityInvitationPayload(
            CurrentSchemaVersion,
            identity.Profile.UserId,
            identity.Profile.DisplayName,
            identity.Profile.KeyId,
            identity.Profile.PublicKeyBase64,
            identity.Profile.CreatedUtc);
        var proof = _signatureService.Sign(
            SerializePayload(payload),
            identity.SigningKey);
        WriteAtomically(
            filePath,
            JsonSerializer.Serialize(
                new IdentityInvitationFile(payload, proof),
                SerializerOptions));
        return Path.GetFullPath(filePath);
    }

    public IdentityInvitationPayload Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var invitation = JsonSerializer.Deserialize<IdentityInvitationFile>(
            File.ReadAllText(filePath, Encoding.UTF8),
            SerializerOptions)
            ?? throw new InvalidOperationException("Identity invitation could not be read.");
        var payload = invitation.Identity;
        if (payload.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported identity invitation schema {payload.SchemaVersion}.");
        }

        if (payload.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(payload.DisplayName) ||
            string.IsNullOrWhiteSpace(payload.KeyId))
        {
            throw new InvalidOperationException("Identity invitation is missing required identity fields.");
        }

        try
        {
            var publicKey = new SignaturePublicKey(
                payload.KeyId,
                Convert.FromBase64String(payload.PublicKeyBase64));
            if (!_signatureService.Verify(SerializePayload(payload), invitation.Proof, publicKey))
            {
                throw new InvalidOperationException(
                    "Identity invitation proof is invalid.");
            }
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Identity invitation contains invalid key or proof data.",
                exception);
        }

        return payload;
    }

    private static byte[] SerializePayload(IdentityInvitationPayload payload) =>
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
