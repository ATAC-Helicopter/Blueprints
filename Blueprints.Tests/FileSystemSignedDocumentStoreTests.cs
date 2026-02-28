using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class FileSystemSignedDocumentStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "Blueprints.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteAndRead_RoundTripsSignedProjectDocument()
    {
        Directory.CreateDirectory(_tempDirectory);

        var generator = new Ed25519KeyPairGenerator();
        var keyPair = generator.Generate("project-admin");
        var serializer = new CanonicalJsonSerializer();
        var signatureService = new Ed25519SignatureService();
        var store = new FileSystemSignedDocumentStore(serializer, signatureService);

        var documentPath = Path.Combine(_tempDirectory, "project.json");
        var document = new ProjectConfigurationDocument(
            SchemaVersion: 1,
            ProjectId: Guid.NewGuid(),
            Name: "VaultSync",
            ProjectCode: "VS",
            VersioningScheme: "SemVer",
            CreatedUtc: DateTimeOffset.UtcNow,
            DefaultCategories:
            [
                new CategoryDefinition("added", "Added"),
                new CategoryDefinition("fixed", "Fixed"),
            ],
            ItemTypes: new Dictionary<string, ItemTypeDefinition>
            {
                ["feature"] = new("feature", "Feature"),
                ["bug"] = new("bug", "Bug"),
            },
            ItemKeyRules: new Dictionary<string, ItemKeyRule>
            {
                ["feature"] = new("VS", ItemKeyScope.Version),
                ["bug"] = new("BUG", ItemKeyScope.Project),
            },
            ChangelogRules: new ChangelogRules(
                IncludeIncompleteByDefault: false,
                IncludeItemKeysByDefault: true,
                IncludeDescriptionsByDefault: false,
                CompactModeByDefault: false));

        store.Write(
            documentPath,
            document,
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var result = store.Read<ProjectConfigurationDocument>(
            documentPath,
            new SignaturePublicKey(keyPair.KeyId, keyPair.PublicKeyBytes));

        Assert.True(result.IsSignatureValid);
        Assert.Equal(document.ProjectCode, result.Document.ProjectCode);
        Assert.Equal("VS", result.Document.ItemKeyRules["feature"].Prefix);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
