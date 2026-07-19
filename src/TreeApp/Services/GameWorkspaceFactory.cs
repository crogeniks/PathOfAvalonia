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
        await Task.WhenAll(treeTask, spritesTask);

        var tree = await treeTask;
        var sprites = await spritesTask;
        var spec = new PassiveSpec(tree, tree.Classes, game.FeatureFlags);
        var equipment = new EquipmentViewModel();
        var treePanel = new MainWindowViewModel(
            spec,
            game.ImportStrategy,
            equipment,
            buildPlannerExportService,
            buildPlannerImportService,
            storageProviderAccessor);
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

        return new GameWorkspaceViewModel(
            workspace,
            treePanel,
            new TreeImageAssetResolver(game, assets, assetLayouts, treeVersion),
            assets,
            switchTreeVersion,
            backToLandingCommand);
    }
}
