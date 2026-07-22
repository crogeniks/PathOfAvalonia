using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Jewels;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class TimelessJewelTests
{
    private static readonly Lazy<TimelessJewelData> Data = new(LoadTimelessJewelData);

    [Fact]
    public void LookupStreamsRemainUnopenedUntilTheirJewelTypeIsResolved()
    {
        using var definitions = new MemoryStream("""
            { "additionOffset": 96, "additions": [], "replacements": [] }
            """u8.ToArray());
        using var mapping = new MemoryStream("""
            { "size": 1, "sizeNotable": 1, "nodes": {}, "localIds": {} }
            """u8.ToArray());
        var openCount = 0;
        var data = TimelessJewelData.Load(
            definitions,
            mapping,
            new Dictionary<TimelessJewelType, Func<Stream>>
            {
                [TimelessJewelType.GloriousVanity] = () =>
                {
                    openCount++;
                    return Stream.Null;
                },
            });

        Assert.False(data.IsEmpty);
        Assert.Equal(0, openCount);
    }

    [Theory]
    [InlineData("Glorious Vanity", "Bathed in the blood of 100 sacrificed in the name of Doryani", TimelessJewelType.GloriousVanity, 100, TimelessConqueror.Vaal, "3")]
    [InlineData("Lethal Pride", "Commanded leadership over 10000 warriors under Kaom", TimelessJewelType.LethalPride, 10000, TimelessConqueror.Karui, "1")]
    [InlineData("Brutal Restraint", "Denoted service of 500 dekhara in the akhara of Balbala", TimelessJewelType.BrutalRestraint, 500, TimelessConqueror.Maraketh, "1_v2")]
    [InlineData("Militant Faith", "Carved to glorify 2000 new faithful converted by High Templar Maxarius", TimelessJewelType.MilitantFaith, 2000, TimelessConqueror.Templar, "1_v2")]
    [InlineData("Elegant Hubris", "Commissioned 160000 coins to commemorate Caspiro", TimelessJewelType.ElegantHubris, 160000, TimelessConqueror.EternalEmpire, "3_v2")]
    [InlineData("Heroic Tragedy", "Remembrancing 8000 songworthy deeds by the line of Medved", TimelessJewelType.HeroicTragedy, 8000, TimelessConqueror.Kalguuran, "3")]
    public void ParserReadsEveryTimelessJewelType(
        string name,
        string seedLine,
        TimelessJewelType expectedType,
        int expectedSeed,
        TimelessConqueror expectedConqueror,
        string expectedConquerorId)
    {
        var item = TimelessItem(1, name, seedLine);

        var parsed = Assert.IsType<TimelessJewelSpec>(TimelessJewelParser.Parse(item));

        Assert.Equal(expectedType, parsed.Type);
        Assert.Equal(expectedSeed, parsed.Seed);
        Assert.Equal(expectedConqueror, parsed.Conqueror);
        Assert.Equal(expectedConquerorId, parsed.ConquerorId);
    }

    [Fact]
    public void ParserUsesTheSelectedPobVariant()
    {
        var item = RawItemParser.Parse(string.Empty, """
            Rarity: Unique
            Glorious Vanity
            Timeless Jewel
            --------
            Variant: Doryani (Corrupted Soul)
            Variant: Xibaqua (Divine Flesh)
            Selected Variant: 2
            Radius: Large
            {variant:1}Bathed in the blood of 101 sacrificed in the name of Doryani
            {variant:2}Bathed in the blood of 202 sacrificed in the name of Xibaqua
            Passives in radius are Conquered by the Vaal
            """);

        var parsed = Assert.IsType<TimelessJewelSpec>(TimelessJewelParser.Parse(item));

        Assert.Equal(202, parsed.Seed);
        Assert.Equal("1", parsed.ConquerorId);
    }

    [Fact]
    public void ParserReadsTheAnnotatedNameWrittenByPobsTimelessJewelFinder()
    {
        var item = RawItemParser.Parse(string.Empty, """
            Rarity: UNIQUE
            Elegant Hubris [140860; 1; Mind Over Matter]
            Timeless Jewel
            League: Legion
            Variant: Cadiro (Supreme Decadence)
            Variant: Victario (Supreme Grandstanding)
            Variant: Caspiro (Supreme Ostentation)
            Selected Variant: 3
            LevelReq: 20
            Radius: Large
            Limited to: 1
            Implicits: 0
            {variant:1}Commissioned 140860 coins to commemorate Cadiro
            {variant:2}Commissioned 140860 coins to commemorate Victario
            {variant:3}Commissioned 140860 coins to commemorate Caspiro
            Passives in radius are Conquered by the Eternal Empire
            Historic
            """);

        var parsed = Assert.IsType<TimelessJewelSpec>(TimelessJewelParser.Parse(item));

        Assert.Equal(TimelessJewelType.ElegantHubris, parsed.Type);
        Assert.Equal(140860, parsed.Seed);
        Assert.Equal(TimelessConqueror.EternalEmpire, parsed.Conqueror);
        Assert.Equal("3_v2", parsed.ConquerorId);
    }

    [Fact]
    public void PobItemSetWithFinderGeneratedTimelessJewelAppliesTreeEffects()
    {
        var build = PobXmlBuildParser.Parse("""
            <PathOfBuilding>
              <Tree activeSpec="1">
                <Spec title="^3^2^1^4^5Min Max Gear" treeVersion="3.28" classId="0" ascendClassId="0" nodes="55190" />
                <Spec title="^3^2^1^4^5End Game Gear" treeVersion="3.28" classId="0" ascendClassId="0" nodes="36634">
                  <Sockets>
                    <Socket nodeId="36634" itemId="3" />
                  </Sockets>
                </Spec>
              </Tree>
              <Items activeItemSet="1">
                <Item id="3">Rarity: UNIQUE
            Elegant Hubris [140860; 1; Mind Over Matter]
            Timeless Jewel
            League: Legion
            Variant: Cadiro (Supreme Decadence)
            Variant: Victario (Supreme Grandstanding)
            Variant: Caspiro (Supreme Ostentation)
            Selected Variant: 3
            LevelReq: 20
            Radius: Large
            Limited to: 1
            Implicits: 0
            {variant:1}Commissioned 140860 coins to commemorate Cadiro
            {variant:2}Commissioned 140860 coins to commemorate Victario
            {variant:3}Commissioned 140860 coins to commemorate Caspiro
            Passives in radius are Conquered by the Eternal Empire
            Historic</Item>
                <ItemSet id="1" title="^3^2^1^4^5Min Max Gear" />
                <ItemSet id="7" title="^3^2^1^4^5End Game Gear">
                  <Slot name="Jewel 1" itemId="3" />
                </ItemSet>
              </Items>
            </PathOfBuilding>
            """, "pob-code");
        build = build.WithVariants(passiveIndex: 1, itemSetIndex: 1);
        var tree = LoadTree();
        var spec = new PassiveSpec(tree, tree.Classes, GameFeatureFlags.Poe1, Data.Value);

        spec.ApplyImport(build);

        Assert.Equal("End Game Gear", build.PassiveTreeVariants[build.ActivePassiveTreeVariantIndex].DisplayName);
        Assert.Equal("End Game Gear", build.ItemSetVariants[build.ActiveItemSetVariantIndex].DisplayName);
        Assert.Equal("Elegant Hubris [140860; 1; Mind Over Matter]", build.ItemsById[3].Name);
        var affectedNodeIds = RadiusMembership.BuildForSockets(tree, JewelRadiusTable.For(tree.GameId, tree.Version))
            [36634].NodesByRadiusIndex[3];
        var transformed = affectedNodeIds
            .Select(spec.EffectiveNode)
            .FirstOrDefault(node => node.ReplacesNode || !node.EffectiveStats.SequenceEqual(node.BaseNode.Stats));
        Assert.NotNull(transformed);
        Assert.True(transformed.IsConquered);
        Assert.Equal(TimelessConqueror.EternalEmpire, transformed.Conqueror);
    }

    [Fact]
    public void GloriousVanitySeedReplacesNotableNameIconAndRolledStats()
    {
        var tree = LoadTree();
        var node = tree.Nodes[58831];

        var effects = ResolveInSocketRadius(
            tree,
            new TimelessJewelSpec(TimelessJewelType.GloriousVanity, 100, TimelessConqueror.Vaal, "3"));
        var effect = effects[node.Id];

        Assert.Equal("Ritual of Flesh", effect.EffectiveName);
        Assert.Equal("Art/2DArt/SkillIcons/passives/VaalNotableDefensive.dds", effect.EffectiveIcon);
        Assert.Equal(["9% increased maximum Life", "Regenerate 1% of Life per second"], effect.EffectiveStats);
        Assert.Equal("Disemboweling", node.Name);
        Assert.Contains("50% increased Melee Critical Strike Chance", node.Stats);
    }

    [Fact]
    public void DifferentGloriousVanitySeedsProduceDifferentNodeEffects()
    {
        var tree = LoadTree();
        var first = ResolveInSocketRadius(
            tree,
            new TimelessJewelSpec(TimelessJewelType.GloriousVanity, 100, TimelessConqueror.Vaal, "3"))[58831];
        var second = ResolveInSocketRadius(
            tree,
            new TimelessJewelSpec(TimelessJewelType.GloriousVanity, 101, TimelessConqueror.Vaal, "3"))[58831];

        Assert.Equal("Ritual of Flesh", first.EffectiveName);
        Assert.Equal("Revitalising Lightning", second.EffectiveName);
        Assert.NotEqual(first.EffectiveStats, second.EffectiveStats);
    }

    [Theory]
    [InlineData(TimelessJewelType.LethalPride, 10000, TimelessConqueror.Karui, "1", 24716, "Battle Trance", "+20% to Fire Resistance")]
    [InlineData(TimelessJewelType.BrutalRestraint, 500, TimelessConqueror.Maraketh, "2", 24716, "Battle Trance", "5% increased Dexterity")]
    [InlineData(TimelessJewelType.MilitantFaith, 2000, TimelessConqueror.Templar, "3", 26564, "Enduring Faith", "+1 to Minimum Endurance Charges while you have at least 150 Devotion")]
    [InlineData(TimelessJewelType.ElegantHubris, 2000, TimelessConqueror.EternalEmpire, "1", 24716, "Rural Life", "80% chance to Avoid being Shocked")]
    [InlineData(TimelessJewelType.HeroicTragedy, 100, TimelessConqueror.Kalguuran, "1", 24716, "Persisting Drive", "15% faster Restoration of Ward")]
    public void LookupAppliesSeedSpecificNotableEffectsForEveryNonVaalType(
        TimelessJewelType type,
        int seed,
        TimelessConqueror conqueror,
        string conquerorId,
        int nodeId,
        string expectedName,
        string expectedStat)
    {
        var tree = LoadTree();
        var effect = ResolveInSocketRadius(tree, new TimelessJewelSpec(type, seed, conqueror, conquerorId))[nodeId];

        Assert.Equal(expectedName, effect.EffectiveName);
        Assert.Contains(expectedStat, effect.EffectiveStats);
    }

    [Theory]
    [InlineData(TimelessJewelType.LethalPride, 10000, TimelessConqueror.Karui, "1", "Strength", "+2 to Strength")]
    [InlineData(TimelessJewelType.BrutalRestraint, 500, TimelessConqueror.Maraketh, "2", "Strength", "+2 to Dexterity")]
    [InlineData(TimelessJewelType.MilitantFaith, 2000, TimelessConqueror.Templar, "3", "Devotion", "+10 to Devotion")]
    [InlineData(TimelessJewelType.ElegantHubris, 2000, TimelessConqueror.EternalEmpire, "1", "Price of Glory", null)]
    [InlineData(TimelessJewelType.HeroicTragedy, 100, TimelessConqueror.Kalguuran, "1", "Strength", "1% increased Ward")]
    public void ConquerorTransformsSmallAttributePassives(
        TimelessJewelType type,
        int seed,
        TimelessConqueror conqueror,
        string conquerorId,
        string expectedName,
        string? expectedStat)
    {
        var tree = LoadTree();
        var effect = ResolveInSocketRadius(tree, new TimelessJewelSpec(type, seed, conqueror, conquerorId))[50570];

        Assert.Equal(expectedName, effect.EffectiveName);
        if (expectedStat is null)
        {
            Assert.Empty(effect.EffectiveStats);
        }
        else
        {
            Assert.Contains(expectedStat, effect.EffectiveStats);
        }
    }

    [Theory]
    [InlineData(TimelessJewelType.GloriousVanity, 100, TimelessConqueror.Vaal, "1", "Divine Flesh")]
    [InlineData(TimelessJewelType.GloriousVanity, 100, TimelessConqueror.Vaal, "2", "Eternal Youth")]
    [InlineData(TimelessJewelType.GloriousVanity, 100, TimelessConqueror.Vaal, "3", "Corrupted Soul")]
    [InlineData(TimelessJewelType.GloriousVanity, 100, TimelessConqueror.Vaal, "2_v2", "Immortal Ambition")]
    [InlineData(TimelessJewelType.LethalPride, 10000, TimelessConqueror.Karui, "1", "Strength of Blood")]
    [InlineData(TimelessJewelType.LethalPride, 10000, TimelessConqueror.Karui, "2", "Tempered by War")]
    [InlineData(TimelessJewelType.LethalPride, 10000, TimelessConqueror.Karui, "3", "Glancing Blows")]
    [InlineData(TimelessJewelType.LethalPride, 10000, TimelessConqueror.Karui, "3_v2", "Chainbreaker")]
    [InlineData(TimelessJewelType.BrutalRestraint, 500, TimelessConqueror.Maraketh, "1", "Wind Dancer")]
    [InlineData(TimelessJewelType.BrutalRestraint, 500, TimelessConqueror.Maraketh, "2", "Dance with Death")]
    [InlineData(TimelessJewelType.BrutalRestraint, 500, TimelessConqueror.Maraketh, "3", "Second Sight")]
    [InlineData(TimelessJewelType.BrutalRestraint, 500, TimelessConqueror.Maraketh, "1_v2", "The Traitor")]
    [InlineData(TimelessJewelType.MilitantFaith, 2000, TimelessConqueror.Templar, "1", "The Agnostic")]
    [InlineData(TimelessJewelType.MilitantFaith, 2000, TimelessConqueror.Templar, "2", "Inner Conviction")]
    [InlineData(TimelessJewelType.MilitantFaith, 2000, TimelessConqueror.Templar, "3", "Power of Purpose")]
    [InlineData(TimelessJewelType.MilitantFaith, 2000, TimelessConqueror.Templar, "1_v2", "Transcendence")]
    [InlineData(TimelessJewelType.ElegantHubris, 2000, TimelessConqueror.EternalEmpire, "1", "Supreme Decadence")]
    [InlineData(TimelessJewelType.ElegantHubris, 2000, TimelessConqueror.EternalEmpire, "2", "Supreme Grandstanding")]
    [InlineData(TimelessJewelType.ElegantHubris, 2000, TimelessConqueror.EternalEmpire, "3", "Supreme Ego")]
    [InlineData(TimelessJewelType.ElegantHubris, 2000, TimelessConqueror.EternalEmpire, "3_v2", "Supreme Ostentation")]
    [InlineData(TimelessJewelType.HeroicTragedy, 100, TimelessConqueror.Kalguuran, "1", "Black Scythe Training")]
    [InlineData(TimelessJewelType.HeroicTragedy, 100, TimelessConqueror.Kalguuran, "2", "Celestial Mathematics")]
    [InlineData(TimelessJewelType.HeroicTragedy, 100, TimelessConqueror.Kalguuran, "3", "The Unbreaking Circle")]
    public void ConquerorReplacesKeystones(
        TimelessJewelType type,
        int seed,
        TimelessConqueror conqueror,
        string conquerorId,
        string expectedName)
    {
        var tree = LoadTree();
        var effect = ResolveInSocketRadius(tree, new TimelessJewelSpec(type, seed, conqueror, conquerorId))[31961];

        Assert.Equal(expectedName, effect.EffectiveName);
        Assert.NotEmpty(effect.EffectiveStats);
    }

    [Fact]
    public void PassiveSpecAppliesAndRemovesTimelessEffectsWithoutMutatingTree()
    {
        var tree = LoadTree();
        var spec = new PassiveSpec(tree, tree.Classes, GameFeatureFlags.Poe1, Data.Value);
        var item = TimelessItem(1, "Glorious Vanity", "Bathed in the blood of 100 sacrificed in the name of Doryani");
        spec.ApplyImport(BuildImport(item));

        var conquered = spec.EffectiveNode(58831);

        Assert.True(conquered.IsConquered);
        Assert.Equal(TimelessConqueror.Vaal, conquered.Conqueror);
        Assert.Equal("Ritual of Flesh", conquered.EffectiveName);
        Assert.Equal(["9% increased maximum Life", "Regenerate 1% of Life per second"], conquered.EffectiveStats);
        Assert.Equal("Disemboweling", tree.Nodes[58831].Name);

        spec.Toggle(55190);
        var restored = spec.EffectiveNode(58831);

        Assert.False(restored.IsConquered);
        Assert.Equal("Disemboweling", restored.EffectiveName);
        Assert.Equal(tree.Nodes[58831].Stats, restored.EffectiveStats);
    }

    [Fact]
    public void TimelessReplacementSpriteAssetsCoverEveryReplacementIcon()
    {
        using var stream = File.OpenRead(Poe1Asset("TimelessJewels", "sprites.json"));
        var sprites = SpriteMap.LoadFromJson(stream);

        Assert.NotNull(sprites.Lookup("legionNormalActive", "Art/2DArt/SkillIcons/passives/VaalOffensive.dds"));
        Assert.NotNull(sprites.Lookup("legionNotableInactive", "Art/2DArt/SkillIcons/passives/EternalEmpireOffensiveNotable.dds"));
        Assert.NotNull(sprites.Lookup("legionKeystoneActive", "Art/2DArt/SkillIcons/passives/StrengthOfBlood.dds"));
        Assert.True(File.Exists(Poe1Asset("TimelessJewels", "sprites", "skills-additional-3.jpg")));
        Assert.True(File.Exists(Poe1Asset("TimelessJewels", "sprites", "keystone-additional-3.png")));
    }

    private static IReadOnlyDictionary<int, TimelessNodeEffect> ResolveInSocketRadius(
        TreeModel tree,
        TimelessJewelSpec jewel)
    {
        var table = JewelRadiusTable.For(tree.GameId, tree.Version);
        var nodeIds = RadiusMembership.BuildForSockets(tree, table)[55190].NodesByRadiusIndex[3];
        return Data.Value.Resolve(jewel, nodeIds.Select(id => tree.Nodes[id]));
    }

    private static ImportedBuild BuildImport(ImportedItem item) => new(
        ClassId: 0,
        AscendClassId: 0,
        SecondaryAscendClassId: 0,
        NodeHashes: [55190],
        ClusterNodeHashes: [],
        MasterySelections: new Dictionary<int, int>(),
        TreeVersion: "3.28.0",
        Source: "test")
    {
        ItemsById = new Dictionary<int, ImportedItem> { [item.Id] = item },
        SocketedJewels = [new ImportedSocketedJewel(55190, item.Id)],
    };

    private static ImportedItem TimelessItem(int id, string name, string seedLine)
    {
        var raw = string.Join('\n',
            "Rarity: Unique",
            name,
            "Timeless Jewel",
            "--------",
            "Radius: Large",
            seedLine,
            "Passives in radius are Conquered by the Vaal",
            "Historic");
        return new ImportedItem(string.Empty, "Unique", name, "Timeless Jewel", raw) { Id = id };
    }

    private static TimelessJewelData LoadTimelessJewelData()
    {
        var root = Poe1Asset("TimelessJewels");
        using var definitions = File.OpenRead(Path.Combine(root, "definitions.json"));
        using var mapping = File.OpenRead(Path.Combine(root, "mapping.json"));
        var paths = new Dictionary<TimelessJewelType, string>
        {
            [TimelessJewelType.GloriousVanity] = "glorious-vanity.z",
            [TimelessJewelType.LethalPride] = "lethal-pride.z",
            [TimelessJewelType.BrutalRestraint] = "brutal-restraint.z",
            [TimelessJewelType.MilitantFaith] = "militant-faith.z",
            [TimelessJewelType.ElegantHubris] = "elegant-hubris.z",
            [TimelessJewelType.HeroicTragedy] = "heroic-tragedy.z",
        };
        var streams = paths.ToDictionary(
            pair => pair.Key,
            pair => (Stream)File.OpenRead(Path.Combine(root, pair.Value)));
        try
        {
            return TimelessJewelData.Load(definitions, mapping, streams);
        }
        finally
        {
            foreach (var stream in streams.Values)
            {
                stream.Dispose();
            }
        }
    }

    private static TreeModel LoadTree()
    {
        using var stream = File.OpenRead(Poe1Asset("3_28_0", "data.json"));
        return TreeLoader.LoadFromJson(stream, "3.28.0");
    }

    private static string Poe1Asset(params string[] parts) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "assets", "PoE1",
            Path.Combine(parts)));
}
