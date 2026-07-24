using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Calculations;
using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Items;

namespace PathOfAvalonia.TreeApp.ViewModels;

public partial class EquipmentViewModel : ObservableObject
{
    private readonly EquipmentWorkspace _workspace;
    private readonly PassiveSpec? _spec;
    private IReadOnlyList<ImportedSkillSet> _importedSkillSets = [];
    private int _mainSocketGroupIndex;
    private bool _synchronizingLoadout;
    private bool _synchronizingSlots;
    private bool _synchronizingTreeJewels;
    private bool _synchronizingCharacterLevel;
    private bool _synchronizingSkillSet;
    private int? _editorItemId;
    private PassiveAllocationPreview _passiveAllocationPreview = PassiveAllocationPreview.None;
    private HashSet<int> _visibleJewelSocketIds = [];
    private BasicCharacterStats? _currentCalculatedStats;

    public EquipmentViewModel(PassiveSpec? spec = null)
    {
        _spec = spec;
        _workspace = new EquipmentWorkspace(spec?.Tree.GameId);
        if (_spec is not null)
        {
            _spec.SpecChanged += OnSpecChanged;
        }
        RefreshEquipment(preserveSelectedItemId: null);
        RaiseCharacterLevelForAllocations();
        RecalculateStats();
    }

