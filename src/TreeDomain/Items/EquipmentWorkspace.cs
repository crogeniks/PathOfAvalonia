using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain.Items;

public sealed record EquipmentLoadout(
    int Id,
    string Name,
    IReadOnlyDictionary<string, int> EquippedItemIds);

/// <summary>
/// Mutable per-build equipment state. Items are owned once by the library;
/// loadouts only map slots to item ids, matching upstream PoB semantics.
/// Passive-tree jewel assignments are independent of equipment loadouts.
/// </summary>
public sealed class EquipmentWorkspace
{
    private readonly Dictionary<int, ImportedItem> _items = [];
    private readonly List<MutableLoadout> _loadouts = [];
    private readonly Dictionary<int, int> _socketedJewelItemIds = [];
    private int _activeLoadoutIndex;
    private int _nextItemId = 1;
    private int _nextLoadoutId = 1;
    private bool _hasExplicitLoadouts;
    private readonly GameId? _gameId;

    public EquipmentWorkspace(GameId? gameId = null)
    {
        _gameId = gameId;
        Reset();
    }

    public IReadOnlyDictionary<int, ImportedItem> Items => _items;
    public IReadOnlyList<EquipmentLoadout> Loadouts => _loadouts
        .Select(loadout => loadout.Snapshot())
        .ToArray();
    public int ActiveLoadoutIndex => _activeLoadoutIndex;
    public EquipmentLoadout ActiveLoadout => _loadouts[_activeLoadoutIndex].Snapshot();
    public IReadOnlyDictionary<int, int> SocketedJewelItemIds => _socketedJewelItemIds;

    public void Reset()
    {
        _items.Clear();
        _loadouts.Clear();
        _socketedJewelItemIds.Clear();
        _activeLoadoutIndex = 0;
        _nextItemId = 1;
        _nextLoadoutId = 1;
        _hasExplicitLoadouts = false;
        _loadouts.Add(new MutableLoadout(_nextLoadoutId++, "Default", []));
    }

    public void Load(ImportedBuild build)
    {
        Reset();
        _loadouts.Clear();
        _nextLoadoutId = 1;

        foreach (var item in build.ItemsById.Values.OrderBy(item => item.Id))
        {
            AddLoadedItem(NormalizeItem(item));
        }
        foreach (var item in build.ItemSetVariants.SelectMany(variant => variant.Items).Concat(build.Items))
        {
            EnsureLoadedItem(NormalizeItem(item));
        }

        if (build.ItemSetVariants.Count > 0)
        {
            _hasExplicitLoadouts = true;
            foreach (var variant in build.ItemSetVariants)
            {
                var assignments = Assignments(variant.Items);
                var id = variant.Id > 0 && _loadouts.All(loadout => loadout.Id != variant.Id)
                    ? variant.Id
                    : NextLoadoutId();
                _loadouts.Add(new MutableLoadout(id, NonEmptyName(variant.DisplayName, _loadouts.Count + 1), assignments));
                _nextLoadoutId = Math.Max(_nextLoadoutId, id + 1);
            }
            _activeLoadoutIndex = Math.Clamp(build.ActiveItemSetVariantIndex, 0, _loadouts.Count - 1);
        }
        else
        {
            _loadouts.Add(new MutableLoadout(NextLoadoutId(), "Default", Assignments(build.Items)));
            _activeLoadoutIndex = 0;
        }

        foreach (var socketed in build.SocketedJewels)
        {
            if (build.ItemsById.TryGetValue(socketed.ItemId, out var item))
            {
                var id = EnsureLoadedItem(item);
                _socketedJewelItemIds[socketed.SocketNodeId] = id;
            }
        }
    }

    public ImportedItem AddItem(string rawText, string preferredSlot)
    {
        var id = NextItemId();
        var normalizedSlot = EquipmentSlotCatalog.NormalizeForGame(preferredSlot, _gameId);
        var item = RawItemParser.Parse(normalizedSlot, rawText) with { Id = id };
        _items[id] = item;
        return item;
    }

    public ImportedItem UpdateItem(int itemId, string rawText, string preferredSlot)
    {
        if (!_items.ContainsKey(itemId))
        {
            throw new ArgumentOutOfRangeException(nameof(itemId));
        }

        var normalizedSlot = EquipmentSlotCatalog.NormalizeForGame(preferredSlot, _gameId);
        var item = RawItemParser.Parse(normalizedSlot, rawText) with { Id = itemId };
        _items[itemId] = item;
        RemoveIncompatibleAssignments(itemId, item);
        return item;
    }

