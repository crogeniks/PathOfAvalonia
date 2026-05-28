using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.ViewModels;

public enum ShellPage
{
    Landing,
    Workspace,
}

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly GameRegistry _games;
    private readonly IGameAssetService _assets;
    private readonly IUserSettingsService _settings;
    private readonly IBuildPlannerExportService _buildPlannerExportService;
    private readonly IStorageProviderAccessor _storageProviderAccessor;

    public ShellViewModel(GameRegistry games, IGameAssetService assets, IUserSettingsService settings)
        : this(
            games,
            assets,
            settings,
            NullBuildPlannerExportService.Instance,
            NullStorageProviderAccessor.Instance)
    {
    }

    public ShellViewModel(
        GameRegistry games,
        IGameAssetService assets,
        IUserSettingsService settings,
        IBuildPlannerExportService buildPlannerExportService,
        IStorageProviderAccessor storageProviderAccessor)
    {
        _games = games;
        _assets = assets;
        _settings = settings;
        _buildPlannerExportService = buildPlannerExportService;
        _storageProviderAccessor = storageProviderAccessor;
        Games = _games.Games.Select(g => new GameChoiceViewModel(g, settings.LastGameId == g.Id)).ToArray();

        if (settings.LastGameId is { } lastGame && _games.TryGet(lastGame, out var game))
        {
            try
            {
                OpenWorkspace(game, game.DefaultTreeVersion);
                return;
            }
            catch
            {
                StatusMessage = "Could not reopen last game.";
            }
        }

        CurrentPage = ShellPage.Landing;
    }

    public IReadOnlyList<GameChoiceViewModel> Games { get; }

    [ObservableProperty] public partial ShellPage CurrentPage { get; set; }
    [ObservableProperty] public partial GameWorkspaceViewModel? ActiveWorkspace { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsConfirmingGameChange { get; set; }

    [RelayCommand]
    private void SelectGame(GameId gameId)
    {
        var game = _games.Get(gameId);
        OpenWorkspace(game, game.DefaultTreeVersion);
    }

    [RelayCommand]
    private void BackToLanding()
    {
        if (ActiveWorkspace?.IsDirty == true)
        {
            IsConfirmingGameChange = true;
            return;
        }
        ReturnToLanding();
    }

    [RelayCommand]
    private void ConfirmGameChange()
    {
        IsConfirmingGameChange = false;
        ReturnToLanding();
    }

    [RelayCommand]
    private void CancelGameChange()
    {
        IsConfirmingGameChange = false;
    }

    private void OpenWorkspace(GameDefinition game, string treeVersion)
    {
        var tree = _assets.LoadTree(game, treeVersion);
        var sprites = _assets.LoadSprites(game, treeVersion);
        var spec = new PassiveSpec(tree, tree.Classes, game.FeatureFlags);
        var equipment = new EquipmentViewModel();
        var treePanel = new MainWindowViewModel(
            spec,
            game.ImportStrategy,
            equipment,
            _buildPlannerExportService,
            _storageProviderAccessor);
        var workspace = new GameWorkspace
        {
            Game = game,
            Tree = tree,
            Sprites = sprites,
            Classes = tree.Classes,
            Spec = spec,
            TreeViewModel = treePanel.TreeViewModel,
            Equipment = equipment,
        };
        ActiveWorkspace = new GameWorkspaceViewModel(
            workspace,
            treePanel,
            new TreeImageAssetResolver(game, treeVersion),
            _assets,
            OpenWorkspace,
            BackToLandingCommand);
        CurrentPage = ShellPage.Workspace;
        _settings.LastGameId = game.Id;
        _settings.Save();
        foreach (var choice in Games)
        {
            choice.IsLastUsed = choice.Id == game.Id;
        }
    }

    private void ReturnToLanding()
    {
        ActiveWorkspace = null;
        CurrentPage = ShellPage.Landing;
    }

    private sealed class NullBuildPlannerExportService : IBuildPlannerExportService
    {
        public static NullBuildPlannerExportService Instance { get; } = new();

        public Task<BuildPlannerExportFileResult?> ExportAsync(
            Avalonia.Platform.Storage.IStorageProvider storageProvider,
            TreeModel tree,
            ClassCatalog classes,
            PathOfAvalonia.TreeDomain.Import.ImportedBuild build,
            System.Threading.CancellationToken cancellationToken) =>
            Task.FromResult<BuildPlannerExportFileResult?>(null);
    }

    private sealed class NullStorageProviderAccessor : IStorageProviderAccessor
    {
        public static NullStorageProviderAccessor Instance { get; } = new();
        public Avalonia.Platform.Storage.IStorageProvider? StorageProvider { get; set; }
    }
}