    public event Action? EquipmentChanged;
    public event Action<PassiveStatPreviewViewModel?>? PassivePreviewChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial ObservableCollection<ItemGroupViewModel> Groups { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<EquipmentSlotViewModel> Slots { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<ItemViewModel> FilteredItems { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<EquipmentLoadoutOptionViewModel> LoadoutOptions { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<string> EditorSlotOptions { get; set; } = new();

    [ObservableProperty]
    public partial int SelectedLoadoutIndex { get; set; }

    [ObservableProperty]
    public partial string ActiveLoadoutName { get; set; } = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSlot))]
    [NotifyPropertyChangedFor(nameof(CanUnequipSelectedSlot))]
    [NotifyPropertyChangedFor(nameof(SelectedSlotTitle))]
    public partial EquipmentSlotViewModel? SelectedSlot { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLibraryItem))]
    [NotifyPropertyChangedFor(nameof(CanEquipSelectedItem))]
    public partial ItemViewModel? SelectedLibraryItem { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowCompatibleOnly { get; set; } = true;

    [ObservableProperty]
    public partial int SelectedWeaponSet { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsEditorOpen { get; set; }

    [ObservableProperty]
    public partial string EditorTitle { get; set; } = "Create custom item";

    [ObservableProperty]
    public partial string EditorRawText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditorSelectedSlot { get; set; } = "Helmet";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditorError))]
    public partial string EditorError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsConfirmingItemDelete { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMetrics))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial ImportedBuildMetricsViewModel? Metrics { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCalculatedStats))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial BasicCharacterStatsViewModel? CalculatedStats { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTreeCalculatedStats))]
    public partial BasicCharacterStatsViewModel? TreeCalculatedStats { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPassivePreview))]
    public partial PassiveStatPreviewViewModel? PassivePreview { get; set; }

    [ObservableProperty]
    public partial int CharacterLevel { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkillGroups))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial ObservableCollection<ImportedSkillGroupViewModel> SkillGroups { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSkillGroup))]
    public partial ImportedSkillGroupViewModel? SelectedSkillGroup { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkillSetVariants))]
    public partial ObservableCollection<ImportedSkillSetOptionViewModel> SkillSetOptions { get; set; } = new();

    [ObservableProperty]
    public partial int SelectedSkillSetIndex { get; set; }

    [ObservableProperty]
    public partial string EmptyMessage { get; set; } = "Import a build to see equipment.";

    public bool HasItems => _workspace.Items.Count > 0;
    public bool HasMetrics => Metrics is not null;
    public bool HasCalculatedStats => CalculatedStats is not null;
    public bool HasTreeCalculatedStats => TreeCalculatedStats is not null;
    public bool HasPassivePreview => PassivePreview is not null;
    public bool HasSkillGroups => SkillGroups.Count > 0;
    public bool HasSelectedSkillGroup => SelectedSkillGroup is not null;
    public bool HasContent => HasItems || HasMetrics || HasCalculatedStats || HasSkillGroups;
    public bool HasSkillSetVariants => SkillSetOptions.Count > 1;
    public bool HasSelectedSlot => SelectedSlot is not null;
    public bool HasSelectedLibraryItem => SelectedLibraryItem is not null;
    public bool CanUnequipSelectedSlot => SelectedSlot?.HasItem == true;
    public bool CanEquipSelectedItem => SelectedSlot is not null
        && SelectedLibraryItem is not null
        && EquipmentSlotCatalog.IsCompatible(SelectedLibraryItem.Item, SelectedSlot.Name)
        && SelectedSlot.Item?.ItemId != SelectedLibraryItem.ItemId;
    public bool CanDeleteLoadout => LoadoutOptions.Count > 1;
    public bool HasEditorError => !string.IsNullOrWhiteSpace(EditorError);
    public string SelectedSlotTitle => SelectedSlot is null ? "Item library" : $"Items for {SelectedSlot.DisplayName}";
    public string LibrarySummary => FilteredItems.Count == 1 ? "1 item" : $"{FilteredItems.Count} items";
    public bool IsPrimaryWeaponSet => SelectedWeaponSet == 1;
    public bool IsSwapWeaponSet => SelectedWeaponSet == 2;

    public void LoadBuild(ImportedBuild build)
    {
        _workspace.Load(build);
        _synchronizingCharacterLevel = true;
        CharacterLevel = Math.Max(build.CharacterLevel, _spec?.MinimumCharacterLevelForAllocations() ?? 1);
        _synchronizingCharacterLevel = false;
        Metrics = build.Metrics.Source != ImportedMetricSource.None || !string.IsNullOrWhiteSpace(build.Metrics.ErrorMessage)
            ? ImportedBuildMetricsViewModel.FromImported(build.Metrics)
            : null;
        _importedSkillSets = build.Skills.SkillSets;
        _mainSocketGroupIndex = build.Skills.MainSocketGroupIndex;
        SkillSetOptions = new ObservableCollection<ImportedSkillSetOptionViewModel>(
            _importedSkillSets.Select(set => new ImportedSkillSetOptionViewModel(set.Index, set.DisplayName)));
        _synchronizingSkillSet = true;
        SelectedSkillSetIndex = build.Skills.ActiveSkillSetIndex >= 0 && build.Skills.ActiveSkillSetIndex < _importedSkillSets.Count
            ? build.Skills.ActiveSkillSetIndex
            : 0;
        _synchronizingSkillSet = false;
        RefreshSkillGroups();
        RefreshEquipment(preserveSelectedItemId: null);
        RecalculateStats();
        IsDirty = false;
    }

    public void Clear()
    {
        _workspace.Reset();
        _synchronizingCharacterLevel = true;
        CharacterLevel = 1;
        _synchronizingCharacterLevel = false;
        Metrics = null;
        _importedSkillSets = [];
        _mainSocketGroupIndex = 0;
        SkillSetOptions = new ObservableCollection<ImportedSkillSetOptionViewModel>();
        _synchronizingSkillSet = true;
        SelectedSkillSetIndex = 0;
        _synchronizingSkillSet = false;
        SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>();
        SelectedSkillGroup = null;
        IsEditorOpen = false;
        IsConfirmingItemDelete = false;
        RefreshEquipment(preserveSelectedItemId: null);
        SynchronizeTreeJewels();
        RecalculateStats();
        IsDirty = false;
    }

    public void MarkUnsupported()
    {
        _workspace.Reset();
        Metrics = null;
        _importedSkillSets = [];
        _mainSocketGroupIndex = 0;
        SkillSetOptions = new ObservableCollection<ImportedSkillSetOptionViewModel>();
        _synchronizingSkillSet = true;
        SelectedSkillSetIndex = 0;
        _synchronizingSkillSet = false;
        SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>();
        SelectedSkillGroup = null;
        EmptyMessage = "Equipment is not available for this game yet.";
        RefreshEquipment(preserveSelectedItemId: null);
        EmptyMessage = "Equipment is not available for this game yet.";
        RecalculateStats();
    }

    public ImportedBuild ApplyToBuild(ImportedBuild build) => _workspace.ApplyTo(build) with
    {
        CharacterLevel = CharacterLevel,
        Skills = build.Skills with { ActiveSkillSetIndex = SelectedSkillSetIndex },
    };

    public void SetPassivePreview(PassiveAllocationPreview preview)
    {
        if (_passiveAllocationPreview.Kind == preview.Kind
            && _passiveAllocationPreview.TargetNodeId == preview.TargetNodeId
            && _passiveAllocationPreview.HasUnmodeledJewelEffectChange == preview.HasUnmodeledJewelEffectChange
            && _passiveAllocationPreview.NodeIds.SetEquals(preview.NodeIds))
        {
            return;
        }

        _passiveAllocationPreview = preview;
        // Hover changes do not mutate the spec, equipment, level, or weapon set.
        // Reuse the current side of the comparison and calculate only the
        // projected allocation state.
        RecalculateStats(recalculateCurrent: false);
    }

    partial void OnCharacterLevelChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 100);
        if (clamped != value)
        {
            CharacterLevel = clamped;
            return;
        }
        if (!_synchronizingCharacterLevel)
        {
            NotifyEquipmentChanged();
        }
    }

    partial void OnSelectedLoadoutIndexChanged(int value)
    {
        if (_synchronizingLoadout || !_workspace.SetActiveLoadout(value))
        {
            return;
        }
        RefreshEquipment(SelectedLibraryItem?.ItemId);
        NotifyEquipmentChanged();
    }

    partial void OnActiveLoadoutNameChanged(string value)
    {
        if (_synchronizingLoadout || string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        _workspace.RenameActiveLoadout(value);
        RefreshLoadouts();
        NotifyEquipmentChanged();
    }

    partial void OnSelectedSlotChanged(EquipmentSlotViewModel? value)
    {
        if (_synchronizingSlots)
        {
            return;
        }
        RefreshLibrary(value?.Item?.ItemId);
        OnPropertyChanged(nameof(CanEquipSelectedItem));
    }

    partial void OnSelectedLibraryItemChanged(ItemViewModel? value)
    {
        IsConfirmingItemDelete = false;
        OnPropertyChanged(nameof(CanEquipSelectedItem));
    }

    partial void OnSearchTextChanged(string value) => RefreshLibrary(SelectedLibraryItem?.ItemId);
    partial void OnShowCompatibleOnlyChanged(bool value) => RefreshLibrary(SelectedLibraryItem?.ItemId);

    [RelayCommand]
    private void UsePrimaryWeapons()
    {
        if (SelectedWeaponSet == 1) return;
        SelectedWeaponSet = 1;
        OnPropertyChanged(nameof(IsPrimaryWeaponSet));
        OnPropertyChanged(nameof(IsSwapWeaponSet));
        RefreshSlots();
        RecalculateStats();
    }

    [RelayCommand]
    private void UseSwapWeapons()
    {
        if (SelectedWeaponSet == 2) return;
        SelectedWeaponSet = 2;
        OnPropertyChanged(nameof(IsPrimaryWeaponSet));
        OnPropertyChanged(nameof(IsSwapWeaponSet));
        RefreshSlots();
        RecalculateStats();
    }

    [RelayCommand]
    private void CreateLoadout()
    {
        _workspace.CreateLoadout($"Loadout {_workspace.Loadouts.Count + 1}", copyActive: false);
        RefreshEquipment(preserveSelectedItemId: null);
        NotifyEquipmentChanged();
    }

    [RelayCommand]
    private void DuplicateLoadout()
    {
        _workspace.CreateLoadout($"{_workspace.ActiveLoadout.Name} copy", copyActive: true);
        RefreshEquipment(SelectedLibraryItem?.ItemId);
        NotifyEquipmentChanged();
    }

    [RelayCommand]
    private void DeleteLoadout()
    {
        if (!_workspace.DeleteActiveLoadout()) return;
        RefreshEquipment(preserveSelectedItemId: null);
        NotifyEquipmentChanged();
    }

    [RelayCommand]
    private void NewItem()
    {
        var slot = SelectedSlot?.Name ?? EditorSlotOptions.FirstOrDefault() ?? "Helmet";
        _editorItemId = null;
        EditorTitle = "Create custom item";
        EditorSelectedSlot = slot;
        EditorRawText = NewItemTemplate(slot);
        EditorError = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void EditSelectedItem()
    {
        if (SelectedLibraryItem is not { } selected) return;
        _editorItemId = selected.ItemId;
        EditorTitle = $"Edit {selected.Name}";
        EditorSelectedSlot = selected.Slot;
        EditorRawText = string.IsNullOrWhiteSpace(selected.RawText) ? BuildFallbackRaw(selected.Item) : selected.RawText;
        EditorError = string.Empty;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void DuplicateSelectedItem()
    {
        if (SelectedLibraryItem is not { } selected) return;
        var raw = string.IsNullOrWhiteSpace(selected.RawText) ? BuildFallbackRaw(selected.Item) : selected.RawText;
        var copy = _workspace.AddItem(raw, selected.Slot);
        RefreshEquipment(copy.Id);
        NotifyEquipmentChanged();
    }

    [RelayCommand]
    private void SaveItem()
    {
        EditorError = ValidateEditor();
        if (HasEditorError)
        {
            return;
        }

        ImportedItem item;
        if (_editorItemId is { } itemId)
        {
            item = _workspace.UpdateItem(itemId, EditorRawText, EditorSelectedSlot);
        }
        else
        {
            item = _workspace.AddItem(EditorRawText, EditorSelectedSlot);
            if (SelectedSlot is { } slot && EquipmentSlotCatalog.IsCompatible(item, slot.Name))
            {
                _workspace.Equip(slot.Name, item.Id);
            }
        }

        _editorItemId = null;
        IsEditorOpen = false;
        SynchronizeTreeJewels();
        RefreshEquipment(item.Id);
        NotifyEquipmentChanged();
    }

    [RelayCommand]
    private void CancelEditor()
    {
        _editorItemId = null;
        IsEditorOpen = false;
        EditorError = string.Empty;
    }

    [RelayCommand]
    private void EquipSelectedItem()
    {
        if (SelectedSlot is not { } slot || SelectedLibraryItem is not { } item || !_workspace.Equip(slot.Name, item.ItemId))
        {
            return;
        }
        SynchronizeTreeJewels();
        RefreshEquipment(item.ItemId);
        NotifyEquipmentChanged();
    }

    [RelayCommand]
    private void UnequipSelectedSlot()
    {
        if (SelectedSlot is not { } slot || !_workspace.Unequip(slot.Name)) return;
        SynchronizeTreeJewels();
        RefreshEquipment(preserveSelectedItemId: null);
        NotifyEquipmentChanged();
    }

    [RelayCommand]
    private void RequestDeleteSelectedItem()
    {
        if (SelectedLibraryItem is not null)
        {
            IsConfirmingItemDelete = true;
        }
    }

    [RelayCommand]
    private void CancelDeleteSelectedItem() => IsConfirmingItemDelete = false;

    [RelayCommand]
    private void ConfirmDeleteSelectedItem()
    {
        if (SelectedLibraryItem is not { } selected || !_workspace.DeleteItem(selected.ItemId)) return;
        IsConfirmingItemDelete = false;
        IsEditorOpen = false;
        SynchronizeTreeJewels();
        RefreshEquipment(preserveSelectedItemId: null);
        NotifyEquipmentChanged();
    }

    partial void OnSelectedSkillSetIndexChanged(int value)
    {
        RefreshSkillGroups();
        if (!_synchronizingSkillSet)
        {
            NotifyEquipmentChanged();
        }
    }

    private void RefreshEquipment(int? preserveSelectedItemId)
    {
        RefreshLoadouts();
        RefreshSlots();
        RefreshGroups();
        RefreshLibrary(preserveSelectedItemId);
        EmptyMessage = HasItems
            ? string.Empty
            : "Your item library is empty. Create an item or paste copied item text to get started.";
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(CanDeleteLoadout));
        OnPropertyChanged(nameof(CanUnequipSelectedSlot));
        OnPropertyChanged(nameof(CanEquipSelectedItem));
    }

    private void RefreshLoadouts()
    {
        _synchronizingLoadout = true;
        LoadoutOptions = new ObservableCollection<EquipmentLoadoutOptionViewModel>(
            _workspace.Loadouts.Select((loadout, index) => new EquipmentLoadoutOptionViewModel(index, loadout.Name)));
        SelectedLoadoutIndex = _workspace.ActiveLoadoutIndex;
        ActiveLoadoutName = _workspace.ActiveLoadout.Name;
        _synchronizingLoadout = false;
    }

    private void RefreshSlots()
    {
        var selectedName = SelectedSlot?.Name;
        var definitions = EquipmentSlotCatalog.ForGame(_spec?.Tree.GameId)
            .Where(slot => SelectedWeaponSet == 1
                ? !slot.Name.EndsWith(" Swap", StringComparison.Ordinal)
                : slot.Name.EndsWith(" Swap", StringComparison.Ordinal) || !slot.Name.StartsWith("Weapon", StringComparison.Ordinal))
            .ToList();

        var jewelSocketIds = new HashSet<int>();
        if (_spec is not null)
        {
            foreach (var node in _spec.Tree.Nodes.Values.Where(node =>
                         node.Type == NodeType.JewelSocket
                         && node.Name != "Charm Socket"
                         && _spec.IsAllocated(node.Id)))
            {
                jewelSocketIds.Add(node.Id);
            }
            foreach (var node in _spec.ActiveSubgraphs.Values
                         .SelectMany(graph => graph.Nodes)
                         .Where(node => node.Type == NodeType.JewelSocket && _spec.IsAllocated(node.Id)))
            {
                jewelSocketIds.Add(node.Id);
            }
        }
        _visibleJewelSocketIds = jewelSocketIds;
        definitions.AddRange(jewelSocketIds.Order().Select((id, index) => EquipmentSlotCatalog.Jewel(id, index)));

        var slots = new List<EquipmentSlotViewModel>();
        EquipmentSlotCategory? previousCategory = null;
        foreach (var definition in definitions.OrderBy(slot => slot.SortOrder))
        {
            var item = _workspace.EquippedItemId(definition.Name) is { } itemId
                && _workspace.Items.TryGetValue(itemId, out var imported)
                    ? ItemViewModel.FromImported(imported, definition.Name, $"Equipped · {DisplaySlotName(definition.Name)}")
                    : null;
            var showHeader = previousCategory != definition.Category;
            slots.Add(new EquipmentSlotViewModel(
                definition.Name,
                DisplaySlotName(definition.Name),
                definition.ShortName,
                CategoryName(definition.Category),
                showHeader,
                item));
            previousCategory = definition.Category;
        }

        _synchronizingSlots = true;
        Slots = new ObservableCollection<EquipmentSlotViewModel>(slots);
        EditorSlotOptions = new ObservableCollection<string>(slots.Select(slot => slot.Name));
        SelectedSlot = slots.FirstOrDefault(slot => slot.Name == selectedName) ?? slots.FirstOrDefault();
        _synchronizingSlots = false;
        OnPropertyChanged(nameof(HasSelectedSlot));
        OnPropertyChanged(nameof(SelectedSlotTitle));
    }

    private void RefreshLibrary(int? preserveSelectedItemId)
    {
        var selectedId = preserveSelectedItemId ?? SelectedLibraryItem?.ItemId;
        var query = SearchText.Trim();
        var equippedSlots = _workspace.ActiveItems()
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(item => DisplaySlotName(item.Slot))));
        var items = _workspace.Items.Values
            .Where(item => !ShowCompatibleOnly || SelectedSlot is null || EquipmentSlotCatalog.IsCompatible(item, SelectedSlot.Name))
            .Where(item => query.Length == 0
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.BaseType.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.RawText.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => EquipmentSlotCatalog.Family(item.Slot, item))
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => ItemViewModel.FromImported(
                item,
                usageText: equippedSlots.TryGetValue(item.Id, out var slots) ? $"Equipped · {slots}" : "Available"))
            .ToArray();
        FilteredItems = new ObservableCollection<ItemViewModel>(items);
        SelectedLibraryItem = selectedId is { } id ? items.FirstOrDefault(item => item.ItemId == id) : null;
        if (SelectedLibraryItem is null && SelectedSlot?.Item is { } equipped)
        {
            SelectedLibraryItem = items.FirstOrDefault(item => item.ItemId == equipped.ItemId);
        }
        OnPropertyChanged(nameof(LibrarySummary));
    }

    private void RefreshGroups()
    {
        var groups = new List<ItemGroupViewModel>();
        var activeGear = _workspace.ActiveGearItems();
        AddGroup(groups, "Equipment", activeGear.Where(item => GroupName(item) == "Equipment"));
        AddGroup(groups, "Flasks & Charms", activeGear.Where(item => GroupName(item) == "Flasks & Charms"));
        AddGroup(groups, "Jewels", activeGear.Where(item => GroupName(item) == "Jewels"));
        var socketed = _workspace.SocketedJewelItemIds
            .OrderBy(pair => pair.Key)
            .Where(pair => _workspace.Items.ContainsKey(pair.Value))
            .Select(pair => ItemViewModel.FromImported(_workspace.Items[pair.Value], $"Jewel {pair.Key}"))
            .ToArray();
        if (socketed.Length > 0)
        {
            groups.Add(new ItemGroupViewModel("Socketed Tree Jewels", socketed));
        }
        Groups = new ObservableCollection<ItemGroupViewModel>(groups);
    }

    private static void AddGroup(List<ItemGroupViewModel> groups, string name, IEnumerable<ImportedItem> items)
    {
        var viewModels = items.Select(item => ItemViewModel.FromImported(item)).ToArray();
        if (viewModels.Length > 0)
        {
            groups.Add(new ItemGroupViewModel(name, viewModels));
        }
    }

    private void SynchronizeTreeJewels()
    {
        if (_spec is null || _synchronizingTreeJewels)
        {
            return;
        }

        _synchronizingTreeJewels = true;
        try
        {
            var socketIds = _spec.SocketedJewels.Keys.Concat(_workspace.SocketedJewelItemIds.Keys).Distinct().ToArray();
            foreach (var socketId in socketIds)
            {
                ImportedItem? item = null;
                if (_workspace.SocketedJewelItemIds.TryGetValue(socketId, out var itemId))
                {
                    _workspace.Items.TryGetValue(itemId, out item);
                }
                _spec.SetSocketedJewel(socketId, item);
            }
        }
        finally
        {
            _synchronizingTreeJewels = false;
        }
    }

    private void OnSpecChanged()
    {
        RefreshForSpecChange(_passiveAllocationPreview);
    }

    internal void UseCoordinatedSpecChanges()
    {
        if (_spec is not null)
        {
            _spec.SpecChanged -= OnSpecChanged;
        }
    }

    internal void RefreshForSpecChange(PassiveAllocationPreview preview)
    {
        _passiveAllocationPreview = preview;
        if (!_synchronizingTreeJewels && VisibleJewelSocketIdsChanged())
        {
            RefreshSlots();
            RefreshLibrary(SelectedLibraryItem?.ItemId);
        }
        RaiseCharacterLevelForAllocations();
        RecalculateStats();
    }

    private bool VisibleJewelSocketIdsChanged()
    {
        if (_spec is null)
        {
            return _visibleJewelSocketIds.Count != 0;
        }

        var currentCount = 0;
        foreach (var node in _spec.Tree.Nodes.Values)
        {
            if (node.Type == NodeType.JewelSocket
                && node.Name != "Charm Socket"
                && _spec.IsAllocated(node.Id))
            {
                currentCount++;
                if (!_visibleJewelSocketIds.Contains(node.Id))
                {
                    return true;
                }
            }
        }
        foreach (var subgraph in _spec.ActiveSubgraphs.Values)
        {
            foreach (var node in subgraph.Nodes)
            {
                if (node.Type == NodeType.JewelSocket && _spec.IsAllocated(node.Id))
                {
                    currentCount++;
                    if (!_visibleJewelSocketIds.Contains(node.Id))
                    {
                        return true;
                    }
                }
            }
        }
        return currentCount != _visibleJewelSocketIds.Count;
    }

    private void RaiseCharacterLevelForAllocations()
    {
        if (_spec is null)
        {
            return;
        }

        var minimumLevel = _spec.MinimumCharacterLevelForAllocations();
        if (minimumLevel <= CharacterLevel)
        {
            return;
        }

        _synchronizingCharacterLevel = true;
        CharacterLevel = minimumLevel;
        _synchronizingCharacterLevel = false;
    }

    private void NotifyEquipmentChanged()
    {
        RecalculateStats();
        IsDirty = true;
        EquipmentChanged?.Invoke();
    }

    private void RecalculateStats(bool recalculateCurrent = true)
    {
        if (_spec is null)
        {
            _currentCalculatedStats = null;
            CalculatedStats = null;
            TreeCalculatedStats = null;
            PassivePreview = null;
            PassivePreviewChanged?.Invoke(null);
            return;
        }

        var activeItems = _workspace.ActiveItems().ToArray();
        if (recalculateCurrent || _currentCalculatedStats is null)
        {
            _currentCalculatedStats = BasicStatCalculator.Calculate(
                _spec,
                activeItems,
                CharacterLevel,
                SelectedWeaponSet);
            CalculatedStats = BasicCharacterStatsViewModel.FromCalculated(_currentCalculatedStats);
        }
        var current = _currentCalculatedStats;

        if (_passiveAllocationPreview.IsEmpty)
        {
            TreeCalculatedStats = CalculatedStats;
            PassivePreview = null;
        }
        else
        {
            var projected = BasicStatCalculator.Calculate(
                _spec,
                activeItems,
                CharacterLevel,
                SelectedWeaponSet,
                _passiveAllocationPreview);
            TreeCalculatedStats = BasicCharacterStatsViewModel.FromCalculated(
                projected,
                current);
            PassivePreview = PassiveStatPreviewViewModel.From(
                _passiveAllocationPreview,
                TreeCalculatedStats.Changes);
        }

        PassivePreviewChanged?.Invoke(PassivePreview);
    }

    private string ValidateEditor()
    {
        if (string.IsNullOrWhiteSpace(EditorSelectedSlot)) return "Choose the item's default slot.";
        if (string.IsNullOrWhiteSpace(EditorRawText)) return "Enter or paste item text.";
        var parsed = RawItemParser.Parse(EditorSelectedSlot, EditorRawText);
        if (string.IsNullOrWhiteSpace(parsed.Name)) return "Item text needs a name after the Rarity line.";
        if (string.IsNullOrWhiteSpace(parsed.BaseType)) return "Item text needs a base type.";
        return string.Empty;
    }

    private static string NewItemTemplate(string slotName)
    {
        var (name, baseType, mod) = EquipmentSlotCatalog.Family(slotName, item: null) switch
        {
            "Jewel" => ("New Jewel", "Cobalt Jewel", "+10 to Intelligence"),
            "Life Flask" => ("New Life Flask", "Life Flask", "50% increased Amount Recovered"),
            "Mana Flask" => ("New Mana Flask", "Mana Flask", "50% increased Amount Recovered"),
            "Flask" => ("New Flask", "Utility Flask", "20% increased Duration"),
            "Charm" => ("New Charm", "Charm", "+10% to Fire Resistance"),
            "Ring" => ("New Ring", "Ruby Ring", "+30 to maximum Life"),
            "Amulet" => ("New Amulet", "Gold Amulet", "+20 to all Attributes"),
            "Belt" => ("New Belt", "Leather Belt", "+60 to maximum Life"),
            "Weapon" => ("New Weapon", "Weapon Base", "100% increased Physical Damage"),
            _ => ("New Item", $"{slotName} Base", "+50 to maximum Life"),
        };
        return $"Rarity: Rare\n{name}\n{baseType}\n--------\n{mod}";
    }

    private static string BuildFallbackRaw(ImportedItem item)
    {
        var header = item.Rarity.Equals("Rare", StringComparison.OrdinalIgnoreCase)
            || item.Rarity.Equals("Unique", StringComparison.OrdinalIgnoreCase)
                ? $"Rarity: {item.Rarity}\n{item.Name}\n{item.BaseType}"
                : $"Rarity: {item.Rarity}\n{item.Name}";
        return header + "\n--------";
    }

    private static string DisplaySlotName(string slotName) =>
        SelectedWeaponLabel(slotName) ?? slotName;

    private static string? SelectedWeaponLabel(string slotName) => slotName switch
    {
        "Weapon 1 Swap" => "Weapon 1",
        "Weapon 2 Swap" => "Weapon 2",
        _ => null,
    };

    private static string CategoryName(EquipmentSlotCategory category) => category switch
    {
        EquipmentSlotCategory.Weapons => "Weapons",
        EquipmentSlotCategory.Armour => "Armour",
        EquipmentSlotCategory.Jewellery => "Jewellery",
        EquipmentSlotCategory.Flasks => "Flasks",
        EquipmentSlotCategory.Charms => "Charms",
        EquipmentSlotCategory.Jewels => "Passive tree jewels",
        _ => category.ToString(),
    };

    private void RefreshSkillGroups()
    {
        if (SelectedSkillSetIndex < 0 || SelectedSkillSetIndex >= _importedSkillSets.Count)
        {
            SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>();
            SelectedSkillGroup = null;
            return;
        }

        var set = _importedSkillSets[SelectedSkillSetIndex];
        SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>(
            set.Groups.Select(group => ImportedSkillGroupViewModel.FromImported(
                set,
                group,
                group.Index == _mainSocketGroupIndex)));
        SelectedSkillGroup = SkillGroups.FirstOrDefault(group => group.IsMainSkillGroup)
            ?? SkillGroups.FirstOrDefault();
    }

    private static string GroupName(ImportedItem item)
    {
        if (item.Slot.Contains("Flask", StringComparison.OrdinalIgnoreCase)
            || item.Slot.Contains("Charm", StringComparison.OrdinalIgnoreCase)
            || item.BaseType.Contains("Charm", StringComparison.OrdinalIgnoreCase))
        {
            return "Flasks & Charms";
        }

        if (item.Slot.Contains("Jewel", StringComparison.OrdinalIgnoreCase)
            || item.BaseType.Contains("Jewel", StringComparison.OrdinalIgnoreCase))
        {
            return "Jewels";
        }

        return "Equipment";
    }
}

