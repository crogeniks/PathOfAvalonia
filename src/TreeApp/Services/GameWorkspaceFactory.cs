using System;
using System.Linq;
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
        var atlasVersion = game.AtlasTreeVersions.Contains(treeVersion, StringComparer.Ordinal)
            ? treeVersion
            : game.AtlasTreeVersions.LastOrDefault();
        var atlasTreeTask = atlasVersion is null ? null : assets.LoadAtlasTreeAsync(game, atlasVersion);
        var atlasSpritesTask = atlasVersion is null ? null : assets.LoadAtlasSpritesAsync(game, atlasVersion);
        await Task.WhenAll(treeTask, spritesTask, timelessJewelDataTask);
        if (atlasTreeTask is not null && atlasSpritesTask is not null)
        {
            try
            {
                await Task.WhenAll(atlasTreeTask, atlasSpritesTask);
            }
            catch
            {
                // Atlas is an optional PoE1 workspace. A missing or malformed
                // Atlas bundle must not prevent the character build from opening.
                atlasTreeTask = null;
                atlasSpritesTask = null;
            }
        }

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
        AtlasTreeViewModel? atlas = null;
        if (atlasVersion is not null && atlasTreeTask is not null && atlasSpritesTask is not null)
        {
            atlas = new AtlasTreeViewModel(
                game,
                await atlasTreeTask,
                await atlasSpritesTask,
                new AtlasTreeImageAssetResolver(game, assets, assetLayouts, atlasVersion),
                assets,
                assetLayouts);
        }

        return new GameWorkspaceViewModel(
            state,
            treeSelection,
            importExport,
            new TreeImageAssetResolver(game, assets, assetLayouts, treeVersion),
            assets,
            switchTreeVersion,
            backToLandingCommand,
            atlas);
    }
}
