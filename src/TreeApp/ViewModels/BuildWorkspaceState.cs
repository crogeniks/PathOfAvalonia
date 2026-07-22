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

        Tree.UseCoordinatedSpecChanges();
        Equipment.UseCoordinatedSpecChanges();
        Tree.HoverPreviewChanged += Equipment.SetPassivePreview;
        Equipment.PassivePreviewChanged += Tree.SetBasicStatPreview;
        Spec.SpecChanged += OnSpecChanged;
    }

    public GameDefinition Game { get; }
    public PassiveSpec Spec { get; }
    public SpriteMap Sprites { get; }
    public PassiveTreeViewModel Tree { get; }
    public EquipmentViewModel Equipment { get; }

    private void OnSpecChanged()
    {
        var preview = Tree.RefreshForSpecChange(publishHoverPreview: false);
        Equipment.RefreshForSpecChange(preview);
        // Preserve the public hover-preview notification after the coordinated
        // calculation. Equipment already owns this preview, so its handler exits
        // without a second stat calculation.
        Tree.PublishHoverPreview();
    }
}