public sealed record EquipmentLoadoutOptionViewModel(int Index, string Name);

public sealed class BasicCharacterStatsViewModel
{
    public BasicCharacterStats Values { get; }
    public string SourceText { get; }
    public string CoverageText { get; }
    public string WarningText { get; }
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);
    public ObservableCollection<CalculatedStatMetricViewModel> Stats { get; }
    public ObservableCollection<CalculatedStatGroupViewModel> StatGroups { get; }
    public ObservableCollection<CalculatedStatMetricViewModel> Changes { get; }

    private BasicCharacterStatsViewModel(
        BasicCharacterStats stats,
        BasicCharacterStats? baseline)
    {
        Values = stats;
        SourceText = baseline is null
            ? "Calculated locally from the current tree and equipment"
            : "Projected from the hovered passive change";
        CoverageText = $"Level {stats.Level} · worst resistance penalty ({BasicStatCalculator.WorstResistancePenalty}%) · {stats.Coverage.AppliedLineCount} basic-stat lines applied";
        var warnings = new List<string>();
        if (stats.Coverage.UnsupportedRelevantLineCount > 0)
        {
            warnings.Add($"{stats.Coverage.UnsupportedRelevantLineCount} relevant line(s) need the full modifier/condition system.");
        }
        if (stats.Coverage.HasIncompleteItemDefences)
        {
            warnings.Add("Saved PoB item text omitted final item defences; ES, armour, evasion, and ward are lower bounds.");
        }
        if (stats.Coverage.HasIncompleteShieldBlock)
        {
            warnings.Add("Saved shield text omitted its final block property; block is a lower bound.");
        }
        warnings.Add("Flasks, buffs, reservations, and conditional effects are excluded.");
        WarningText = "Experimental subset: " + string.Join(" ", warnings);

        var rows = new List<CalculatedStatMetricViewModel>
        {
            IntegerRow("Strength", stats.Strength, baseline?.Strength, "Attributes", CalculatedStatTone.Strength),
            IntegerRow("Dexterity", stats.Dexterity, baseline?.Dexterity, "Attributes", CalculatedStatTone.Dexterity),
            IntegerRow("Intelligence", stats.Intelligence, baseline?.Intelligence, "Attributes", CalculatedStatTone.Intelligence),
            IntegerRow("Total Life", stats.Life, baseline?.Life, "Pools", CalculatedStatTone.Life),
            IntegerRow("Total Mana", stats.Mana, baseline?.Mana, "Pools", CalculatedStatTone.Mana),
            IntegerRow(
                "Energy Shield",
                stats.EnergyShield,
                baseline?.EnergyShield,
                "Pools",
                CalculatedStatTone.EnergyShield,
                stats.Coverage.HasIncompleteItemDefences),
            DecimalRow("Life Regen", stats.LifeRegeneration, baseline?.LifeRegeneration, "Recovery", CalculatedStatTone.Life),
            DecimalRow("Mana Regen", stats.ManaRegeneration, baseline?.ManaRegeneration, "Recovery", CalculatedStatTone.Mana),
            IntegerRow(
                "Armour",
                stats.Armour,
                baseline?.Armour,
                "Defences",
                CalculatedStatTone.Armour,
                stats.Coverage.HasIncompleteItemDefences),
            IntegerRow(
                "Evasion",
                stats.Evasion,
                baseline?.Evasion,
                "Defences",
                CalculatedStatTone.Evasion,
                stats.Coverage.HasIncompleteItemDefences),
        };
        if (stats.Ward > 0 || baseline?.Ward > 0)
        {
            rows.Add(IntegerRow(
                "Ward",
                stats.Ward,
                baseline?.Ward,
                "Defences",
                CalculatedStatTone.Ward,
                stats.Coverage.HasIncompleteItemDefences));
        }
        rows.AddRange(
        [
            PercentRow(
                "Block Chance",
                stats.BlockChance,
                baseline?.BlockChance,
                "Avoidance",
                CalculatedStatTone.Avoidance,
                stats.Coverage.HasIncompleteShieldBlock),
            PercentRow(
                "Spell Block Chance",
                stats.SpellBlockChance,
                baseline?.SpellBlockChance,
                "Avoidance",
                CalculatedStatTone.Avoidance),
            PercentRow(
                "Spell Suppression",
                stats.SpellSuppressionChance,
                baseline?.SpellSuppressionChance,
                "Avoidance",
                CalculatedStatTone.Avoidance),
            ResistanceRow(
                "Fire Resistance",
                stats.FireResistance,
                baseline?.FireResistance,
                "Resistances",
                CalculatedStatTone.Fire),
            ResistanceRow(
                "Cold Resistance",
                stats.ColdResistance,
                baseline?.ColdResistance,
                "Resistances",
                CalculatedStatTone.Cold),
            ResistanceRow(
                "Lightning Resistance",
                stats.LightningResistance,
                baseline?.LightningResistance,
                "Resistances",
                CalculatedStatTone.Lightning),
            ResistanceRow(
                "Chaos Resistance",
                stats.ChaosResistance,
                baseline?.ChaosResistance,
                "Resistances",
                CalculatedStatTone.Chaos),
            SignedPercentRow(
                "Movement Speed",
                stats.MovementSpeedModifier,
                baseline?.MovementSpeedModifier,
                "Movement",
                CalculatedStatTone.Movement),
        ]);
        Stats = new ObservableCollection<CalculatedStatMetricViewModel>(rows);
        StatGroups = new ObservableCollection<CalculatedStatGroupViewModel>(
            rows.GroupBy(row => row.Group)
                .Select(group => new CalculatedStatGroupViewModel(group.Key, group)));
        Changes = new ObservableCollection<CalculatedStatMetricViewModel>(rows.Where(row => row.HasChange));
    }

    public static BasicCharacterStatsViewModel FromCalculated(
        BasicCharacterStats stats,
        BasicCharacterStats? baseline = null) =>
        new(stats, baseline);

    private static CalculatedStatMetricViewModel IntegerRow(
        string label,
        int value,
        int? baseline,
        string group,
        CalculatedStatTone tone,
        bool partial = false) =>
        Row(label, PartialNumber(value, partial), group, tone, value - baseline, IntegerDelta(value, baseline));

    private static CalculatedStatMetricViewModel DecimalRow(
        string label,
        double value,
        double? baseline,
        string group,
        CalculatedStatTone tone) =>
        Row(label, Decimal(value), group, tone, value - baseline, DecimalDelta(value, baseline));

    private static CalculatedStatMetricViewModel PercentRow(
        string label,
        int value,
        int? baseline,
        string group,
        CalculatedStatTone tone,
        bool partial = false) =>
        Row(label, PartialPercent(value, partial), group, tone, value - baseline, IntegerDelta(value, baseline, "%"));

    private static CalculatedStatMetricViewModel SignedPercentRow(
        string label,
        int value,
        int? baseline,
        string group,
        CalculatedStatTone tone) =>
        Row(label, SignedPercent(value), group, tone, value - baseline, IntegerDelta(value, baseline, "%"));

    private static CalculatedStatMetricViewModel ResistanceRow(
        string label,
        BasicResistance value,
        BasicResistance? baseline,
        string group,
        CalculatedStatTone tone)
    {
        var uncappedChange = baseline is null ? 0 : value.Uncapped - baseline.Uncapped;
        var maximumChange = baseline is null ? 0 : value.Maximum - baseline.Maximum;
        var parts = new List<string>();
        if (uncappedChange != 0)
        {
            parts.Add($"{uncappedChange:+0;-0;0}%");
        }
        if (maximumChange != 0)
        {
            parts.Add($"max {maximumChange:+0;-0;0}%");
        }
        return Row(
            label,
            Resistance(value),
            group,
            tone,
            uncappedChange != 0 ? uncappedChange : maximumChange,
            parts.Count == 0 ? string.Empty : $"({string.Join(", ", parts)})");
    }

    private static CalculatedStatMetricViewModel Row(
        string label,
        string value,
        string group,
        CalculatedStatTone tone,
        double? change,
        string deltaText) => new(
            label,
            value,
            group,
            tone,
            deltaText,
            change > 0,
            change < 0);

    private static string IntegerDelta(int value, int? baseline, string suffix = "")
    {
        if (baseline is null || value == baseline.Value)
        {
            return string.Empty;
        }
        return $"({value - baseline.Value:+#,0;-#,0;0}{suffix})";
    }

    private static string DecimalDelta(double value, double? baseline)
    {
        if (baseline is null || Math.Abs(value - baseline.Value) < 0.05)
        {
            return string.Empty;
        }
        return $"({value - baseline.Value:+0.0;-0.0;0.0})";
    }

    private static string Number(int value) => value.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
    private static string PartialNumber(int value, bool partial) => partial ? $"{Number(value)}+ (partial)" : Number(value);
    private static string Decimal(double value) => value.ToString("N1", System.Globalization.CultureInfo.CurrentCulture);
    private static string Percent(int value) => $"{value}%";
    private static string PartialPercent(int value, bool partial) => partial ? $"{value}%+ (partial)" : Percent(value);
    private static string SignedPercent(int value) => $"{value:+0;-0;0}%";
    private static string Resistance(BasicResistance value) => value.OverCap > 0
        ? $"{value.Capped}% (+{value.OverCap}%)"
        : $"{value.Capped}%";
}

