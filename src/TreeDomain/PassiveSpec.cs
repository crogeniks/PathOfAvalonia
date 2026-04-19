namespace PathOfAvalonia.TreeDomain;

public sealed class PassiveSpec
{
    public TreeModel Tree { get; }
    private readonly HashSet<int> _allocated = new();

    public PassiveSpec(TreeModel tree) => Tree = tree;

    public IReadOnlySet<int> AllocatedNodes => _allocated;
    public bool IsAllocated(int id) => _allocated.Contains(id);

    // PoC-level toggle. Full reachability / dependency rules (selection.md §2–4)
    // are deferred until the domain gets its own tests.
    public void Toggle(int id)
    {
        if (!_allocated.Add(id))
        {
            _allocated.Remove(id);
        }
        SpecChanged?.Invoke();
    }

    public event Action? SpecChanged;
}
