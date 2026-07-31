using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;

namespace Blueprints.Security.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly string _rootDirectory;
    private readonly IIdentityStore _identityStore;
    private readonly IdentityBackupService _backupService;

    public IdentityService(string rootDirectory, IIdentityStore identityStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _rootDirectory = rootDirectory;
        _identityStore = identityStore;
        _backupService = new IdentityBackupService(
            identityStore,
            new Ed25519SignatureService());
    }

    public StoredIdentity GetOrCreateDefaultIdentity(string displayName)
    {
        var existingProfile = ListProfiles()
            .OrderBy(static profile => profile.CreatedUtc)
            .FirstOrDefault();

        return existingProfile is null
            ? _identityStore.Create(displayName)
            : _identityStore.Load(existingProfile.UserId);
    }

    public StoredIdentity CreateIdentity(string displayName) =>
        _identityStore.Create(displayName);

    public string ExportBackup(string filePath, string passphrase) =>
        _backupService.Export(
            filePath,
            GetOrCreateDefaultIdentity("Local Admin"),
            passphrase);

    public StoredIdentity ImportBackup(string filePath, string passphrase) =>
        _backupService.Import(filePath, passphrase);

    public IReadOnlyList<IdentityProfile> ListProfiles()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return [];
        }

        var profiles = new List<IdentityProfile>();

        foreach (var identityDirectory in Directory.EnumerateDirectories(_rootDirectory))
        {
            var profilePath = Path.Combine(identityDirectory, "identity.json");
            if (!File.Exists(profilePath))
            {
                continue;
            }

            var profile = System.Text.Json.JsonSerializer.Deserialize<IdentityProfile>(
                File.ReadAllText(profilePath),
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                });

            if (profile is not null)
            {
                profiles.Add(profile);
            }
        }

        return profiles
            .OrderBy(static profile => profile.CreatedUtc)
            .ToArray();
    }
}
