using CommunityToolkit.Mvvm.ComponentModel;

namespace Blueprints.App.Models;

public sealed class SourceImportProposal : ObservableObject
{
    private bool _isIncluded;
    private string _title;
    private string _description;
    private string _itemTypeId;
    private string _categoryId;
    private bool _isDone;
    private WorkspaceVersionCard? _targetVersion;

    public SourceImportProposal(
        SourceDiscoveryCandidate candidate,
        WorkspaceVersionCard? targetVersion,
        bool isDuplicate)
    {
        Candidate = candidate;
        _isIncluded = !isDuplicate;
        _title = candidate.Title;
        _description = candidate.Description ?? string.Empty;
        _itemTypeId = candidate.SuggestedItemTypeId;
        _categoryId = candidate.SuggestedCategoryId;
        _isDone = candidate.IsDone;
        _targetVersion = targetVersion;
        IsDuplicate = isDuplicate;
    }

    public SourceDiscoveryCandidate Candidate { get; }

    public SourceArtifactKind Kind => Candidate.Kind;

    public string SourceReference => Candidate.SourceReference;

    public string SourceContext => Candidate.SourceContext;

    public string ProviderReferenceSummary =>
        Candidate.ProviderReference?.DisplaySummary ?? "Unclassified source reference";

    public string ConfidenceSummary => $"{Candidate.Confidence:P0} confidence";

    public bool IsDuplicate { get; }

    public string DuplicateSummary => IsDuplicate ? "Possible duplicate" : "New proposal";

    public bool IsIncluded
    {
        get => _isIncluded;
        set => SetProperty(ref _isIncluded, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string ItemTypeId
    {
        get => _itemTypeId;
        set => SetProperty(ref _itemTypeId, value);
    }

    public string CategoryId
    {
        get => _categoryId;
        set => SetProperty(ref _categoryId, value);
    }

    public bool IsDone
    {
        get => _isDone;
        set => SetProperty(ref _isDone, value);
    }

    public WorkspaceVersionCard? TargetVersion
    {
        get => _targetVersion;
        set => SetProperty(ref _targetVersion, value);
    }
}
