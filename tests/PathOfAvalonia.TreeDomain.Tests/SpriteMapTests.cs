using System.Text;
using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class SpriteMapTests
{
    private const string HinekoraEffect =
        "Art/2DArt/UIImages/InGame/AncestralTrial/PassiveTreeTattoos/HinekoraPassiveBG.png";

    [Fact]
    public void Poe1Version329SpriteMapLoadsArrayCoordinates()
    {
        using var stream = File.OpenRead(Poe1Asset("3_29_0", "data.json"));

        var sprites = SpriteMap.LoadPoe1FromGggTree(stream, "3_29_0/assets");

        Assert.Equal(new SpriteRect(139, 0, 139, 138), sprites.Lookup("tattooActiveEffect", HinekoraEffect));
    }

    [Fact]
    public void Poe1GggTreeAcceptsScalarAndArraySpriteCoordinates()
    {
        using var stream = JsonStream("""
            {
              "sprites": {
                "tattooActiveEffect": {
                  "0.3835": {
                    "filename": "https://web.poecdn.com/image/passive-skill/tattoo-active-effect-3.png?hash",
                    "w": 576,
                    "h": 704,
                    "coords": {
                      "scalar.png": { "x": 0, "y": 0, "w": 139, "h": 138 },
                      "array.png": {
                        "x": [139, 0],
                        "y": [0, 414],
                        "w": [139, 288],
                        "h": [138, 290]
                      }
                    }
                  }
                }
              }
            }
            """);

        var sprites = SpriteMap.LoadPoe1FromGggTree(stream, "3_29_0/assets");

        Assert.Equal(new SpriteRect(0, 0, 139, 138), sprites.Lookup("tattooActiveEffect", "scalar.png"));
        Assert.Equal(new SpriteRect(139, 0, 139, 138), sprites.Lookup("tattooActiveEffect", "array.png"));
    }

    [Fact]
    public void Poe1GggTreeRejectsMismatchedSpriteCoordinateArrays()
    {
        using var stream = JsonStream("""
            {
              "sprites": {
                "tattooActiveEffect": {
                  "0.3835": {
                    "filename": "tattoo.png",
                    "w": 576,
                    "h": 704,
                    "coords": {
                      "invalid.png": {
                        "x": [139, 0],
                        "y": [0],
                        "w": [139, 288],
                        "h": [138, 290]
                      }
                    }
                  }
                }
              }
            }
            """);

        Assert.Throws<System.Text.Json.JsonException>(
            () => SpriteMap.LoadPoe1FromGggTree(stream, "3_29_0/assets"));
    }

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));

    private static string Poe1Asset(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", .. parts]));
}
