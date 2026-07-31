using Blueprints.Security.Services;

namespace Blueprints.Tests;

public sealed class IdentityBackupServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "IdentityBackups",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ExportAndImport_RestoresSameSigningIdentityUnderLocalProtection()
    {
        var sourceRoot = Path.Combine(_testRoot, "source");
        var destinationRoot = Path.Combine(_testRoot, "destination");
        var sourceStore = CreateStore(sourceRoot);
        var destinationStore = CreateStore(destinationRoot);
        var original = sourceStore.Create("Backup Owner");
        var backupPath = Path.Combine(_testRoot, "identity.blueprints-backup");
        const string passphrase = "a long test passphrase";
        var exportService = new IdentityBackupService(
            sourceStore,
            new Ed25519SignatureService());
        var importService = new IdentityBackupService(
            destinationStore,
            new Ed25519SignatureService());

        exportService.Export(backupPath, original, passphrase);
        var restored = importService.Import(backupPath, passphrase);

        Assert.Equal(original.Profile.UserId, restored.Profile.UserId);
        Assert.Equal(original.Profile.KeyId, restored.Profile.KeyId);
        Assert.Equal(original.Profile.PublicKeyBase64, restored.Profile.PublicKeyBase64);
        var reloaded = destinationStore.Load(restored.Profile.UserId);
        var signatureService = new Ed25519SignatureService();
        var message = "restored identity proof"u8.ToArray();
        var signature = signatureService.Sign(message, reloaded.SigningKey);
        Assert.True(signatureService.Verify(message, signature, original.PublicKey));
    }

    [Fact]
    public void Import_RejectsWrongPassphraseWithoutCreatingIdentity()
    {
        var sourceRoot = Path.Combine(_testRoot, "source");
        var destinationRoot = Path.Combine(_testRoot, "destination");
        var sourceStore = CreateStore(sourceRoot);
        var destinationStore = CreateStore(destinationRoot);
        var original = sourceStore.Create("Backup Owner");
        var backupPath = Path.Combine(_testRoot, "identity.blueprints-backup");
        var exportService = new IdentityBackupService(
            sourceStore,
            new Ed25519SignatureService());
        var importService = new IdentityBackupService(
            destinationStore,
            new Ed25519SignatureService());
        exportService.Export(backupPath, original, "correct horse battery staple");

        var error = Assert.Throws<InvalidOperationException>(() =>
            importService.Import(backupPath, "incorrect passphrase"));

        Assert.Contains("incorrect", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(
            Path.Combine(destinationRoot, original.Profile.UserId.ToString("N"))));
    }

    [Fact]
    public void Import_RejectsChangedPublicIdentityEvenWithValidCiphertext()
    {
        var sourceRoot = Path.Combine(_testRoot, "source");
        var destinationRoot = Path.Combine(_testRoot, "destination");
        var sourceStore = CreateStore(sourceRoot);
        var destinationStore = CreateStore(destinationRoot);
        var original = sourceStore.Create("Backup Owner");
        var backupPath = Path.Combine(_testRoot, "identity.blueprints-backup");
        var service = new IdentityBackupService(
            sourceStore,
            new Ed25519SignatureService());
        service.Export(backupPath, original, "correct horse battery staple");
        var json = File.ReadAllText(backupPath);
        File.WriteAllText(
            backupPath,
            json.Replace(
                "\"displayName\": \"Backup Owner\"",
                "\"displayName\": \"Attacker\"",
                StringComparison.Ordinal));
        var importService = new IdentityBackupService(
            destinationStore,
            new Ed25519SignatureService());

        Assert.Throws<InvalidOperationException>(() =>
            importService.Import(backupPath, "correct horse battery staple"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static FileSystemIdentityStore CreateStore(string root)
    {
        Directory.CreateDirectory(root);
        return new FileSystemIdentityStore(
            root,
            new Ed25519KeyPairGenerator(),
            new LocalFilePrivateKeyProtector(Path.Combine(root, "protector.key")));
    }
}
