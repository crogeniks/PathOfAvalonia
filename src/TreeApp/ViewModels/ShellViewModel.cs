using System.Collections.Generic;
using System.Linq;
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

    public ShellViewModel(GameRegistry games, IGameAssetService assets, IUserSettingsService settings)
    {
        _games = games;
        _assets = assets;
        _settings = settings;
        Games = _games.Games.Select(g => new GameChoiceViewModel(g, settings.LastGameId == g.Id)).ToArray();

        if (settings.LastGameId is { } lastGame && _games.TryGet(lastGame, out var game))
        {
            try
            {
                OpenWorkspace(game);
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

    [ObservableProperty] private ShellPage _currentPage;
    [ObservableProperty] private GameWorkspaceViewModel? _activeWorkspace;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isConfirmingGameChange;

    [RelayCommand]
    private void SelectGame(GameId gameId)
    {
        OpenWorkspace(_games.Get(gameId));
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

    private void OpenWorkspace(GameDefinition game)
    {
        var tree = _assets.LoadTree(game);
        var sprites = _assets.LoadSprites(game);
        var spec = new PassiveSpec(tree, tree.Classes, game.FeatureFlags);
        var equipment = new EquipmentViewModel();
        var treePanel = new MainWindowViewModel(spec, game.ImportStrategy, equipment);
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
        ActiveWorkspace = new GameWorkspaceViewModel(workspace, treePanel, new TreeImageAssetResolver(game), BackToLandingCommand);
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
}
