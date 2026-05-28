using System;
using System.Collections.Generic;
using System.Linq;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public sealed class GameAssetLayoutRegistry(IEnumerable<IGameAssetLayout> layouts) : IGameAssetLayoutRegistry
{
    private readonly IReadOnlyDictionary<GameId, IGameAssetLayout> _layouts =
        layouts.ToDictionary(layout => layout.GameId);

    public IGameAssetLayout Get(GameId gameId) =>
        _layouts.TryGetValue(gameId, out var layout)
            ? layout
            : throw new NotSupportedException($"Unsupported game: {gameId}");
}
