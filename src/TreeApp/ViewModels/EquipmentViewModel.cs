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
    private ObservableCollection<ItemViewModel> _items = new();

    [ObservableProperty]
    private string _emptyMessage = "Import a build to see equipment.";

    public bool HasItems => Items.Count > 0;

    public void LoadBuild(ImportedBuild build)
    {
        if (build.Items.Count == 0)
        {
            EmptyMessage = "No items in this build.";
            Items = new ObservableCollection<ItemViewModel>();
            return;
        }
        EmptyMessage = string.Empty;
        Items = new ObservableCollection<ItemViewModel>(
            build.Items.Select(ItemViewModel.FromImported));
    }

    public void Clear()
    {
        Items = new ObservableCollection<ItemViewModel>();
        EmptyMessage = "Import a build to see equipment.";
    }
}
