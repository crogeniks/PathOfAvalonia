using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.ViewModels;

public sealed class GameWorkspace
{
    public required GameDefinition Game { get; init; }
    public required TreeModel Tree { get; init; }
    public required SpriteMap Sprites { get; init; }
    public required ClassCatalog Classes { get; init; }
    public required PassiveSpec Spec { get; init; }
    public required PassiveTreeViewModel TreeViewModel { get; init; }
    public required EquipmentViewModel Equipment { get; init; }
}

public sealed partial class GameWorkspaceViewModel : ObservableObject
{
    private const string NoDiffVersion = "None";
    private readonly IGameAssetService _assets;
    private readonly Action<GameDefinition, string> _switchTreeVersion;
    private readonly int _initialClassIndex;
    private readonly int _initialAllocatedCount;

    public GameWorkspaceViewModel(
        GameWorkspace workspace,
        MainWindowViewModel treePanel,
        ITreeImageAssetResolver imageResolver,
        IGameAssetService assets,
        Action<GameDefinition, string> switchTreeVersion,
        IRelayCommand backToLandingCommand,
        IRelayCommand openBackendSettingsCommand)
    {
        Workspace = workspace;
        TreePanel = treePanel;
        ImageResolver = imageResolver;
        _assets = assets;
        _switchTreeVersion = switchTreeVersion;
        BackToLandingCommand = backToLandingCommand;
        OpenBackendSettingsCommand = openBackendSettingsCommand;
        _selectedTreeVersion = workspace.Tree.Version;
        TreeVersionOptions = workspace.Game.TreeVersions;
        DiffTreeVersionOptions = [NoDiffVersion, .. workspace.Game.TreeVersions.Where(version => version != workspace.Tree.Version)];
        _initialClassIndex = workspace.Spec.SelectedClassIndex;
        _initialAllocatedCount = workspace.Spec.AllocatedNodes.Count;
        workspace.Spec.SpecChanged += () => OnPropertyChanged(nameof(IsDirty));
    }

    public GameWorkspace Workspace { get; }
    public MainWindowViewModel TreePanel { get; }
    public ITreeImageAssetResolver ImageResolver { get; }
    public IRelayCommand BackToLandingCommand { get; }
    public IRelayCommand OpenBackendSettingsCommand { get; }
    public string GameName => Workspace.Game.DisplayName;
    public string TreeVersion => Workspace.Tree.Version;
    public bool HasTreeVersionOptions => TreeVersionOptions.Count > 1;
    public bool HasDiffVersionOptions => DiffTreeVersionOptions.Count > 1;
    public IReadOnlyList<string> TreeVersionOptions { get; }
    public IReadOnlyList<string> DiffTreeVersionOptions { get; }
    public string DiffSummary => TreePanel.TreeViewModel.Diff.HasChanges
        ? $"+{TreePanel.TreeViewModel.Diff.AddedCount} ~{TreePanel.TreeViewModel.Diff.ChangedCount} -{TreePanel.TreeViewModel.Diff.RemovedCount}"
        : string.Empty;
    public bool SupportsEquipment => Workspace.Game.FeatureFlags.SupportsEquipmentImport;
    public bool IsDirty =>
        Workspace.Spec.SelectedClassIndex != _initialClassIndex
        || Workspace.Spec.SelectedAscendancyIndex != 0
        || Workspace.Spec.AllocatedNodes.Count != _initialAllocatedCount
        || Workspace.Spec.ActiveSubgraphs.Count > 0
        || Workspace.Spec.SocketedJewels.Count > 0
        || Workspace.Spec.AttributeOverrides.Count > 0;

    [ObservableProperty] private string _selectedTreeVersion = string.Empty;
    [ObservableProperty] private string _selectedDiffTreeVersion = NoDiffVersion;

    partial void OnSelectedTreeVersionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == Workspace.Tree.Version)
        {
            return;
        }

        _switchTreeVersion(Workspace.Game, value);
    }

    partial void OnSelectedDiffTreeVersionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == NoDiffVersion || value == Workspace.Tree.Version)
        {
            TreePanel.TreeViewModel.SetDiff(TreeDiff.Empty);
            OnPropertyChanged(nameof(DiffSummary));
            return;
        }

        var baseline = _assets.LoadTree(Workspace.Game, value);
        TreePanel.TreeViewModel.SetDiff(TreeDiff.Compare(Workspace.Tree, baseline));
        OnPropertyChanged(nameof(DiffSummary));
    }
}
