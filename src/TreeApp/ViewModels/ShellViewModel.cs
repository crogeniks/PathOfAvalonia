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
    private readonly IBuildPlannerExportService _buildPlannerExportService;
    private readonly IStorageProviderAccessor _storageProviderAccessor;

    public ShellViewModel(GameRegistry games, IGameAssetService assets, IUserSettingsService settings)
        : this(
            games,
            assets,
            settings,
            NullPobCalculationService.Instance,
            NullBuildPlannerExportService.Instance,
            NullStorageProviderAccessor.Instance)
    {
    }

    public ShellViewModel(
        GameRegistry games,
        IGameAssetService assets,
        IUserSettingsService settings,
        IPobCalculationService pobCalculationService)
        : this(
            games,
            assets,
            settings,
            pobCalculationService,
            NullBuildPlannerExportService.Instance,
            NullStorageProviderAccessor.Instance)
    {
    }

    public ShellViewModel(
        GameRegistry games,
        IGameAssetService assets,
        IUserSettingsService settings,
        IPobCalculationService pobCalculationService,
        IBuildPlannerExportService buildPlannerExportService,
        IStorageProviderAccessor storageProviderAccessor)
    {
        _games = games;
        _assets = assets;
        _settings = settings;
        _pobCalculationService = pobCalculationService;
        _buildPlannerExportService = buildPlannerExportService;
        _storageProviderAccessor = storageProviderAccessor;
        Games = _games.Games.Select(g => new GameChoiceViewModel(g, settings.LastGameId == g.Id)).ToArray();
        ResetBackendSettingsFields();

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
    [ObservableProperty] public partial bool IsBackendSettingsOpen { get; set; }
    [ObservableProperty] public partial bool EnablePobBackend { get; set; }
    [ObservableProperty] public partial string Poe1PobPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string Poe2PobPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string LuaExecutablePath { get; set; } = string.Empty;
    [ObservableProperty] public partial string PobBackendTimeoutSeconds { get; set; } = "120";

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
            _pobCalculationService,
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
