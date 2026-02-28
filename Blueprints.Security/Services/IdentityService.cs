using Blueprints.Security.Abstractions;
using Blueprints.Security.Models;

namespace Blueprints.Security.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly string _rootDirectory;
    private readonly IIdentityStore _identityStore;

    public IdentityService(string rootDirectory, IIdentityStore identityStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _rootDirectory = rootDirectory;
        _identityStore = identityStore;
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