public sealed record CalculatedStatMetricViewModel(
    string Label,
    string Value,
    string Group,
    CalculatedStatTone Tone,
    string DeltaText = "",
    bool IsPositiveChange = false,
    bool IsNegativeChange = false)
{
    public bool HasChange => IsPositiveChange || IsNegativeChange;
    public bool IsStrengthTone => Tone == CalculatedStatTone.Strength;
    public bool IsDexterityTone => Tone == CalculatedStatTone.Dexterity;
    public bool IsIntelligenceTone => Tone == CalculatedStatTone.Intelligence;
    public bool IsLifeTone => Tone == CalculatedStatTone.Life;
    public bool IsManaTone => Tone == CalculatedStatTone.Mana;
    public bool IsEnergyShieldTone => Tone == CalculatedStatTone.EnergyShield;
    public bool IsArmourTone => Tone == CalculatedStatTone.Armour;
    public bool IsEvasionTone => Tone == CalculatedStatTone.Evasion;
    public bool IsWardTone => Tone == CalculatedStatTone.Ward;
    public bool IsAvoidanceTone => Tone == CalculatedStatTone.Avoidance;
    public bool IsFireTone => Tone == CalculatedStatTone.Fire;
    public bool IsColdTone => Tone == CalculatedStatTone.Cold;
    public bool IsLightningTone => Tone == CalculatedStatTone.Lightning;
    public bool IsChaosTone => Tone == CalculatedStatTone.Chaos;
}

