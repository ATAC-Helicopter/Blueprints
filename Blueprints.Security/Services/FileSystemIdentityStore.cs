using System.Text;
using System.Text.Json;
using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;

namespace Blueprints.Security.Services;

public sealed class FileSystemIdentityStore : IIdentityStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _rootDirectory;
    private readonly IKeyPairGenerator _keyPairGenerator;
    private readonly IPrivateKeyProtector _privateKeyProtector;

    public FileSystemIdentityStore(
        string rootDirectory,
        IKeyPairGenerator keyPairGenerator,
        IPrivateKeyProtector privateKeyProtector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _rootDirectory = rootDirectory;
        _keyPairGenerator = keyPairGenerator;
        _privateKeyProtector = privateKeyProtector;
    }

    public StoredIdentity Create(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var userId = Guid.NewGuid();
        var keyId = userId.ToString("N");
        var keyPair = _keyPairGenerator.Generate(keyId);
        var profile = new IdentityProfile(
            UserId: userId,
            DisplayName: displayName.Trim(),
            KeyId: keyPair.KeyId,
            PublicKeyBase64: Convert.ToBase64String(keyPair.PublicKeyBytes),
            KeyStorageProvider: _privateKeyProtector.ProviderName,
            CreatedUtc: DateTimeOffset.UtcNow);

        var identityDirectory = GetIdentityDirectory(userId);
        Directory.CreateDirectory(identityDirectory);

        var protectedPrivateKeyBytes = _privateKeyProtector.Protect(keyPair.PrivateKeyBytes);

        File.WriteAllText(
            GetProfilePath(userId),
            JsonSerializer.Serialize(profile, SerializerOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        File.WriteAllBytes(GetProtectedPrivateKeyPath(userId), protectedPrivateKeyBytes);

        return new StoredIdentity(
            profile,
            new SignatureKeyMaterial(profile.KeyId, keyPair.PrivateKeyBytes),
            new SignaturePublicKey(profile.KeyId, keyPair.PublicKeyBytes));
    }

    public StoredIdentity Load(Guid userId)
    {
        var profilePath = GetProfilePath(userId);
        var privateKeyPath = GetProtectedPrivateKeyPath(userId);

        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException("Identity profile was not found.", profilePath);
        }

        if (!File.Exists(privateKeyPath))
        {
            throw new FileNotFoundException("Protected private key was not found.", privateKeyPath);
        }

        var profileJson = File.ReadAllText(profilePath, Encoding.UTF8);
        var profile = JsonSerializer.Deserialize<IdentityProfile>(profileJson, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize identity profile.");

        var protectedPrivateKeyBytes = File.ReadAllBytes(privateKeyPath);
        var privateKeyBytes = _privateKeyProtector.Unprotect(protectedPrivateKeyBytes);
        var publicKeyBytes = Convert.FromBase64String(profile.PublicKeyBase64);

        return new StoredIdentity(
            profile,
            new SignatureKeyMaterial(profile.KeyId, privateKeyBytes),
            new SignaturePublicKey(profile.KeyId, publicKeyBytes));
    }

    public StoredIdentity Import(IdentityProfile profile, byte[] privateKeyBytes)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(privateKeyBytes);
        if (profile.UserId == Guid.Empty
            || string.IsNullOrWhiteSpace(profile.DisplayName)
            || string.IsNullOrWhiteSpace(profile.KeyId)
            || privateKeyBytes.Length == 0)
        {
            throw new InvalidOperationException("The restored identity is incomplete.");
        }

        var identityDirectory = GetIdentityDirectory(profile.UserId);
        if (Directory.Exists(identityDirectory))
        {
            throw new InvalidOperationException(
                "An identity with this user ID already exists on this device.");
        }

        var importedProfile = profile with
        {
            DisplayName = profile.DisplayName.Trim(),
            KeyStorageProvider = _privateKeyProtector.ProviderName,
        };
        var stagingDirectory = identityDirectory + ".importing";
        if (Directory.Exists(stagingDirectory))
        {
            throw new InvalidOperationException(
                "An incomplete identity import directory already exists. Inspect or move it before retrying.");
        }

        Directory.CreateDirectory(stagingDirectory);
        try
        {
            File.WriteAllText(
                Path.Combine(stagingDirectory, "identity.json"),
                JsonSerializer.Serialize(importedProfile, SerializerOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllBytes(
                Path.Combine(stagingDirectory, "private.key.protected"),
                _privateKeyProtector.Protect(privateKeyBytes));
            Directory.Move(stagingDirectory, identityDirectory);
        }
        catch
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }

            throw;
        }

        return new StoredIdentity(
            importedProfile,
            new SignatureKeyMaterial(importedProfile.KeyId, privateKeyBytes.ToArray()),
            new SignaturePublicKey(
                importedProfile.KeyId,
                Convert.FromBase64String(importedProfile.PublicKeyBase64)));
    }

    private string GetIdentityDirectory(Guid userId) =>
        Path.Combine(_rootDirectory, userId.ToString("N"));

    private string GetProfilePath(Guid userId) =>
        Path.Combine(GetIdentityDirectory(userId), "identity.json");

    private string GetProtectedPrivateKeyPath(Guid userId) =>
        Path.Combine(GetIdentityDirectory(userId), "private.key.protected");
}
