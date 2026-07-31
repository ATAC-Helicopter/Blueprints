using Blueprints.App.Models;
using Blueprints.App.Services;

namespace Blueprints.Tests;

public sealed class HostedSourceProviderContractTests
{
    [Fact]
    public void Router_RejectsIncompatibleContractVersion()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new HostedSourceProviderRouter([new StubReader(contractVersion: 2)]));

        Assert.Contains("requires contract 1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Router_RejectsUnboundedProviderResponse()
    {
        var reader = new StubReader(
            candidateCount: HostedSourceProviderContract.MaximumCandidatesPerDiscovery + 1);
        var router = new HostedSourceProviderRouter([reader]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            router.Read(
                "/repository",
                new HostedRepositoryDescriptor(SourceProviderKind.GitHub, "owner/repo")));

        Assert.Contains("bounded contract", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInReadersDeclareStableCapabilities()
    {
        IHostedSourceProviderReader github = new GitHubRestSourceProviderReader();
        IHostedSourceProviderReader gitlab = new GitLabRestSourceProviderReader();

        Assert.Equal(1, github.ContractVersion);
        Assert.Equal(SourceProviderKind.GitHub, github.Provider);
        Assert.True(github.Capabilities.HasFlag(SourceProviderCapabilities.Issues));
        Assert.Equal(1, gitlab.ContractVersion);
        Assert.Equal(SourceProviderKind.GitLab, gitlab.Provider);
        Assert.True(gitlab.Capabilities.HasFlag(SourceProviderCapabilities.ChangeRequests));
    }

    private sealed class StubReader(
        int contractVersion = HostedSourceProviderContract.CurrentVersion,
        int candidateCount = 0) : IHostedSourceProviderReader
    {
        public int ContractVersion => contractVersion;

        public SourceProviderKind Provider => SourceProviderKind.GitHub;

        public SourceProviderCapabilities Capabilities =>
            SourceProviderCapabilities.Issues;

        public HostedSourceDiscoveryResult Read(
            string repositoryRoot,
            HostedRepositoryDescriptor repository) =>
            new(
                Enumerable.Range(0, candidateCount)
                    .Select(index => new SourceDiscoveryCandidate(
                        SourceArtifactKind.Issue,
                        $"Issue {index}",
                        null,
                        "issue",
                        "fixed",
                        false,
                        $"issue:{index}",
                        "Contract test",
                        1))
                    .ToArray(),
                [],
                candidateCount,
                0,
                0,
                0);
    }
}
