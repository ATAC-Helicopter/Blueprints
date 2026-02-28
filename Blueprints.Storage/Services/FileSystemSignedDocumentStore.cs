using System.Text;
using System.Text.Json;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Models;

namespace Blueprints.Storage.Services;

public sealed class FileSystemSignedDocumentStore : ISignedDocumentStore
{
    private static readonly JsonSerializerOptions SignatureSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ICanonicalJsonSerializer _canonicalJsonSerializer;
    private readonly ISignatureService _signatureService;

    public FileSystemSignedDocumentStore(
        ICanonicalJsonSerializer canonicalJsonSerializer,
        ISignatureService signatureService)
    {
        _canonicalJsonSerializer = canonicalJsonSerializer;
        _signatureService = signatureService;
    }

    public SignedDocumentReadResult<T> Read<T>(
        string documentPath,
        SignaturePublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        var canonicalJson = File.ReadAllText(documentPath, Encoding.UTF8);
        var document = _canonicalJsonSerializer.Deserialize<T>(canonicalJson);
        var signature = ReadSignature(GetSignaturePath(documentPath));
        var isSignatureValid = _signatureService.Verify(
            Encoding.UTF8.GetBytes(canonicalJson),
            signature,
            publicKey);

        return new SignedDocumentReadResult<T>(
            document,
            canonicalJson,
            signature,
            isSignatureValid);
    }

    public SignedDocumentWriteResult Write<T>(
        string documentPath,
        T document,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        var canonicalJson = _canonicalJsonSerializer.Serialize(document);
        var payload = Encoding.UTF8.GetBytes(canonicalJson);
        var signature = _signatureService.Sign(payload, signingKey);
        var signaturePath = GetSignaturePath(documentPath);

        EnsureParentDirectoryExists(documentPath);
        EnsureParentDirectoryExists(signaturePath);

        File.WriteAllText(documentPath, canonicalJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(
            signaturePath,
            JsonSerializer.Serialize(signature, SignatureSerializerOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new SignedDocumentWriteResult(
            documentPath,
            signaturePath,
            canonicalJson,
            signature);
    }

    private static void EnsureParentDirectoryExists(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string GetSignaturePath(string documentPath) =>
        Path.ChangeExtension(documentPath, ".sig");

    private static DetachedSignature ReadSignature(string signaturePath)
    {
        var json = File.ReadAllText(signaturePath, Encoding.UTF8);
        var signature = JsonSerializer.Deserialize<DetachedSignature>(json, SignatureSerializerOptions);
        return signature ?? throw new InvalidOperationException("Failed to deserialize detached signature.");
    }
}