public enum CalculatedStatTone
{
    Neutral,
    Strength,
    Dexterity,
    Intelligence,
    Life,
    Mana,
    EnergyShield,
    Armour,
    Evasion,
    Ward,
    Avoidance,
    Fire,
    Cold,
    Lightning,
    Chaos,
    Movement,
}

public sealed class CalculatedStatGroupViewModel
{
    public CalculatedStatGroupViewModel(
        string name,
        IEnumerable<CalculatedStatMetricViewModel> stats)
    {
        Name = name;
        Stats = new ObservableCollection<CalculatedStatMetricViewModel>(stats);
    }

    public string Name { get; }
    public ObservableCollection<CalculatedStatMetricViewModel> Stats { get; }
    public bool IsAttributesGroup => Name == "Attributes";
    public bool IsPoolsGroup => Name == "Pools";
    public bool IsRecoveryGroup => Name == "Recovery";
    public bool IsDefencesGroup => Name == "Defences";
    public bool IsAvoidanceGroup => Name == "Avoidance";
    public bool IsResistancesGroup => Name == "Resistances";
}

public sealed class PassiveStatPreviewViewModel
{
    private PassiveStatPreviewViewModel(
        string sidebarText,
        string tooltipHeading,
        string warningText,
        IEnumerable<CalculatedStatMetricViewModel> changes)
    {
        SidebarText = sidebarText;
        TooltipHeading = tooltipHeading;
        WarningText = warningText;
        Changes = new ObservableCollection<CalculatedStatMetricViewModel>(changes);
    }

