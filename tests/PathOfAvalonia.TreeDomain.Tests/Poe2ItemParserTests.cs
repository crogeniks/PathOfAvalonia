using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2ItemParserTests
{
    [Fact]
    public void ParsesSocketsAndRunes()
    {
        var item = RawItemParser.Parse("Body Armour", """
            Rarity: Rare
            Dire Shell
            Expert Hexer's Robe
            --------
            Quality: +20%
            Sockets: S S S
            Rune: Soul Core of Cholotl
            Rune: Talisman of Thruldana
            {enchant}{rune}Adds fire resistance
            Corrupted
            """);

        Assert.Equal(3, item.Sockets.Count);
        Assert.Equal(new[] { "Soul Core of Cholotl", "Talisman of Thruldana" }, item.Runes);
    }

    [Fact]
    public void ItemViewModelStripsPoe2MetadataTags()
    {
        var item = RawItemParser.Parse("Body Armour", """
            Rarity: Rare
            Dire Shell
            Expert Hexer's Robe
            --------
            {enchant}{rune}Adds fire resistance
            {custom}Custom line
            Desecrated
            """);

        var vm = ItemViewModel.FromImported(item);

        Assert.Contains(vm.Body, line => line.Text == "Adds fire resistance");
        Assert.Contains(vm.Body, line => line.Text == "Custom line");
        Assert.Contains(vm.StatusFlags, line => line.Text == "Desecrated");
    }
}
