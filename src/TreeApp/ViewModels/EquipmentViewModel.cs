using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.ViewModels;

public partial class EquipmentViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private ObservableCollection<ItemGroupViewModel> _groups = new();

    [ObservableProperty]
    private string _emptyMessage = "Import a build to see equipment.";

    public bool HasItems => Groups.Any(group => group.Items.Count > 0);

    public void LoadBuild(ImportedBuild build)
    {
        var groups = new List<ItemGroupViewModel>();
        var activeItems = build.Items.ToArray();

        var equipment = activeItems.Where(item => GroupName(item) == "Equipment").ToArray();
        if (equipment.Length > 0)
        {
            groups.Add(new ItemGroupViewModel(
                "Equipment",
                equipment.Select(item => ItemViewModel.FromImported(item))));
        }

        var flaskCharmItems = activeItems.Where(item => GroupName(item) == "Flasks & Charms").ToArray();
        if (flaskCharmItems.Length > 0)
        {
            groups.Add(new ItemGroupViewModel(
                "Flasks & Charms",
                flaskCharmItems.Select(item => ItemViewModel.FromImported(item))));
        }

        var jewelItems = activeItems
            .Where(item => GroupName(item) == "Jewels")
            .Select(item => ItemViewModel.FromImported(item))
            .ToArray();
        if (jewelItems.Length > 0)
        {
            groups.Add(new ItemGroupViewModel("Jewels", jewelItems));
        }

        var socketedJewels = SocketedJewels(build, activeItems.Select(item => item.Id).ToHashSet()).ToArray();
        if (socketedJewels.Length > 0)
        {
            groups.Add(new ItemGroupViewModel("Socketed Tree Jewels", socketedJewels));
        }

        if (groups.Count == 0)
        {
            EmptyMessage = "Import a build to view equipment.";
            Groups = new ObservableCollection<ItemGroupViewModel>();
            return;
        }
        EmptyMessage = string.Empty;
        Groups = new ObservableCollection<ItemGroupViewModel>(groups);
    }

    public void Clear()
    {
        Groups = new ObservableCollection<ItemGroupViewModel>();
        EmptyMessage = "Import a build to view equipment.";
    }

    public void MarkUnsupported()
    {
        Groups = new ObservableCollection<ItemGroupViewModel>();
        EmptyMessage = "Equipment is not available for this game yet.";
    }

    private static IEnumerable<ItemViewModel> SocketedJewels(ImportedBuild build, HashSet<int> activeItemIds)
    {
        var seen = new HashSet<int>();
        foreach (var socketed in build.SocketedJewels.OrderBy(jewel => jewel.SocketNodeId))
        {
            if (activeItemIds.Contains(socketed.ItemId)
                || !seen.Add(socketed.ItemId)
                || !build.ItemsById.TryGetValue(socketed.ItemId, out var item))
            {
                continue;
            }

            yield return ItemViewModel.FromImported(item, $"Jewel {socketed.SocketNodeId}");
        }
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
