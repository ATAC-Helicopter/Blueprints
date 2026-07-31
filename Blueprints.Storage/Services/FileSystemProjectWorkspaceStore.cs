using Blueprints.Core.Enums;
using Blueprints.Core.Models;
using Blueprints.Core.Services;
using Blueprints.Security.Models;
using Blueprints.Storage.Abstractions;
using Blueprints.Storage.Models;

namespace Blueprints.Storage.Services;

public sealed class FileSystemProjectWorkspaceStore : IProjectWorkspaceStore
{
    private readonly ISignedDocumentStore _signedDocumentStore;

    public FileSystemProjectWorkspaceStore(ISignedDocumentStore signedDocumentStore)
    {
        _signedDocumentStore = signedDocumentStore;
    }

    public ProjectWorkspaceLoadResult Load(
        string workspaceRoot,
        SignaturePublicKey publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        return Load(
            workspaceRoot,
            new Dictionary<string, SignaturePublicKey>(StringComparer.Ordinal)
            {
                [publicKey.KeyId] = publicKey,
            });
    }

    public ProjectWorkspaceLoadResult Load(
        string workspaceRoot,
        IReadOnlyDictionary<string, SignaturePublicKey> publicKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(publicKeys);

        try
        {
            var projectResult = _signedDocumentStore.Read<ProjectConfigurationDocument>(
                GetProjectDocumentPath(workspaceRoot),
                publicKeys);
            var membersResult = _signedDocumentStore.Read<MemberDocument>(
                GetMembersDocumentPath(workspaceRoot),
                publicKeys);

            var versionSnapshots = new List<VersionWorkspaceSnapshot>();
            var allSignaturesValid = projectResult.IsSignatureValid && membersResult.IsSignatureValid;
            var totalDocuments = 2;
            var invalidSignatures = allSignaturesValid ? 0 : CountInvalid(projectResult.IsSignatureValid, membersResult.IsSignatureValid);
            CanvasLayoutDocument? canvasLayout = null;
            RelationshipDocument? relationships = null;

            var canvasLayoutPath = GetCanvasLayoutDocumentPath(workspaceRoot);
            if (File.Exists(canvasLayoutPath))
            {
                var canvasLayoutResult = _signedDocumentStore.Read<CanvasLayoutDocument>(
                    canvasLayoutPath,
                    publicKeys);
                CanvasLayoutValidator.Validate(canvasLayoutResult.Document, projectResult.Document.ProjectId);
                canvasLayout = canvasLayoutResult.Document;
                totalDocuments++;
                if (!canvasLayoutResult.IsSignatureValid)
                {
                    invalidSignatures++;
                    allSignaturesValid = false;
                }
            }

            var relationshipsPath = GetRelationshipsDocumentPath(workspaceRoot);
            if (File.Exists(relationshipsPath))
            {
                var relationshipsResult = _signedDocumentStore.Read<RelationshipDocument>(
                    relationshipsPath,
                    publicKeys);
                RelationshipDocumentValidator.Validate(
                    relationshipsResult.Document,
                    projectResult.Document.ProjectId);
                relationships = relationshipsResult.Document;
                totalDocuments++;
                if (!relationshipsResult.IsSignatureValid)
                {
                    invalidSignatures++;
                    allSignaturesValid = false;
                }
            }

            var versionsRoot = GetVersionsRoot(workspaceRoot);
            if (Directory.Exists(versionsRoot))
            {
                foreach (var versionDirectory in Directory.EnumerateDirectories(versionsRoot).OrderBy(static path => path, StringComparer.Ordinal))
                {
                    var versionResult = _signedDocumentStore.Read<VersionDocument>(
                        GetVersionDocumentPath(versionDirectory),
                        publicKeys);

                    totalDocuments++;
                    if (!versionResult.IsSignatureValid)
                    {
                        invalidSignatures++;
                        allSignaturesValid = false;
                    }

                    var items = new List<ItemDocument>();
                    var itemsRoot = GetItemsRoot(versionDirectory);
                    if (Directory.Exists(itemsRoot))
                    {
                        foreach (var itemPath in Directory.EnumerateFiles(itemsRoot, "*.json").OrderBy(static path => path, StringComparer.Ordinal))
                        {
                            var itemResult = _signedDocumentStore.Read<ItemDocument>(itemPath, publicKeys);
                            ItemDocumentValidator.Validate(
                                itemResult.Document,
                                projectResult.Document.ProjectId,
                                versionResult.Document.VersionId);
                            totalDocuments++;
                            if (!itemResult.IsSignatureValid)
                            {
                                invalidSignatures++;
                                allSignaturesValid = false;
                            }

                            items.Add(itemResult.Document);
                        }
                    }

                    versionSnapshots.Add(new VersionWorkspaceSnapshot(versionResult.Document, items));
                }
            }

            if (canvasLayout is not null)
            {
                CanvasLayoutValidator.ValidateEntityReferences(
                    canvasLayout,
                    projectResult.Document.ProjectId,
                    versionSnapshots.Select(static version => version.Version.VersionId).ToHashSet(),
                    versionSnapshots
                        .SelectMany(static version => version.Items)
                        .Select(static item => item.ItemId)
                        .ToHashSet());
            }

            if (relationships is not null)
            {
                RelationshipDocumentValidator.ValidateEntityReferences(
                    relationships,
                    projectResult.Document.ProjectId,
                    versionSnapshots.Select(static version => version.Version.VersionId).ToHashSet(),
                    versionSnapshots
                        .SelectMany(static version => version.Items)
                        .Select(static item => item.ItemId)
                        .ToHashSet());
            }

            var trustState = allSignaturesValid ? TrustState.Trusted : TrustState.Untrusted;
            var summary = allSignaturesValid
                ? $"Validated {totalDocuments} signed documents."
                : $"Validated {totalDocuments} signed documents with {invalidSignatures} invalid signatures.";

            return new ProjectWorkspaceLoadResult(
                new ProjectWorkspaceSnapshot(
                    projectResult.Document,
                    membersResult.Document,
                    versionSnapshots,
                    canvasLayout,
                    relationships),
                new TrustReport(trustState, summary, DateTimeOffset.UtcNow));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or InvalidOperationException)
        {
            return new ProjectWorkspaceLoadResult(
                EmptyWorkspace(),
                new TrustReport(
                    TrustState.Corrupt,
                    $"Workspace load failed: {exception.Message}",
                    DateTimeOffset.UtcNow));
        }
    }

