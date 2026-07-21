using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.ViewModels;

/// <summary>
/// Canonical mutable state for one open build workspace. All workspace-facing
/// view models receive this instance instead of retaining parallel references
/// to the tree, class catalog, spec, and presentation models.
/// </summary>
public sealed class BuildWorkspaceState
{
    public BuildWorkspaceState(
        GameDefinition game,
        PassiveSpec spec,
        SpriteMap sprites,
        PassiveTreeViewModel tree,
        EquipmentViewModel equipment)
    {
        Game = game;
        Spec = spec;
        Sprites = sprites;
        Tree = tree;
        Equipment = equipment;

        Tree.HoverPreviewChanged += Equipment.SetPassivePreview;
        Equipment.PassivePreviewChanged += Tree.SetBasicStatPreview;
    }

    public GameDefinition Game { get; }
    public PassiveSpec Spec { get; }
    public SpriteMap Sprites { get; }
    public PassiveTreeViewModel Tree { get; }
    public EquipmentViewModel Equipment { get; }
}
