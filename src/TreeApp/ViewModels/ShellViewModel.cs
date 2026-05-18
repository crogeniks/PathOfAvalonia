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
    private readonly IGameAssetService _assets;
    private readonly IUserSettingsService _settings;
    private readonly IPobCalculationService _pobCalculationService;

    public ShellViewModel(GameRegistry games, IGameAssetService assets, IUserSettingsService settings)
        : this(games, assets, settings, NullPobCalculationService.Instance)
    {
    }

    public ShellViewModel(
        GameRegistry games,
        IGameAssetService assets,
        IUserSettingsService settings,
        IPobCalculationService pobCalculationService)
    {
        _games = games;
        _assets = assets;
        _settings = settings;
        _pobCalculationService = pobCalculationService;
        Games = _games.Games.Select(g => new GameChoiceViewModel(g, settings.LastGameId == g.Id)).ToArray();
        ResetBackendSettingsFields();

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
    [ObservableProperty] private bool _isBackendSettingsOpen;
    [ObservableProperty] private bool _enablePobBackend;
    [ObservableProperty] private string _poe1PobPath = string.Empty;
    [ObservableProperty] private string _poe2PobPath = string.Empty;
    [ObservableProperty] private string _luaExecutablePath = string.Empty;
    [ObservableProperty] private string _pobBackendTimeoutSeconds = "120";

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

    [RelayCommand]
    private void OpenBackendSettings()
    {
        ResetBackendSettingsFields();
        IsBackendSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseBackendSettings()
    {
        ResetBackendSettingsFields();
        IsBackendSettingsOpen = false;
    }

    [RelayCommand]
    private void SaveBackendSettings()
    {
        if (!int.TryParse(PobBackendTimeoutSeconds, out var timeoutSeconds))
        {
            StatusMessage = "PoB backend timeout must be a whole number of seconds.";
            return;
        }

        timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 600);
        _settings.EnablePobBackend = EnablePobBackend;
        _settings.Poe1PobPath = EmptyToNull(Poe1PobPath);
        _settings.Poe2PobPath = EmptyToNull(Poe2PobPath);
        _settings.LuaExecutablePath = EmptyToNull(LuaExecutablePath);
        _settings.PobBackendTimeoutSeconds = timeoutSeconds;
        _settings.Save();

        PobBackendTimeoutSeconds = timeoutSeconds.ToString();
        IsBackendSettingsOpen = false;
        StatusMessage = "PoB backend settings saved.";
    }

    private void OpenWorkspace(GameDefinition game)
    {
        var tree = _assets.LoadTree(game);
        var sprites = _assets.LoadSprites(game);
        var spec = new PassiveSpec(tree, tree.Classes, game.FeatureFlags);
        var equipment = new EquipmentViewModel();
        var treePanel = new MainWindowViewModel(spec, game.ImportStrategy, equipment, _pobCalculationService);
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
            new TreeImageAssetResolver(game),
            BackToLandingCommand,
            OpenBackendSettingsCommand);
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

    private void ResetBackendSettingsFields()
    {
        EnablePobBackend = _settings.EnablePobBackend;
        Poe1PobPath = _settings.Poe1PobPath ?? string.Empty;
        Poe2PobPath = _settings.Poe2PobPath ?? string.Empty;
        LuaExecutablePath = _settings.LuaExecutablePath ?? string.Empty;
        PobBackendTimeoutSeconds = Math.Clamp(_settings.PobBackendTimeoutSeconds, 1, 600).ToString();
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class NullPobCalculationService : IPobCalculationService
    {
        public static NullPobCalculationService Instance { get; } = new();

        public Task<PathOfAvalonia.TreeDomain.Import.ImportedBuildMetrics> CalculateAsync(
            GameId gameId,
            PathOfAvalonia.TreeDomain.Import.ImportedBuild build,
            System.Threading.CancellationToken cancellationToken) =>
            Task.FromResult(build.Metrics);
    }
}
