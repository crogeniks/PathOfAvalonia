using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

/// <summary>
/// Creates the mutable state and view models that belong to one open game workspace.
/// </summary>
public interface IGameWorkspaceFactory
{
    Task<GameWorkspaceViewModel> CreateAsync(
        GameDefinition game,
        string treeVersion,
        Func<GameDefinition, string, Task> switchTreeVersion,
        IRelayCommand backToLandingCommand);
}

public sealed class GameWorkspaceFactory(
    IGameAssetService assets,
    IGameAssetLayoutRegistry assetLayouts,
    IBuildPlannerExportService buildPlannerExportService,
    IBuildPlannerImportService buildPlannerImportService,
    IStorageProviderAccessor storageProviderAccessor) : IGameWorkspaceFactory
{
    public async Task<GameWorkspaceViewModel> CreateAsync(
        GameDefinition game,
        string treeVersion,
        Func<GameDefinition, string, Task> switchTreeVersion,
        IRelayCommand backToLandingCommand)
    {
        var treeTask = assets.LoadTreeAsync(game, treeVersion);
        var spritesTask = assets.LoadSpritesAsync(game, treeVersion);
        var timelessJewelDataTask = assets.LoadTimelessJewelDataAsync(game, treeVersion);
        await Task.WhenAll(treeTask, spritesTask, timelessJewelDataTask);

        var tree = await treeTask;
        var sprites = await spritesTask;
        var timelessJewelData = await timelessJewelDataTask;
        var spec = new PassiveSpec(tree, tree.Classes, game.FeatureFlags, timelessJewelData);
        var equipment = new EquipmentViewModel(spec);
        var state = new BuildWorkspaceState(
            game,
            spec,
            sprites,
            new PassiveTreeViewModel(spec),
            equipment);
        var treeSelection = new TreeSelectionViewModel(state);
        var importExport = new BuildImportExportViewModel(
            state,
            game.ImportStrategy,
            new BuildPlannerFileService(storageProviderAccessor, buildPlannerExportService, buildPlannerImportService));

        return new GameWorkspaceViewModel(
            state,
            treeSelection,
            importExport,
            new TreeImageAssetResolver(game, assets, assetLayouts, treeVersion),
            assets,
            switchTreeVersion,
            backToLandingCommand);
    }
}
