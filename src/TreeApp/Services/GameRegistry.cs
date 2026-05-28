using System.Collections.Generic;
using System.Linq;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Poe1;
using PathOfAvalonia.TreeDomain.Poe2;

namespace PathOfAvalonia.TreeApp.Services;

public sealed class GameRegistry
{
    private readonly IReadOnlyList<GameDefinition> _games =
    [
        CreatePoe1(),
        CreatePoe2(),
    ];

    public IReadOnlyList<GameDefinition> Games => _games;

    public GameDefinition Get(GameId id) =>
        _games.First(g => g.Id == id);

    public bool TryGet(GameId id, out GameDefinition game)
    {
        foreach (var candidate in _games)
        {
            if (candidate.Id == id)
            {
                game = candidate;
                return true;
            }
        }
        game = _games[0];
        return false;
    }

    public static GameDefinition CreatePoe1() => new(
        GameId.PathOfExile1,
        "Path of Exile",
        "PoE1",
        "3.28.0",
        "Assets/PoE1",
        new Poe1TreeLoader(),
        new Poe1ImportStrategy(),
        GameFeatureFlags.Poe1,
        ["3.28.0"]);

    public static GameDefinition CreatePoe2() => new(
        GameId.PathOfExile2,
        "Path of Exile 2",
        "PoE2",
        "0.5.0",
        "Assets/PoE2",
        new Poe2TreeLoader(),
        new Poe2ImportStrategy(),
        GameFeatureFlags.Poe2Milestone2,
        ["0.4.0", "0.5.0"]);
}
