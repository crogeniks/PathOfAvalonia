namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    // Switch to a different starting class. Resets the allocation to just that class-start
    // node — paths valid under the old class don't carry over.
    public void SetClass(int classIndex)
    {
        if (_selectedClassIndex == classIndex)
        {
            return;
        }
        if (!_classStartNodeByIndex.ContainsKey(classIndex))
        {
            return;
        }
        _allocated.Clear();
        _masterySelections.Clear();
        _attributeOverrides.Clear();
        _allocationSets.Clear();
        _selectedClassIndex = classIndex;
        _selectedAscendancyIndex = 0;
        _allocated.Add(_classStartNodeByIndex[classIndex]);
        RebuildActiveRadiusEffects();
        SpecChanged?.Invoke();
    }

    public void SetAscendancy(int ascendancyIndex)
    {
        var names = Classes.AscendancyNames(_selectedClassIndex);
        if (ascendancyIndex < 0 || ascendancyIndex >= names.Count || _selectedAscendancyIndex == ascendancyIndex)
        {
            return;
        }

        RemoveSelectedAscendancyAllocations();
        _selectedAscendancyIndex = ascendancyIndex;
        if (SelectedAscendancyName is { } ascendancyName
            && _ascendancyStartNodeByName.TryGetValue(ascendancyName, out var startId))
        {
            _allocated.Add(startId);
        }
        RebuildActiveRadiusEffects();
        SpecChanged?.Invoke();
    }

    private string? SelectedAscendancyName => Classes.AscendancyTreeName(_selectedClassIndex, _selectedAscendancyIndex);

    private int? SelectedAscendancyStartNodeId()
    {
        if (SelectedAscendancyName is not { } ascendancyName)
        {
            return null;
        }
        return _ascendancyStartNodeByName.TryGetValue(ascendancyName, out var startId) ? startId : null;
    }

    private void RemoveSelectedAscendancyAllocations()
    {
        if (SelectedAscendancyName is not { } ascendancyName)
        {
            return;
        }
        foreach (var id in _allocated.ToArray())
        {
            if (Tree.Nodes.TryGetValue(id, out var node) && node.AscendancyName == ascendancyName)
            {
                _allocated.Remove(id);
                _masterySelections.Remove(id);
                _allocationSets.Remove(id);
            }
        }
    }
}