    public void Save(
        string workspaceRoot,
        ProjectWorkspaceSnapshot workspace,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(workspace);

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(GetProjectRoot(workspaceRoot));
        Directory.CreateDirectory(GetVersionsRoot(workspaceRoot));

        _signedDocumentStore.Write(
            GetProjectDocumentPath(workspaceRoot),
            workspace.Project,
            signingKey);
        _signedDocumentStore.Write(
            GetMembersDocumentPath(workspaceRoot),
            workspace.Members,
            signingKey);
        if (workspace.CanvasLayout is not null)
        {
            SaveCanvasLayout(workspaceRoot, workspace.CanvasLayout, signingKey);
        }
        if (workspace.Relationships is not null)
        {
            SaveRelationships(workspaceRoot, workspace.Relationships, signingKey);
        }

        foreach (var versionSnapshot in workspace.Versions)
        {
            var versionDirectory = GetVersionDirectory(workspaceRoot, versionSnapshot.Version.VersionId);
            Directory.CreateDirectory(versionDirectory);
            Directory.CreateDirectory(GetItemsRoot(versionDirectory));

            _signedDocumentStore.Write(
                GetVersionDocumentPath(versionDirectory),
                versionSnapshot.Version,
                signingKey);

            foreach (var item in versionSnapshot.Items)
            {
                _signedDocumentStore.Write(
                    GetItemDocumentPath(versionDirectory, item.ItemId),
                    item,
                    signingKey);
            }
        }
    }

    public void SaveCanvasLayout(
        string workspaceRoot,
        CanvasLayoutDocument layout,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(layout);

        CanvasLayoutValidator.Validate(layout, layout.ProjectId);
        _signedDocumentStore.Write(
            GetCanvasLayoutDocumentPath(workspaceRoot),
            layout,
            signingKey);
    }

    public void SaveRelationships(
        string workspaceRoot,
        RelationshipDocument relationships,
        SignatureKeyMaterial signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(relationships);

        RelationshipDocumentValidator.Validate(relationships, relationships.ProjectId);
        _signedDocumentStore.Write(
            GetRelationshipsDocumentPath(workspaceRoot),
            relationships,
            signingKey);
    }

    private static ProjectWorkspaceSnapshot EmptyWorkspace() =>
        new(
            new ProjectConfigurationDocument(
                0,
                Guid.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                DateTimeOffset.MinValue,
                [],
                new Dictionary<string, ItemTypeDefinition>(),
                new Dictionary<string, ItemKeyRule>(),
                new ChangelogRules(false, false, false, false)),
            new MemberDocument(0, Guid.Empty, 0, []),
            []);

    private static int CountInvalid(params bool[] values) =>
        values.Count(static value => !value);

    private static string GetProjectRoot(string workspaceRoot) =>
        Path.Combine(workspaceRoot, "project");

    private static string GetVersionsRoot(string workspaceRoot) =>
        Path.Combine(workspaceRoot, "versions");

    private static string GetProjectDocumentPath(string workspaceRoot) =>
        Path.Combine(GetProjectRoot(workspaceRoot), "project.json");

    private static string GetMembersDocumentPath(string workspaceRoot) =>
        Path.Combine(GetProjectRoot(workspaceRoot), "members.json");

    private static string GetCanvasLayoutDocumentPath(string workspaceRoot) =>
        Path.Combine(GetProjectRoot(workspaceRoot), "canvas-layout.json");

    private static string GetRelationshipsDocumentPath(string workspaceRoot) =>
        Path.Combine(GetProjectRoot(workspaceRoot), "relationships.json");

    private static string GetVersionDirectory(string workspaceRoot, Guid versionId) =>
        Path.Combine(GetVersionsRoot(workspaceRoot), versionId.ToString("N"));

    private static string GetVersionDocumentPath(string versionDirectory) =>
        Path.Combine(versionDirectory, "version.json");

    private static string GetItemsRoot(string versionDirectory) =>
        Path.Combine(versionDirectory, "items");

    private static string GetItemDocumentPath(string versionDirectory, Guid itemId) =>
        Path.Combine(GetItemsRoot(versionDirectory), $"{itemId:N}.json");
}
