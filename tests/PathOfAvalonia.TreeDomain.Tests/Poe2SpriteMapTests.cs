using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2SpriteMapTests
{
    [Fact]
    public void GggSpriteAssetsContainNodeIconsAndFrames()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE2", "0_5_0"));
        using var skills = File.OpenRead(Path.Combine(root, "assets", "skills.json"));
        using var frames = File.OpenRead(Path.Combine(root, "assets", "frame.json"));
        using var jewels = File.OpenRead(Path.Combine(root, "assets", "jewel.json"));
        var sprites = SpriteMap.LoadPoe2FromGggAssets(skills, frames, jewels);

        Assert.True(sprites.Atlases["poe2NodeIcons"].Coords.Count > 0);
        Assert.True(sprites.Atlases["poe2Frames"].Coords.Count > 0);
        Assert.True(sprites.Atlases["poe2Jewels"].Coords.Count > 0);
        Assert.NotEqual(
            sprites.Atlases["poe2NodeIcons"].Coords["Art/2DArt/SkillIcons/passives/2handeddamage.png"],
            sprites.Atlases["poe2NodeIcons"].Coords["Art/2DArt/SkillIcons/passives/KeystoneWhispersOfDoom.png"]);
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
            sprites.Atlases["poe2Frames"].Coords["OracleFrameUnallocated"]);
        foreach (var key in new[]
        {
            "JewelSocketActiveRed",
            "JewelSocketActiveGreen",
            "JewelSocketActiveBlue",
            "JewelSocketActivePrismatic",
            "JewelSocketActiveLegion",
        })
        {
            Assert.NotNull(sprites.Lookup("poe2Jewels", key));
        }
        foreach (var atlas in sprites.Atlases.Values)
        {
            var atlasPath = Path.GetFullPath(Path.Combine(root, atlas.File));
            Assert.True(File.Exists(atlasPath), atlasPath);
        }
    }
}
