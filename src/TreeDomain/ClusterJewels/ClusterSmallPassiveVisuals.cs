namespace PathOfAvalonia.TreeDomain.ClusterJewels;

// Cluster-jewel small passives are generated nodes, so unlike notables they do
// not have a node entry in the tree data to supply their icon.  Match the
// jewel's enchantment to the same icon family used by ordinary passive nodes.
public static class ClusterSmallPassiveVisuals
{
    private const string IconRoot = "Art/2DArt/SkillIcons/passives/";

    public static string? IconFor(IReadOnlyList<string> stats)
    {
        var text = string.Join(' ', stats).ToLowerInvariant();
        if (text.Length == 0 || text.Contains("nothing")) return null;
        if (text.Contains("minion")) return IconRoot + "miniondamage.png";
        if (text.Contains("chaos")) return IconRoot + "ChaosDamage.png";
        if (text.Contains("fire")) return IconRoot + "FireDamagenode.png";
        if (text.Contains("cold")) return IconRoot + "ColdDamagenode.png";
        if (text.Contains("lightning")) return IconRoot + "LightningDamagenode.png";
        if (text.Contains("physical")) return IconRoot + "PhysicalDamageNode.png";
        if (text.Contains("damage over time")) return IconRoot + "DamageOverTimeNode.png";
        if (text.Contains("energy shield")) return IconRoot + "EnergyShieldNode.png";
        if (text.Contains("evasion")) return IconRoot + "EvasionNode.png";
        if (text.Contains("armour")) return IconRoot + "ArmourNode.png";
        if (text.Contains("projectile")) return IconRoot + "ProjectileDmgNode.png";
        if (text.Contains("totem")) return IconRoot + "TotemDmgNode.png";
        if (text.Contains("trap") || text.Contains("mine")) return IconRoot + "TrapAndMineDmgNode.png";
        if (text.Contains("area")) return IconRoot + "AreaDmgNode.png";
        if (text.Contains("life")) return IconRoot + "LifeandMana.png";
        return IconRoot + "damage.png";
    }
}
