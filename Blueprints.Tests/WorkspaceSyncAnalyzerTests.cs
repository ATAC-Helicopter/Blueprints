using Blueprints.Collaboration.Services;
using Blueprints.Core.Models;
using Blueprints.Security.Models;
using Blueprints.Security.Services;
using Blueprints.Storage.Models;
using Blueprints.Storage.Services;

namespace Blueprints.Tests;

public sealed class WorkspaceSyncAnalyzerTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "Blueprints.Tests",
        "WorkspaceSyncAnalyzer",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Analyze_FindsOutgoingAndIncomingDocuments()
    {
        var localRoot = Path.Combine(_rootDirectory, "local");
        var sharedRoot = Path.Combine(_rootDirectory, "shared");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(sharedRoot);

        var keyPair = new Ed25519KeyPairGenerator().Generate("sync-admin");
        var signedStore = new FileSystemSignedDocumentStore(
            new CanonicalJsonSerializer(),
            new Ed25519SignatureService());
        var workspaceStore = new FileSystemProjectWorkspaceStore(signedStore);

        var localWorkspace = TestWorkspaceFactory.CreateWorkspaceSnapshot();
        var sharedWorkspace = TestWorkspaceFactory.CreateWorkspaceSnapshot(projectId: localWorkspace.Project.ProjectId);
        workspaceStore.Save(localRoot, localWorkspace, new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));
        workspaceStore.Save(sharedRoot, sharedWorkspace, new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var localVersion = localWorkspace.Versions[0];
        var extraItem = new ItemDocument(
            1,
            localWorkspace.Project.ProjectId,
            localVersion.Version.VersionId,
            Guid.NewGuid(),
            "BUG-2",
            "bug",
            "bug",
            "Only local",
            null,
            false,
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            localWorkspace.Members.Members[0].UserId,
            localWorkspace.Members.Members[0].DisplayName);

        workspaceStore.Save(
            localRoot,
            new ProjectWorkspaceSnapshot(
                localWorkspace.Project,
                localWorkspace.Members,
                [
                    new VersionWorkspaceSnapshot(
                        localVersion.Version,
                        localVersion.Items.Concat([extraItem]).ToArray()),
                ]),
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var sharedVersion = sharedWorkspace.Versions[0];
        workspaceStore.Save(
            sharedRoot,
            new ProjectWorkspaceSnapshot(
                sharedWorkspace.Project,
                sharedWorkspace.Members,
                [
                    new VersionWorkspaceSnapshot(
                        sharedVersion.Version with { Notes = "Updated on shared" },
                        sharedVersion.Items),
                ]),
            new SignatureKeyMaterial(keyPair.KeyId, keyPair.PrivateKeyBytes));

        var analyzer = new WorkspaceSyncAnalyzer(new WorkspaceExchangeSnapshotBuilder());
        var analysis = analyzer.Analyze(new WorkspacePaths(localRoot, sharedRoot));

        Assert.Contains(analysis.OutgoingDocumentPaths, static path => path.Contains("/items/", StringComparison.Ordinal));
        Assert.Contains("versions/" + sharedVersion.Version.VersionId.ToString("N") + "/version.json", analysis.IncomingDocumentPaths);
        Assert.DoesNotContain("versions/" + sharedVersion.Version.VersionId.ToString("N") + "/version.json", analysis.PotentialConflictDocumentPaths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
