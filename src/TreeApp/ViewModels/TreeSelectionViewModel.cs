using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PathOfAvalonia.TreeApp.ViewModels;

/// <summary>Synchronizes class and ascendancy selections with the passive spec.</summary>
public sealed partial class TreeSelectionViewModel : ObservableObject
{
    private readonly BuildWorkspaceState _state;
    private bool _syncingClass;
    private bool _syncingAscendancy;

    public TreeSelectionViewModel(BuildWorkspaceState state)
    {
        _state = state;
        SelectedClassIndex = state.Spec.SelectedClassIndex;
        SelectedAscendancyIndex = state.Spec.SelectedAscendancyIndex;
        SelectedAscendancyName = AscendancyNameAt(SelectedClassIndex, SelectedAscendancyIndex);
        state.Spec.SpecChanged += OnSpecChanged;
    }

    public IReadOnlyList<string> ClassNames => _state.Spec.Classes.ClassNames;
    public IReadOnlyList<string> AscendancyNames => _state.Spec.Classes.AscendancyNames(SelectedClassIndex);
    public bool IsTreeControlsExpanded => !IsTreeControlsCollapsed;
    public string TreeControlsToggleText => IsTreeControlsCollapsed ? "Show controls" : "Hide controls";

    [ObservableProperty] public partial int SelectedClassIndex { get; set; }
    [ObservableProperty] public partial int SelectedAscendancyIndex { get; set; }
    [ObservableProperty] public partial string SelectedAscendancyName { get; set; } = "None";
    [ObservableProperty] public partial bool IsTreeControlsCollapsed { get; set; }

    private void OnSpecChanged()
    {
        _syncingClass = true;
        _syncingAscendancy = true;
        SelectedClassIndex = _state.Spec.SelectedClassIndex;
        OnPropertyChanged(nameof(AscendancyNames));
        SelectedAscendancyIndex = _state.Spec.SelectedAscendancyIndex;
        SelectedAscendancyName = AscendancyNameAt(SelectedClassIndex, SelectedAscendancyIndex);
        _syncingClass = false;
        _syncingAscendancy = false;
    }

    partial void OnSelectedClassIndexChanged(int value)
    {
        OnPropertyChanged(nameof(AscendancyNames));
        if (!_syncingClass && value >= 0) _state.Spec.SetClass(value);
    }

    partial void OnSelectedAscendancyIndexChanged(int value)
    {
        if (!_syncingAscendancy && value >= 0) _state.Spec.SetAscendancy(value);
    }

    partial void OnSelectedAscendancyNameChanged(string value)
    {
        if (_syncingAscendancy) return;
        var index = AscendancyIndexOf(AscendancyNames, value);
        if (index >= 0) _state.Spec.SetAscendancy(index);
    }

    partial void OnIsTreeControlsCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTreeControlsExpanded));
        OnPropertyChanged(nameof(TreeControlsToggleText));
    }

    [RelayCommand]
    private void ToggleTreeControls() => IsTreeControlsCollapsed = !IsTreeControlsCollapsed;

    private string AscendancyNameAt(int classIndex, int ascendancyIndex)
    {
        var names = _state.Spec.Classes.AscendancyNames(classIndex);
        return ascendancyIndex >= 0 && ascendancyIndex < names.Count ? names[ascendancyIndex] : names[0];
    }

    private static int AscendancyIndexOf(IReadOnlyList<string> names, string value)
    {
        for (var i = 0; i < names.Count; i++) if (names[i] == value) return i;
        return -1;
    }
}
