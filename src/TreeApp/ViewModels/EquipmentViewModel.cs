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
        if (build.Items.Count > 0)
        {
            groups.Add(new ItemGroupViewModel(
                "Equipment",
                build.Items.Select(item => ItemViewModel.FromImported(item))));
        }

        var jewelItems = SocketedJewels(build).ToArray();
        if (jewelItems.Length > 0)
        {
            groups.Add(new ItemGroupViewModel("Jewels", jewelItems));
        }

        if (groups.Count == 0)
        {
            EmptyMessage = "No items in this build.";
            Groups = new ObservableCollection<ItemGroupViewModel>();
            return;
        }
        EmptyMessage = string.Empty;
        Groups = new ObservableCollection<ItemGroupViewModel>(groups);
    }

    public void Clear()
    {
        Groups = new ObservableCollection<ItemGroupViewModel>();
        EmptyMessage = "Import a build to see equipment.";
    }

    private static IEnumerable<ItemViewModel> SocketedJewels(ImportedBuild build)
    {
        var seen = new HashSet<int>();
        foreach (var socketed in build.SocketedJewels.OrderBy(jewel => jewel.SocketNodeId))
        {
            if (!seen.Add(socketed.ItemId) || !build.ItemsById.TryGetValue(socketed.ItemId, out var item))
            {
                continue;
            }

            yield return ItemViewModel.FromImported(item, $"Jewel {socketed.SocketNodeId}");
        }
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
