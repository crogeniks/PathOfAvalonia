using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class GameAssetServiceTests
{
    [Fact]
    public void SharesTreeAndSpriteLoadsForTheSameGameVersion()
    {
        var games = new GameRegistry();
        var service = new GameAssetService(
            games,
            new GameAssetLayoutRegistry([new Poe1GameAssetLayout(), new Poe2GameAssetLayout()]));
        var game = games.Get(GameId.PathOfExile2);

        var firstTree = service.LoadTreeAsync(game);
        var secondTree = service.LoadTreeAsync(game, game.DefaultTreeVersion);
        var firstSprites = service.LoadSpritesAsync(game);
        var secondSprites = service.LoadSpritesAsync(game, game.DefaultTreeVersion);

        Assert.Same(firstTree, secondTree);
        Assert.Same(firstSprites, secondSprites);
    }
}
