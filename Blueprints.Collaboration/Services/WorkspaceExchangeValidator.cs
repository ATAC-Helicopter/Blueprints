using System.Text;
using System.Text.Json;
using Blueprints.Collaboration.Models;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;

namespace Blueprints.Collaboration.Services;

public sealed class WorkspaceExchangeValidator
{
    private static readonly JsonSerializerOptions SignatureSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ISignatureService _signatureService;

    public WorkspaceExchangeValidator(ISignatureService signatureService)
    {
        _signatureService = signatureService;
    }

    public ExchangeValidationResult Validate(
        string workspaceRoot,
        IEnumerable<string> documentPaths,
        SignaturePublicKey publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return Validate(
            workspaceRoot,
            documentPaths,
            new Dictionary<string, SignaturePublicKey>(StringComparer.Ordinal)
            {
                [publicKey.KeyId] = publicKey,
            });
    }

    public ExchangeValidationResult Validate(
        string workspaceRoot,
        IEnumerable<string> documentPaths,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(documentPaths);
        ArgumentNullException.ThrowIfNull(publicKeys);

        var invalidPaths = new List<string>();

        foreach (var documentPath in documentPaths.Distinct(StringComparer.Ordinal))
        {
            var absoluteDocumentPath = Path.Combine(workspaceRoot, documentPath.Replace('/', Path.DirectorySeparatorChar));
            var absoluteSignaturePath = Path.Combine(workspaceRoot, Path.ChangeExtension(documentPath, ".sig").Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(absoluteDocumentPath) || !File.Exists(absoluteSignaturePath))
            {
                invalidPaths.Add(documentPath);
                continue;
            }

            var payload = Encoding.UTF8.GetBytes(File.ReadAllText(absoluteDocumentPath, Encoding.UTF8));
            var signature = JsonSerializer.Deserialize<DetachedSignature>(
                File.ReadAllText(absoluteSignaturePath, Encoding.UTF8),
                SignatureSerializerOptions);

            if (signature is null ||
                !publicKeys.TryGetValue(signature.KeyId, out var publicKey) ||
                !_signatureService.Verify(payload, signature, publicKey))
            {
                invalidPaths.Add(documentPath);
            }
        }

        return new ExchangeValidationResult(invalidPaths.Count == 0, invalidPaths);
    }
}