    public bool Equip(string slotName, int itemId)
    {
        slotName = EquipmentSlotCatalog.NormalizeForGame(slotName, _gameId);
        if (!_items.TryGetValue(itemId, out var item)
            || !EquipmentSlotCatalog.IsAvailableForGame(slotName, _gameId)
            || !EquipmentSlotCatalog.IsCompatible(item, slotName))
        {
            return false;
        }

        if (EquipmentSlotCatalog.TryParseJewelSocket(slotName, out var socketNodeId))
        {
            _socketedJewelItemIds[socketNodeId] = itemId;
        }
        else
        {
            _loadouts[_activeLoadoutIndex].EquippedItemIds[slotName] = itemId;
        }
        return true;
    }

    public bool Unequip(string slotName)
    {
        slotName = EquipmentSlotCatalog.NormalizeForGame(slotName, _gameId);
        return EquipmentSlotCatalog.TryParseJewelSocket(slotName, out var socketNodeId)
            ? _socketedJewelItemIds.Remove(socketNodeId)
            : _loadouts[_activeLoadoutIndex].EquippedItemIds.Remove(slotName);
    }

    public int? EquippedItemId(string slotName)
    {
        slotName = EquipmentSlotCatalog.NormalizeForGame(slotName, _gameId);
        if (EquipmentSlotCatalog.TryParseJewelSocket(slotName, out var socketNodeId))
        {
            return _socketedJewelItemIds.GetValueOrDefault(socketNodeId) is var id && id > 0 ? id : null;
        }

        return _loadouts[_activeLoadoutIndex].EquippedItemIds.GetValueOrDefault(slotName) is var equippedId && equippedId > 0
            ? equippedId
            : null;
    }

    public bool DeleteItem(int itemId)
    {
        if (!_items.Remove(itemId))
        {
            return false;
        }

        foreach (var loadout in _loadouts)
        {
            foreach (var slot in loadout.EquippedItemIds.Where(pair => pair.Value == itemId).Select(pair => pair.Key).ToArray())
            {
                loadout.EquippedItemIds.Remove(slot);
            }
        }
        foreach (var socket in _socketedJewelItemIds.Where(pair => pair.Value == itemId).Select(pair => pair.Key).ToArray())
        {
            _socketedJewelItemIds.Remove(socket);
        }
        return true;
    }

    public int CreateLoadout(string name, bool copyActive)
    {
        _hasExplicitLoadouts = true;
        var assignments = copyActive
            ? new Dictionary<string, int>(_loadouts[_activeLoadoutIndex].EquippedItemIds, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);
        _loadouts.Add(new MutableLoadout(NextLoadoutId(), NonEmptyName(name, _loadouts.Count + 1), assignments));
        _activeLoadoutIndex = _loadouts.Count - 1;
        return _activeLoadoutIndex;
    }

    public bool DeleteActiveLoadout()
    {
        if (_loadouts.Count <= 1)
        {
            return false;
        }

        _loadouts.RemoveAt(_activeLoadoutIndex);
        _activeLoadoutIndex = Math.Min(_activeLoadoutIndex, _loadouts.Count - 1);
        return true;
    }

