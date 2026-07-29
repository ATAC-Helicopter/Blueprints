using Blueprints.App.Models;
using Blueprints.App.ViewModels;

namespace Blueprints.Tests;

public sealed class MainWindowNavigationTests
{
    [Fact]
    public void NavigationCommands_SelectExactlyOneWorkspaceSection()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.NavigateToReleasesCommand.Execute(null);

        Assert.Equal(WorkspaceSection.Releases, viewModel.SelectedWorkspaceSection);
        Assert.True(viewModel.IsReleasesSelected);
        Assert.False(viewModel.IsOverviewSelected);
        Assert.False(viewModel.IsTeamSelected);
        Assert.False(viewModel.IsSyncSelected);
        Assert.False(viewModel.IsTrustSelected);
        Assert.False(viewModel.IsIntegrationsSelected);
        Assert.Equal("Release drafting board", viewModel.SelectedWorkspaceSectionTitle);
    }

    [Fact]
    public void NavigationCommands_CanTraverseTheProjectMap()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.NavigateToTeamCommand.Execute(null);
        Assert.True(viewModel.IsTeamSelected);

        viewModel.NavigateToSyncCommand.Execute(null);
        Assert.True(viewModel.IsSyncSelected);

        viewModel.NavigateToTrustCommand.Execute(null);
        Assert.True(viewModel.IsTrustSelected);

        viewModel.NavigateToIntegrationsCommand.Execute(null);
        Assert.True(viewModel.IsIntegrationsSelected);

        viewModel.NavigateToOverviewCommand.Execute(null);
        Assert.True(viewModel.IsOverviewSelected);
    }
}
