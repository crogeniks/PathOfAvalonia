using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2SpriteMapTests
{
    [Fact]
    public void GeneratedSpriteMapContainsNodeIconsAndFrames()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE2", "sprites_0_4.json"));
        using var stream = File.OpenRead(path);
        var sprites = SpriteMap.LoadFromJson(stream);

        Assert.True(sprites.Atlases["poe2NodeIcons"].Coords.Count > 0);
        Assert.True(sprites.Atlases["poe2Frames"].Coords.Count > 0);
        Assert.NotEqual(
            sprites.Atlases["poe2NodeIcons"].Coords["Art/2DArt/SkillIcons/passives/life1.dds"],
            sprites.Atlases["poe2NodeIcons"].Coords["Art/2DArt/SkillIcons/passives/mana.dds"]);
        Assert.NotEqual(
            sprites.Atlases["poe2Frames"].Coords["PSSkillFrame"],
            sprites.Atlases["poe2Frames"].Coords["NotableFrameUnallocated"]);
        Assert.NotEqual(
            sprites.Atlases["poe2Frames"].Coords["PSSkillFrame"],
            sprites.Atlases["poe2Frames"].Coords["PSSkillFrameActive"]);
        Assert.NotEqual(
            sprites.Atlases["poe2Frames"].Coords["PSSkillFrame"],
            sprites.Atlases["poe2Frames"].Coords["PSSkillFrameHighlighted"]);
        Assert.NotEqual(
            sprites.Atlases["poe2Frames"].Coords["NotableFrameUnallocated"],
            sprites.Atlases["poe2Frames"].Coords["NotableFrameCanAllocate"]);
        Assert.NotEqual(
            sprites.Atlases["poe2Frames"].Coords["PSSkillFrame"],
            sprites.Atlases["poe2Frames"].Coords["JewelFrameUnallocated"]);
        Assert.NotEqual(
            sprites.Atlases["poe2Frames"].Coords["PSSkillFrame"],
            sprites.Atlases["poe2Frames"].Coords["OracleFrameSmallNormal"]);
        foreach (var atlas in sprites.Atlases.Values)
        {
            var atlasPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, atlas.File));
            Assert.True(File.Exists(atlasPath), atlasPath);
        }
    }
}