    public string SidebarText { get; }
    public string TooltipHeading { get; }
    public string WarningText { get; }
    public ObservableCollection<CalculatedStatMetricViewModel> Changes { get; }
    public bool HasChanges => Changes.Count > 0;
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);

    public static PassiveStatPreviewViewModel From(
        PassiveAllocationPreview preview,
        IEnumerable<CalculatedStatMetricViewModel> changes)
    {
        var nodeLabel = preview.NodeIds.Count == 1 ? "1 passive" : $"{preview.NodeIds.Count} passives";
        var warning = preview.HasUnmodeledJewelEffectChange
            ? "Jewel-radius follow-on changes are not included in this preview."
            : string.Empty;
        return preview.Kind switch
        {
            PassiveAllocationPreviewKind.Allocate => new PassiveStatPreviewViewModel(
                $"Previewing allocation · {nodeLabel}",
                $"Allocating {nodeLabel} will give you:",
                warning,
                changes),
            PassiveAllocationPreviewKind.Deallocate => new PassiveStatPreviewViewModel(
                $"Previewing refund · {nodeLabel}",
                $"Refunding {nodeLabel} will give you:",
                warning,
                changes),
            _ => new PassiveStatPreviewViewModel(string.Empty, string.Empty, string.Empty, []),
        };
    }
}

