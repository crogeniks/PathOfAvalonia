using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IGameAssetLayoutRegistry
{
    IGameAssetLayout Get(GameId gameId);
}
