using System;
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
    private readonly IGameWorkspaceFactory _workspaceFactory;
    private readonly IUserSettingsService _settings;
    private readonly IBuildLibraryService? _buildLibrary;
    private int _workspaceLoadRequest;

    public ShellViewModel(
        GameRegistry games,
        IGameWorkspaceFactory workspaceFactory,
        IUserSettingsService settings,
        IBuildLibraryService? buildLibrary = null)
    {
        _games = games;
        _workspaceFactory = workspaceFactory;
        _settings = settings;
        _buildLibrary = buildLibrary;
        Games = _games.Games.Select(g => new GameChoiceViewModel(g, settings.LastGameId == g.Id)).ToArray();

        if (settings.LastBuildId is { } lastBuildId && _buildLibrary is not null)
        {
            _ = OpenSavedBuildAsync(lastBuildId, fallBackToLastGame: true);
        }
        else if (settings.LastGameId is { } lastGame && _games.TryGet(lastGame, out var game))
        {
            _ = OpenWorkspaceAsync(game, game.DefaultTreeVersion, "Could not reopen last game.");
        }
        else
        {
            CurrentPage = ShellPage.Landing;
        }
    }

    public IReadOnlyList<GameChoiceViewModel> Games { get; }

    [ObservableProperty] public partial ShellPage CurrentPage { get; set; }
    [ObservableProperty] public partial GameWorkspaceViewModel? ActiveWorkspace { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsConfirmingGameChange { get; set; }

    [RelayCommand]
    private Task SelectGame(GameId gameId)
    {
        var game = _games.Get(gameId);
        return OpenWorkspaceAsync(game, game.DefaultTreeVersion, "Could not open the selected game.");
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

    private async Task OpenWorkspaceAsync(
        GameDefinition game,
        string treeVersion,
        string errorMessage,
        SavedBuild? savedBuild = null)
    {
        var request = ++_workspaceLoadRequest;
        StatusMessage = "Loading tree data…";
        CurrentPage = ShellPage.Landing;
        try
        {
            var workspace = await _workspaceFactory.CreateAsync(
                game,
                treeVersion,
                SwitchTreeVersionAsync,
                BackToLandingCommand);
            workspace.SetOpenSavedBuildHandler(id => OpenSavedBuildAsync(id, fallBackToLastGame: false));
            if (savedBuild is not null)
            {
                await workspace.RestoreSavedBuildAsync(savedBuild);
            }
            if (request != _workspaceLoadRequest)
            {
                return;
            }
            ActiveWorkspace = workspace;
            CurrentPage = ShellPage.Workspace;
            StatusMessage = string.Empty;
            _settings.LastGameId = game.Id;
            _settings.LastBuildId = savedBuild?.Id;
            _settings.Save();
            foreach (var choice in Games)
            {
                choice.IsLastUsed = choice.Id == game.Id;
            }
        }
        catch
        {
            if (request == _workspaceLoadRequest)
            {
                StatusMessage = errorMessage;
                CurrentPage = ShellPage.Landing;
            }
        }
    }

    private Task SwitchTreeVersionAsync(GameDefinition game, string treeVersion) =>
        OpenWorkspaceWithClearedBuildAsync(game, treeVersion);

    private async Task OpenWorkspaceWithClearedBuildAsync(GameDefinition game, string treeVersion)
    {
        await OpenWorkspaceAsync(game, treeVersion, "Could not load the selected tree version.");
    }

    private async Task OpenSavedBuildAsync(Guid id, bool fallBackToLastGame)
    {
        if (_buildLibrary is null)
        {
            return;
        }

        try
        {
            var savedBuild = await _buildLibrary.LoadAsync(id);
            if (savedBuild is null || !_games.TryGet(savedBuild.GameId, out var game))
            {
                throw new InvalidOperationException("The saved build could not be found.");
            }
            var treeVersion = game.TreeVersions.Contains(savedBuild.TreeVersion)
                ? savedBuild.TreeVersion
                : game.DefaultTreeVersion;
            await OpenWorkspaceAsync(game, treeVersion, "Could not open the saved build.", savedBuild);
        }
        catch
        {
            _settings.LastBuildId = null;
            _settings.Save();
            if (fallBackToLastGame
                && _settings.LastGameId is { } lastGame
                && _games.TryGet(lastGame, out var fallbackGame))
            {
                await OpenWorkspaceAsync(
                    fallbackGame,
                    fallbackGame.DefaultTreeVersion,
                    "The last saved build could not be reopened.");
            }
            else
            {
                StatusMessage = "The saved build could not be opened.";
            }
        }
    }

    private void ReturnToLanding()
    {
        _workspaceLoadRequest++;
        ActiveWorkspace = null;
        CurrentPage = ShellPage.Landing;
    }
}