public sealed class EquipmentSlotViewModel
{
    public EquipmentSlotViewModel(
        string name,
        string displayName,
        string shortName,
        string category,
        bool showCategoryHeader,
        ItemViewModel? item)
    {
        Name = name;
        DisplayName = displayName;
        ShortName = shortName;
        Category = category;
        ShowCategoryHeader = showCategoryHeader;
        Item = item;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public string ShortName { get; }
    public string Category { get; }
    public bool ShowCategoryHeader { get; }
    public ItemViewModel? Item { get; }
    public bool HasItem => Item is not null;
    public bool IsEmpty => Item is null;
}

public sealed class ItemGroupViewModel
{
    public string Header { get; }
    public ObservableCollection<ItemViewModel> Items { get; }

    public ItemGroupViewModel(string header, IEnumerable<ItemViewModel> items)
    {
        Header = header;
        Items = new ObservableCollection<ItemViewModel>(items);
    }
}

public sealed record ImportedSkillSetOptionViewModel(int Index, string DisplayName);

public sealed class ImportedBuildMetricsViewModel
{
    public string SourceText { get; }
    public string BackendText { get; }
    public string ErrorMessage { get; }
    public ObservableCollection<ImportedStatMetricViewModel> PlayerStats { get; }
    public ObservableCollection<ImportedSkillDpsMetricViewModel> SkillDps { get; }
    public ObservableCollection<string> Warnings { get; }
    public bool HasBackend => !string.IsNullOrWhiteSpace(BackendText);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasPlayerStats => PlayerStats.Count > 0;
    public bool HasSkillDps => SkillDps.Count > 0;