    public void RenameActiveLoadout(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _loadouts[_activeLoadoutIndex].Name = name.Trim();
        }
    }

    public bool SetActiveLoadout(int index)
    {
        if (index < 0 || index >= _loadouts.Count || index == _activeLoadoutIndex)
        {
            return false;
        }
        _activeLoadoutIndex = index;
        return true;
    }

    public IReadOnlyList<ImportedItem> ActiveGearItems() => ItemsFor(_loadouts[_activeLoadoutIndex]);

    public IReadOnlyList<ImportedItem> ActiveItems()
    {
        var items = ActiveGearItems().ToList();
        items.AddRange(_socketedJewelItemIds
            .OrderBy(pair => pair.Key)
            .Where(pair => _items.ContainsKey(pair.Value))
            .Select(pair => _items[pair.Value] with { Slot = $"Jewel {pair.Key}" }));
        return items;
    }

    public ImportedBuild ApplyTo(ImportedBuild build)
    {
        var variants = _hasExplicitLoadouts || _loadouts.Count > 1
            ? _loadouts.Select((loadout, index) => new ImportedItemSetVariant(index, loadout.Id, loadout.Name, ItemsFor(loadout))).ToArray()
            : [];
        var socketedJewels = _socketedJewelItemIds
            .OrderBy(pair => pair.Key)
            .Select(pair => new ImportedSocketedJewel(pair.Key, pair.Value))
            .ToArray();

        return build with
        {
            Items = ActiveGearItems(),
            ItemsById = new Dictionary<int, ImportedItem>(_items),
            ItemSetVariants = variants,
            ActiveItemSetVariantIndex = variants.Length == 0 ? 0 : _activeLoadoutIndex,
            SocketedJewels = socketedJewels,
        };
    }

    private Dictionary<string, int> Assignments(IEnumerable<ImportedItem> items)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in items.Where(item => !EquipmentSlotCatalog.TryParseJewelSocket(item.Slot, out _)))
        {
            var normalizedItem = NormalizeItem(item);
            var id = EnsureLoadedItem(normalizedItem);
            if (!string.IsNullOrWhiteSpace(normalizedItem.Slot)
                && EquipmentSlotCatalog.IsAvailableForGame(normalizedItem.Slot, _gameId))
            {
                result[normalizedItem.Slot] = id;
            }
        }
        return result;
    }

    private IReadOnlyList<ImportedItem> ItemsFor(MutableLoadout loadout) => loadout.EquippedItemIds
        .Where(pair => _items.ContainsKey(pair.Value))
        .OrderBy(pair => BuildPlannerItemSlots.SortOrder(pair.Key))
        .ThenBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => _items[pair.Value] with { Slot = pair.Key })
        .ToArray();

    private int EnsureLoadedItem(ImportedItem item)
    {
        if (item.Id > 0 && _items.ContainsKey(item.Id))
        {
            var existing = _items[item.Id];
            if (string.IsNullOrWhiteSpace(existing.Slot) && !string.IsNullOrWhiteSpace(item.Slot))
            {
                _items[item.Id] = existing with { Slot = item.Slot };
            }
            return item.Id;
        }

        var equivalent = _items.Values.FirstOrDefault(candidate =>
            candidate.Name == item.Name
            && candidate.BaseType == item.BaseType
            && candidate.RawText == item.RawText);
        if (equivalent is not null)
        {
            if (string.IsNullOrWhiteSpace(equivalent.Slot) && !string.IsNullOrWhiteSpace(item.Slot))
            {
                _items[equivalent.Id] = equivalent with { Slot = item.Slot };
            }
            return equivalent.Id;
        }

        return AddLoadedItem(item);
    }

    private int AddLoadedItem(ImportedItem item)
    {
        var id = item.Id > 0 && !_items.ContainsKey(item.Id) ? item.Id : NextItemId();
        _items[id] = item with { Id = id };
        _nextItemId = Math.Max(_nextItemId, id + 1);
        return id;
    }

    private ImportedItem NormalizeItem(ImportedItem item)
    {
        var normalizedSlot = EquipmentSlotCatalog.NormalizeForGame(item.Slot, _gameId);
        return normalizedSlot == item.Slot ? item : item with { Slot = normalizedSlot };
    }

    private void RemoveIncompatibleAssignments(int itemId, ImportedItem item)
    {
        foreach (var loadout in _loadouts)
        {
            foreach (var slot in loadout.EquippedItemIds
                         .Where(pair => pair.Value == itemId && !EquipmentSlotCatalog.IsCompatible(item, pair.Key))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                loadout.EquippedItemIds.Remove(slot);
            }
        }
        foreach (var socket in _socketedJewelItemIds
                     .Where(pair => pair.Value == itemId && !EquipmentSlotCatalog.IsCompatible(item, $"Jewel {pair.Key}"))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _socketedJewelItemIds.Remove(socket);
        }
    }

    private int NextItemId()
    {
        while (_items.ContainsKey(_nextItemId)) _nextItemId++;
        return _nextItemId++;
    }

    private int NextLoadoutId()
    {
        while (_loadouts.Any(loadout => loadout.Id == _nextLoadoutId)) _nextLoadoutId++;
        return _nextLoadoutId++;
    }

    private static string NonEmptyName(string? name, int number) =>
        string.IsNullOrWhiteSpace(name) ? $"Loadout {number}" : name.Trim();

    private sealed class MutableLoadout(int id, string name, Dictionary<string, int> equippedItemIds)
    {
        public int Id { get; } = id;
        public string Name { get; set; } = name;
        public Dictionary<string, int> EquippedItemIds { get; } = equippedItemIds;
        public EquipmentLoadout Snapshot() => new(Id, Name, new Dictionary<string, int>(EquippedItemIds));
    }
}
