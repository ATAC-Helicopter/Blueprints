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

    [Fact]
    public void CanvasNodeSelection_ConnectsInspectorToTheRealWorkspaceObjects()
    {
        var viewModel = new MainWindowViewModel();
        var version = Assert.Single(viewModel.Versions, candidate => candidate.Items.Count > 0);
        var item = version.Items[0];

        viewModel.SelectItemNodeCommand.Execute(item);

        Assert.Equal(version.VersionId, viewModel.SelectedVersion?.VersionId);
        Assert.Equal(item.ItemId, viewModel.SelectedItem?.ItemId);
        Assert.True(viewModel.HasSelectedItem);
        Assert.Contains(item.ItemKey, viewModel.InspectorSelectionSummary, StringComparison.Ordinal);

        viewModel.SelectVersionNodeCommand.Execute(version);

        Assert.Equal(version.VersionId, viewModel.SelectedVersion?.VersionId);
        Assert.Null(viewModel.SelectedItem);
        Assert.False(viewModel.HasSelectedItem);
        Assert.Equal($"VERSION / {version.Name}", viewModel.InspectorSelectionSummary);
    }

    [Fact]
    public void BeginNewItem_ClearsTheInspectorAndKeepsTheSelectedConnectionTarget()
    {
        var viewModel = new MainWindowViewModel();
        var version = Assert.Single(viewModel.Versions, candidate => candidate.Items.Count > 0);
        viewModel.SelectItemNodeCommand.Execute(version.Items[0]);

        viewModel.BeginNewItemCommand.Execute(null);

        Assert.Equal(version.VersionId, viewModel.SelectedVersion?.VersionId);
        Assert.Null(viewModel.SelectedItem);
        Assert.Empty(viewModel.ItemEditorTitle);
        Assert.Contains(version.Name, viewModel.WorkspaceMessage, StringComparison.Ordinal);
    }
}