    private ImportedBuildMetricsViewModel(ImportedBuildMetrics metrics)
    {
        SourceText = metrics.Source switch
        {
            ImportedMetricSource.SavedXmlSnapshot => "DPS source: Saved snapshot",
            _ => "DPS source: Unavailable",
        };
        BackendText = string.Join(" ", new[] { metrics.BackendName, metrics.BackendVersion, metrics.BackendPath }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        ErrorMessage = metrics.ErrorMessage ?? string.Empty;
        PlayerStats = new ObservableCollection<ImportedStatMetricViewModel>(
            metrics.PlayerStats
                .Where(IsKeyStat)
                .Select(stat => new ImportedStatMetricViewModel(stat.Label, stat.DisplayValue)));
        SkillDps = new ObservableCollection<ImportedSkillDpsMetricViewModel>(
            metrics.SkillDps.Select(ImportedSkillDpsMetricViewModel.FromImported));
        Warnings = new ObservableCollection<string>(metrics.Warnings);
    }

    public static ImportedBuildMetricsViewModel FromImported(ImportedBuildMetrics metrics) => new(metrics);

    private static bool IsKeyStat(ImportedStatMetric stat)
    {
        var normalized = stat.Stat.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalized.Equals("FullDPS", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("TotalDPS", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("CombinedDPS", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Life", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("EnergyShield", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Armour", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Evasion", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("FireResist", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ColdResist", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("LightningResist", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ChaosResist", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ImportedStatMetricViewModel(string Label, string Value);

public sealed class ImportedSkillDpsMetricViewModel
{
    public string Name { get; }
    public string Count { get; }
    public string Dps { get; }
    public string Detail { get; }
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    private ImportedSkillDpsMetricViewModel(ImportedSkillDpsMetric metric)
    {
        Name = metric.Name;
        Count = metric.Count.ToString();
        Dps = metric.DisplayDps;
        Detail = string.Join(" / ", new[] { metric.SkillPart, metric.Source }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public static ImportedSkillDpsMetricViewModel FromImported(ImportedSkillDpsMetric metric) => new(metric);
}

public sealed class ImportedSkillGroupViewModel
{
    public string Header { get; }
    public string Metadata { get; }
    public string SkillSetName { get; }
    public string SocketLocation { get; }
    public string Source { get; }
    public string State { get; }
    public string FullDpsState { get; }
    public string GroupCount { get; }
    public string GemCount { get; }
    public string MainSkill { get; }
    public bool IsEnabled { get; }
    public bool IsDisabled => !IsEnabled;
    public double DisplayOpacity => IsEnabled ? 1 : 0.58;
    public bool IncludeInFullDps { get; }
    public bool IsMainSkillGroup { get; }
    public bool HasSource => !string.IsNullOrWhiteSpace(Source);
    public ObservableCollection<ImportedGemViewModel> Gems { get; }
    public bool HasMetadata => !string.IsNullOrWhiteSpace(Metadata);

    private ImportedSkillGroupViewModel(ImportedSkillSet set, ImportedSkillGroup group, bool isMainSkillGroup)
    {
        Header = group.Label;
        SkillSetName = set.DisplayName;
        SocketLocation = string.IsNullOrWhiteSpace(group.Slot) ? "Not assigned" : group.Slot;
        Source = group.Source ?? string.Empty;
        IsEnabled = group.Enabled;
        IncludeInFullDps = group.IncludeInFullDps;
        IsMainSkillGroup = isMainSkillGroup;
        State = group.Enabled ? "Enabled" : "Disabled";
        FullDpsState = group.IncludeInFullDps ? "Included" : "Not included";
        GroupCount = group.GroupCount.ToString();
        GemCount = group.Gems.Count == 1 ? "1 gem" : $"{group.Gems.Count} gems";
        MainSkill = group.MainActiveSkillIndex >= 0 && group.MainActiveSkillIndex < group.Gems.Count
            ? group.Gems[group.MainActiveSkillIndex].NameSpec
            : group.Gems.FirstOrDefault(gem => gem.Enabled)?.NameSpec ?? "None";
        var parts = new List<string> { set.DisplayName };
        if (!string.IsNullOrWhiteSpace(group.Slot))
        {
            parts.Add(group.Slot);
        }
        parts.Add(group.Enabled ? "enabled" : "disabled");
        if (group.IncludeInFullDps)
        {
            parts.Add("FullDPS");
        }
        if (group.GroupCount != 1)
        {
            parts.Add($"x{group.GroupCount}");
        }
        Metadata = string.Join(" · ", parts);
        Gems = new ObservableCollection<ImportedGemViewModel>(group.Gems.Select(ImportedGemViewModel.FromImported));
    }

    public static ImportedSkillGroupViewModel FromImported(
        ImportedSkillSet set,
        ImportedSkillGroup group,
        bool isMainSkillGroup = false) =>
        new(set, group, isMainSkillGroup);
}

public sealed class ImportedGemViewModel
{
    public string Name { get; }
    public string Metadata { get; }
    public string Level { get; }
    public string Quality { get; }
    public string Count { get; }
    public string State { get; }
    public bool IsDisabled { get; }
    public double DisplayOpacity => IsDisabled ? 0.58 : 1;
    public bool HasMetadata => !string.IsNullOrWhiteSpace(Metadata);

    private ImportedGemViewModel(ImportedGem gem)
    {
        Name = gem.NameSpec;
        IsDisabled = !gem.Enabled;
        Level = gem.Level?.ToString() ?? "—";
        Quality = gem.Quality is { } qualityValue ? $"{qualityValue}%" : "—";
        Count = gem.Count.ToString();
        State = gem.Enabled ? "Enabled" : "Disabled";
        var parts = new List<string>();
        if (gem.Level is { } level)
        {
            parts.Add($"lvl {level}");
        }
        if (gem.Quality is { } quality)
        {
            parts.Add($"{quality}%");
        }
        if (gem.Count != 1)
        {
            parts.Add($"x{gem.Count}");
        }
        if (!gem.Enabled)
        {
            parts.Add("disabled");
        }
        Metadata = string.Join(" · ", parts);
    }

    public static ImportedGemViewModel FromImported(ImportedGem gem) => new(gem);
}
