using PathOfAvalonia.TreeDomain;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class ClassCatalogTests
{
    [Fact]
    public void Poe1CatalogPreservesClassAndAscendancyNames()
    {
        var catalog = ClassCatalog.CreatePoe1();

        Assert.Equal(new[] { "Scion", "Marauder", "Ranger", "Witch", "Duelist", "Templar", "Shadow" }, catalog.ClassNames);
        Assert.Equal(new[] { "None", "Juggernaut", "Berserker", "Chieftain" }, catalog.AscendancyNames(1));
    }

    [Fact]
    public void Poe1ScavengerMapsToReliquarianTreeName()
    {
        var catalog = ClassCatalog.CreatePoe1();

        Assert.Equal("Scavenger", catalog.AscendancyNames(0)[2]);
        Assert.Equal("Reliquarian", catalog.AscendancyTreeName(0, 2));
    }
}

