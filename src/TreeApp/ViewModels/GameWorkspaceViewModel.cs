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
    private readonly int _initialClassIndex;
    private readonly int _initialAllocatedCount;

    public GameWorkspaceViewModel(
        GameWorkspace workspace,
        MainWindowViewModel treePanel,
        ITreeImageAssetResolver imageResolver,
        IRelayCommand backToLandingCommand)
    {
        Workspace = workspace;
        TreePanel = treePanel;
        ImageResolver = imageResolver;
        BackToLandingCommand = backToLandingCommand;
        _initialClassIndex = workspace.Spec.SelectedClassIndex;
        _initialAllocatedCount = workspace.Spec.AllocatedNodes.Count;
        workspace.Spec.SpecChanged += () => OnPropertyChanged(nameof(IsDirty));
    }

    public GameWorkspace Workspace { get; }
    public MainWindowViewModel TreePanel { get; }
    public ITreeImageAssetResolver ImageResolver { get; }
    public IRelayCommand BackToLandingCommand { get; }
    public string GameName => Workspace.Game.DisplayName;
    public string TreeVersion => Workspace.Tree.Version;
    public bool SupportsEquipment => Workspace.Game.FeatureFlags.SupportsEquipmentImport;
    public bool IsDirty =>
        Workspace.Spec.SelectedClassIndex != _initialClassIndex
        || Workspace.Spec.SelectedAscendancyIndex != 0
        || Workspace.Spec.AllocatedNodes.Count != _initialAllocatedCount
        || Workspace.Spec.ActiveSubgraphs.Count > 0
        || Workspace.Spec.SocketedJewels.Count > 0;
}

