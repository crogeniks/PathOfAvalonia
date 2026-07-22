using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    /// <summary>
    /// Equips, replaces, or removes an item in a passive-tree jewel socket without
    /// reapplying the rest of the build import. Cluster graphs and radius effects
    /// are kept in sync with the new item.
    /// </summary>
    public bool SetSocketedJewel(int socketNodeId, ImportedItem? item)
    {
        if (!TryGetNode(socketNodeId, out var socket) || socket?.Type != NodeType.JewelSocket)
        {
            return false;
        }
        if (item is not null
            && _socketedJewels.TryGetValue(socketNodeId, out var existing)
            && (ReferenceEquals(existing, item) || existing == item))
        {
            return false;
        }

        if (item is null)
        {
            var changed = RemoveClusterRecursive(socketNodeId);
            changed |= _socketedJewels.Remove(socketNodeId);
            if (changed)
            {
                RebuildActiveRadiusEffects();
                PruneInvalidRadiusOnlyAllocations();
                SpecChanged?.Invoke();
            }
            return changed;
        }

        if (ImportedClusterJewelParser.TryParse(item, out var cluster))
        {
            if (!CanInsertCluster(socketNodeId, cluster.Size))
            {
                return false;
            }

            if (!SetClusterJewelCore(
                socketNodeId,
                new ClusterJewelSpec(
                    socketNodeId,
                    cluster.Size,
                    cluster.PassiveCount,
                    cluster.SocketCount,
                    cluster.NotableNames,
                    cluster.KeystoneName,
                    cluster.SmallPassiveStats)))
            {
                return false;
            }
        }
        else
        {
            RemoveClusterRecursive(socketNodeId);
        }

        _socketedJewels[socketNodeId] = item;
        RebuildActiveRadiusEffects();
        PruneInvalidRadiusOnlyAllocations();
        SpecChanged?.Invoke();
        return true;
    }
}
