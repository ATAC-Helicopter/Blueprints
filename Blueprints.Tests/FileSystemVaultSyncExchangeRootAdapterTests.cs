using System.Text.Json;
using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class FileSystemVaultSyncExchangeRootAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly Guid _projectId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    [Fact]
    public void PrepareIntent_DerivesTheProjectSpecificExchangeRoot()
    {
        CreateMetadataStore();
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();

        var intent = adapter.PrepareIntent(_root, _projectId);

        Assert.Equal(Path.GetFullPath(_root), intent.DestinationRoot);
        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(_root),
                ".blueprints",
                "projects",
                _projectId.ToString("D")),
            intent.ExchangeRoot);
    }

    [Fact]
    public void Register_RequiresFreshExactSingleUseApproval()
    {
        CreateMetadataStore();
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();
        var now = DateTimeOffset.UtcNow;
        var intent = adapter.PrepareIntent(_root, _projectId);
        var approval = adapter.Approve(intent, now);

        Assert.Throws<InvalidOperationException>(
            () => adapter.Register(
                intent,
                approval with
                {
                    Intent = intent with { ProjectId = Guid.NewGuid() },
                },
                now));
        Assert.Throws<InvalidOperationException>(
            () => adapter.Register(
                intent,
                approval with { ExpiresUtc = now },
                now));

        adapter.Register(intent, approval, now);

        Assert.Throws<InvalidOperationException>(
            () => adapter.Register(intent, approval, now));
    }

    [Fact]
    public void Register_WritesBoundedProjectMarkerAndIsIdempotent()
    {
        CreateMetadataStore();
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();
        var now = DateTimeOffset.Parse("2026-07-30T23:00:00Z");
        var intent = adapter.PrepareIntent(_root, _projectId);

        var created = adapter.Register(
            intent,
            adapter.Approve(intent, now),
            now);

        Assert.False(created.AlreadyRegistered);
        Assert.True(File.Exists(created.RegistrationMarkerPath));
        using (var marker = JsonDocument.Parse(
                   File.ReadAllText(created.RegistrationMarkerPath)))
        {
            Assert.Equal(
                1,
                marker.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(
                _projectId,
                marker.RootElement.GetProperty("projectId").GetGuid());
        }

        var existing = adapter.Register(
            intent,
            adapter.Approve(intent, now.AddMinutes(1)),
            now.AddMinutes(1));

        Assert.True(existing.AlreadyRegistered);
        Assert.Equal(created.ExchangeRoot, existing.ExchangeRoot);
    }

    [Fact]
    public void Register_RefusesToAdoptUnregisteredContent()
    {
        CreateMetadataStore();
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();
        var now = DateTimeOffset.UtcNow;
        var intent = adapter.PrepareIntent(_root, _projectId);
        Directory.CreateDirectory(intent.ExchangeRoot);
        File.WriteAllText(Path.Combine(intent.ExchangeRoot, "unexpected.txt"), "content");

        var exception = Assert.Throws<InvalidOperationException>(
            () => adapter.Register(
                intent,
                adapter.Approve(intent, now),
                now));

        Assert.Contains(
            "unregistered content",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrepareIntent_RejectsMetadataOutsideTheCanonicalLayout()
    {
        var metadataDirectory = Path.Combine(_root, "meta");
        Directory.CreateDirectory(metadataDirectory);
        File.WriteAllBytes(
            Path.Combine(
                metadataDirectory,
                FileSystemVaultSyncStatusReader.MetadataFileName),
            []);
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();

        var exception = Assert.Throws<InvalidOperationException>(
            () => adapter.PrepareIntent(_root, _projectId));

        Assert.Contains(
            "<destination>/.vaultsync/meta",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Register_RejectsOversizedExistingMarker()
    {
        CreateMetadataStore();
        var adapter = new FileSystemVaultSyncExchangeRootAdapter();
        var now = DateTimeOffset.UtcNow;
        var intent = adapter.PrepareIntent(_root, _projectId);
        Directory.CreateDirectory(intent.ExchangeRoot);
        var markerPath = Path.Combine(
            intent.ExchangeRoot,
            FileSystemVaultSyncExchangeRootAdapter.RegistrationMarkerFileName);
        using (var stream = File.Create(markerPath))
        {
            stream.SetLength(
                FileSystemVaultSyncExchangeRootAdapter.MaximumRegistrationMarkerBytes + 1);
        }

        var exception = Assert.Throws<InvalidOperationException>(
            () => adapter.Register(
                intent,
                adapter.Approve(intent, now),
                now));

        Assert.Contains("read limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void CreateMetadataStore()
    {
        var metadataDirectory = Path.Combine(_root, ".vaultsync", "meta");
        Directory.CreateDirectory(metadataDirectory);
        File.WriteAllBytes(
            Path.Combine(
                metadataDirectory,
                FileSystemVaultSyncStatusReader.MetadataFileName),
            []);
    }
}
