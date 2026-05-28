using System.Xml.Linq;

namespace PathOfAvalonia.TreeDomain.Import;

internal sealed record ParsedItems(
    IReadOnlyList<ImportedItem> ActiveItems,
    IReadOnlyDictionary<int, ImportedItem> ItemsById,
    IReadOnlyList<ImportedItemSetVariant> ItemSetVariants,
    int ActiveItemSetVariantIndex);

internal static class PobItemSetParser
{
    public static ParsedItems Parse(string xml)
    {
        var itemsEl = PobXmlHelpers.TryExtractElement(xml, "Items");
        if (itemsEl is null)
        {
            return Empty();
        }

        var texts = new Dictionary<int, string>();
        var itemsById = new Dictionary<int, ImportedItem>();
        foreach (var el in itemsEl.Elements("Item"))
        {
            if ((int?)el.Attribute("id") is int id)
            {
                texts[id] = el.Value;
                itemsById[id] = RawItemParser.Parse(string.Empty, rawText: el.Value.Trim()) with { Id = id };
            }
        }

        var itemSets = itemsEl.Elements("ItemSet").ToArray();
        var variants = itemSets
            .Select((setEl, index) => ParseItemSetVariant(setEl, index, texts))
            .ToArray();
        if (variants.Length == 0)
        {
            return new ParsedItems([], itemsById, [], 0);
        }

        var activeIndex = ActiveItemSetIndex(itemsEl, itemSets);
        return new ParsedItems(variants[activeIndex].Items, itemsById, variants, activeIndex);
    }

    private static ParsedItems Empty() =>
        new([], new Dictionary<int, ImportedItem>(), [], 0);

    private static ImportedItemSetVariant ParseItemSetVariant(XElement setEl, int index, IReadOnlyDictionary<int, string> texts)
    {
        var id = (int?)setEl.Attribute("id") ?? 0;
        var result = new List<ImportedItem>();
        foreach (var slotEl in setEl.Elements("Slot"))
        {
            var slotName = (string?)slotEl.Attribute("name") ?? string.Empty;
            var itemId = (int?)slotEl.Attribute("itemId") ?? 0;
            if (itemId > 0 && texts.TryGetValue(itemId, out var raw))
            {
                result.Add(RawItemParser.Parse(slotName, raw.Trim()) with { Id = itemId });
            }
        }

        result.Sort((a, b) => SlotIndex(a.Slot).CompareTo(SlotIndex(b.Slot)));
        return new ImportedItemSetVariant(index, id, ItemSetDisplayName(setEl, id, index), result);
    }

    private static int ActiveItemSetIndex(XElement itemsEl, IReadOnlyList<XElement> itemSets)
    {
        if ((int?)itemsEl.Attribute("activeItemSet") is int activeItemSet)
        {
            var index = IndexOfItemSetId(itemSets, activeItemSet);
            if (index >= 0)
            {
                return index;
            }
        }
        if ((int?)itemsEl.Attribute("active") is int active)
        {
            var index = IndexOfItemSetId(itemSets, active);
            if (index >= 0)
            {
                return index;
            }
        }

        for (var i = 0; i < itemSets.Count; i++)
        {
            var isActive = string.Equals((string?)itemSets[i].Attribute("active"), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals((string?)itemSets[i].Attribute("active"), "1", StringComparison.OrdinalIgnoreCase);
            if (isActive)
            {
                return i;
            }
        }

        return 0;
    }

    private static int IndexOfItemSetId(IReadOnlyList<XElement> itemSets, int id)
    {
        for (var i = 0; i < itemSets.Count; i++)
        {
            if (((int?)itemSets[i].Attribute("id") ?? 0) == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static string ItemSetDisplayName(XElement element, int id, int index)
    {
        var title = ((string?)element.Attribute("title"))?.Trim();
        if (!string.IsNullOrEmpty(title))
        {
            return title;
        }

        var name = ((string?)element.Attribute("name"))?.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        return id > 0 ? $"Item Set {id}" : $"Item Set {index + 1}";
    }

    private static int SlotIndex(string slot) => slot switch
    {
        "Weapon 1" => 0,
        "Weapon 2" => 1,
        "Weapon 1 Swap" => 2,
        "Weapon 2 Swap" => 3,
        "Helmet" => 4,
        "Body Armour" => 5,
        "Gloves" => 6,
        "Boots" => 7,
        "Amulet" => 8,
        "Ring 1" => 9,
        "Ring 2" => 10,
        "Belt" => 11,
        "Flask 1" => 12,
        "Flask 2" => 13,
        "Flask 3" => 14,
        "Flask 4" => 15,
        "Flask 5" => 16,
        _ => 100,
    };
}
