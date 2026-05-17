using PathOfAvalonia.TreeDomain.Poe1;
using PathOfAvalonia.TreeDomain.Poe2;

namespace PathOfAvalonia.TreeDomain;

public static class TreeLoader
{
    public static TreeModel LoadFromJson(Stream stream, string version) =>
        LoadPoe1FromJson(stream, version);

    public static TreeModel LoadPoe1FromJson(Stream stream, string version) =>
        new Poe1TreeLoader().Load(stream, version, GameId.PathOfExile1);

    public static TreeModel LoadPoe2FromJson(Stream stream, string version) =>
        new Poe2TreeLoader().Load(stream, version, GameId.PathOfExile2);
}

