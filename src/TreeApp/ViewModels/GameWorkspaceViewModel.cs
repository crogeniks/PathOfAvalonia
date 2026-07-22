using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.ViewModels;

public sealed partial class GameWorkspaceViewModel : ObservableObject
{
    private const string NoDiffVersion = "None";
    private readonly IGameAssetService _assets;
    private readonly Func<GameDefinition, string, Task> _switchTreeVersion;
    private int _diffLoadRequest;
    private readonly int _initialClassIndex;
    private readonly int _initialAllocatedCount;

    public GameWorkspaceViewModel(
        BuildWorkspaceState state,
        TreeSelectionViewModel treeSelection,
        BuildImportExportViewModel importExport,
        ITreeImageAssetResolver imageResolver,
        IGameAssetService assets,
        Func<GameDefinition, string, Task> switchTreeVersion,
        IRelayCommand backToLandingCommand,
        AtlasTreeViewModel? atlas = null)
    {
        State = state;
        TreeSelection = treeSelection;
        ImportExport = importExport;
        ImageResolver = imageResolver;
        _assets = assets;
        _switchTreeVersion = switchTreeVersion;
        BackToLandingCommand = backToLandingCommand;
        Atlas = atlas;
        SelectedTreeVersion = state.Spec.Tree.Version;
        TreeVersionOptions = state.Game.TreeVersions;
        DiffTreeVersionOptions = [NoDiffVersion, .. state.Game.TreeVersions.Where(version => version != state.Spec.Tree.Version)];
        _initialClassIndex = state.Spec.SelectedClassIndex;
        _initialAllocatedCount = state.Spec.AllocatedNodes.Count;
        state.Spec.SpecChanged += () => OnPropertyChanged(nameof(IsDirty));
        state.Equipment.EquipmentChanged += () => OnPropertyChanged(nameof(IsDirty));
        if (Atlas is not null)
        {
            Atlas.StateChanged += () => OnPropertyChanged(nameof(IsDirty));
        }
    }

    public BuildWorkspaceState State { get; }
    public TreeSelectionViewModel TreeSelection { get; }
    public BuildImportExportViewModel ImportExport { get; }
    [Obsolete("Use State. This projection does not own separate workspace state.")]
    public BuildWorkspaceState Workspace => State;
    [Obsolete("Use ImportExport or TreeSelection.")]
    public BuildImportExportViewModel TreePanel => ImportExport;
    public ITreeImageAssetResolver ImageResolver { get; }
    public IRelayCommand BackToLandingCommand { get; }
    public AtlasTreeViewModel? Atlas { get; }
    public bool HasAtlasTree => Atlas is not null;
    public string GameName => State.Game.DisplayName;
    public string TreeVersion => State.Spec.Tree.Version;
    // Keep the current PoE1 tree visible even while it has only one version, so the
    // version-selection affordance is already in place when the 3.29 tree arrives.
    public bool HasTreeVersionOptions => State.Game.Id == GameId.PathOfExile1 || TreeVersionOptions.Count > 1;
    public bool HasDiffVersionOptions => DiffTreeVersionOptions.Count > 1;
    public IReadOnlyList<string> TreeVersionOptions { get; }
    public IReadOnlyList<string> DiffTreeVersionOptions { get; }
    public string DiffSummary => State.Tree.Diff.HasChanges
        ? $"+{State.Tree.Diff.AddedCount} ~{State.Tree.Diff.ChangedCount} -{State.Tree.Diff.RemovedCount}"
        : string.Empty;
    public bool SupportsEquipment => State.Game.FeatureFlags.SupportsEquipmentImport;
    public bool IsDirty =>
        State.Spec.SelectedClassIndex != _initialClassIndex
        || State.Spec.SelectedAscendancyIndex != 0
        || State.Spec.AllocatedNodes.Count != _initialAllocatedCount
        || State.Spec.ActiveSubgraphs.Count > 0
        || State.Spec.SocketedJewels.Count > 0
        || State.Spec.AttributeOverrides.Count > 0
        || State.Equipment.IsDirty
        || Atlas?.IsDirty == true;

    [ObservableProperty] public partial string SelectedTreeVersion { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedDiffTreeVersion { get; set; } = NoDiffVersion;

    partial void OnSelectedTreeVersionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == State.Spec.Tree.Version)
        {
            return;
        }

        _ = _switchTreeVersion(State.Game, value);
    }

    async partial void OnSelectedDiffTreeVersionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == NoDiffVersion || value == State.Spec.Tree.Version)
        {
            _diffLoadRequest++;
            State.Tree.SetDiff(TreeDiff.Empty);
            OnPropertyChanged(nameof(DiffSummary));
            return;
        }

        var request = ++_diffLoadRequest;
        try
        {
            var baseline = await _assets.LoadTreeAsync(State.Game, value);
            if (request != _diffLoadRequest || SelectedDiffTreeVersion != value)
            {
                return;
            }

            State.Tree.SetDiff(TreeDiff.Compare(State.Spec.Tree, baseline));
            OnPropertyChanged(nameof(DiffSummary));
        }
        catch
        {
            if (request == _diffLoadRequest)
            {
                State.Tree.SetDiff(TreeDiff.Empty);
                OnPropertyChanged(nameof(DiffSummary));
            }
        }
    }

}
