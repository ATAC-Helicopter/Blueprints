using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;

namespace Blueprints.Security.Services;

public sealed class IdentityBackupService
{
    private const int CurrentSchemaVersion = 1;
    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;
    private const int DerivationIterations = 600_000;
    private const long MaximumBackupBytes = 1024 * 1024;
    private const string KeyDerivation = "PBKDF2-SHA256";
    private static readonly byte[] ProofPayload =
        "Blueprints identity backup proof v1"u8.ToArray();
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IIdentityStore _identityStore;
    private readonly ISignatureService _signatureService;

    public IdentityBackupService(
        IIdentityStore identityStore,
        ISignatureService signatureService)
    {
        _identityStore = identityStore;
        _signatureService = signatureService;
    }

    public string Export(
        string filePath,
        StoredIdentity identity,
        string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(identity);
        ValidatePassphrase(passphrase);

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var tag = new byte[TagLength];
        var ciphertext = new byte[identity.SigningKey.PrivateKeyBytes.Length];
        var key = DeriveKey(passphrase, salt, DerivationIterations);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(
                nonce,
                identity.SigningKey.PrivateKeyBytes,
                ciphertext,
                tag,
                CreateAssociatedData(identity.Profile));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        var backup = new IdentityBackupFile(
            CurrentSchemaVersion,
            identity.Profile,
            KeyDerivation,
            DerivationIterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
        WriteAtomically(filePath, JsonSerializer.Serialize(backup, SerializerOptions));
        return Path.GetFullPath(filePath);
    }

    public StoredIdentity Import(string filePath, string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ValidatePassphrase(passphrase);
        var info = new FileInfo(filePath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Identity backup was not found.", filePath);
        }

        if (info.Length > MaximumBackupBytes)
        {
            throw new InvalidOperationException("Identity backup exceeds the supported size.");
        }

        var backup = JsonSerializer.Deserialize<IdentityBackupFile>(
            File.ReadAllText(filePath, Encoding.UTF8),
            SerializerOptions)
            ?? throw new InvalidOperationException("Identity backup could not be read.");
        ValidateBackup(backup);

        byte[] salt;
        byte[] nonce;
        byte[] ciphertext;
        byte[] tag;
        try
        {
            salt = Convert.FromBase64String(backup.SaltBase64);
            nonce = Convert.FromBase64String(backup.NonceBase64);
            ciphertext = Convert.FromBase64String(backup.CiphertextBase64);
            tag = Convert.FromBase64String(backup.TagBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Identity backup contains malformed encrypted data.",
                exception);
        }

        if (salt.Length != SaltLength
            || nonce.Length != NonceLength
            || tag.Length != TagLength
            || ciphertext.Length is < 32 or > 256)
        {
            throw new InvalidOperationException(
                "Identity backup encryption parameters are invalid.");
        }

        var privateKey = new byte[ciphertext.Length];
        var key = DeriveKey(passphrase, salt, backup.Iterations);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(
                nonce,
                ciphertext,
                tag,
                privateKey,
                CreateAssociatedData(backup.Identity));
            VerifyPrivateKey(backup.Identity, privateKey);
            return _identityStore.Import(backup.Identity, privateKey);
        }
        catch (AuthenticationTagMismatchException exception)
        {
            throw new InvalidOperationException(
                "The passphrase is incorrect or the identity backup was changed.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    private void VerifyPrivateKey(IdentityProfile profile, byte[] privateKey)
    {
        try
        {
            var signingKey = new SignatureKeyMaterial(profile.KeyId, privateKey);
            var publicKey = new SignaturePublicKey(
                profile.KeyId,
                Convert.FromBase64String(profile.PublicKeyBase64));
            var proof = _signatureService.Sign(ProofPayload, signingKey);
            if (!_signatureService.Verify(ProofPayload, proof, publicKey))
            {
                throw new InvalidOperationException(
                    "The identity backup private key does not match its public identity.");
            }
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Identity backup contains an invalid public key.",
                exception);
        }
    }

    private static void ValidateBackup(IdentityBackupFile backup)
    {
        if (backup.SchemaVersion != CurrentSchemaVersion
            || !string.Equals(backup.KeyDerivation, KeyDerivation, StringComparison.Ordinal)
            || backup.Iterations != DerivationIterations
            || backup.Identity.UserId == Guid.Empty
            || string.IsNullOrWhiteSpace(backup.Identity.DisplayName)
            || string.IsNullOrWhiteSpace(backup.Identity.KeyId)
            || backup.Identity.DisplayName.Length > 200
            || backup.Identity.KeyId.Length > 128)
        {
            throw new InvalidOperationException(
                "Identity backup format or identity metadata is unsupported.");
        }
    }

    private static void ValidatePassphrase(string passphrase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passphrase);
        if (passphrase.Length < 12 || passphrase.Length > 1024)
        {
            throw new InvalidOperationException(
                "Identity backup passphrases must contain at least 12 characters.");
        }
    }

    private static byte[] DeriveKey(
        string passphrase,
        byte[] salt,
        int iterations)
    {
        var passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                passphraseBytes,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                KeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passphraseBytes);
        }
    }

    private static byte[] CreateAssociatedData(IdentityProfile identity) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                identity.UserId,
                identity.DisplayName,
                identity.KeyId,
                identity.PublicKeyBase64,
                identity.CreatedUtc,
            },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

    private static void WriteAtomically(string filePath, string json)
    {
        var fullPath = Path.GetFullPath(filePath);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Identity backup path has no parent.");
        Directory.CreateDirectory(parent);
        var temporaryPath = fullPath + ".tmp";
        File.WriteAllText(
            temporaryPath,
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
}
