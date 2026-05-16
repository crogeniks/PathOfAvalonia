namespace PathOfAvalonia.TreeDomain.ClusterJewels;

public static class ClusterJewelResolver
{
    private readonly record struct Layout(double Radius, HashSet<int> NotableSlots);

    // Orbit radii per passive count, matching PoB's cluster jewel orbit assignments:
    //   Small  (2–4):  6-slot orbit  → radius  82
    //   Medium (5–8):  16-slot orbit → radius 335
    //   Large  (9–12): 40-slot orbit → radius 493
    // Notable slot indices are approximate (PoB's exact positions come from
    // ClusterJewels.lua; uniform distribution is visually close enough for this MVP).
    private static readonly IReadOnlyDictionary<int, Layout> Layouts =
        new Dictionary<int, Layout>
        {
            [2]  = new(82,  new HashSet<int> { 0 }),
            [3]  = new(82,  new HashSet<int> { 0, 2 }),
            [4]  = new(82,  new HashSet<int> { 0, 2 }),
            [5]  = new(335, new HashSet<int> { 0, 3 }),
            [6]  = new(335, new HashSet<int> { 0, 3 }),
            [7]  = new(335, new HashSet<int> { 0, 3 }),
            [8]  = new(335, new HashSet<int> { 0, 3, 6 }),
            [9]  = new(493, new HashSet<int> { 0, 3, 6 }),
            [10] = new(493, new HashSet<int> { 0, 3, 7 }),
            [11] = new(493, new HashSet<int> { 0, 4, 8 }),
            [12] = new(493, new HashSet<int> { 0, 4, 8 }),
        };

    // Node IDs for cluster slots: 65536 + socketId * 16 + slotIndex.
    // This is collision-free for any two distinct socket IDs (since 1 * 16 > 11 = max slot index).
    public static int ClusterNodeId(int socketNodeId, int slotIndex) =>
        65536 + socketNodeId * 16 + slotIndex;

    // Generates the subgraph for a cluster jewel inserted into socketNode.
    // The ring is NOT centered on the socket. Instead it is offset outward (away from
    // the tree center), so that slot 0 lands exactly at the socket's position and the
    // ring expands outward from there.
    //
    // Ring links between cluster nodes are wired here. The socket↔slot-0 bidirectional
    // link is left to the caller (PassiveSpec.SetClusterJewel) to avoid side-effects on
    // the tree graph inside this pure generator.
    public static ClusterSubgraph Resolve(
        Node socketNode, ClusterJewelSpec spec,
        double treeCenterX, double treeCenterY)
    {
        var count = Math.Clamp(spec.PassiveCount, 2, 12);
        if (!Layouts.TryGetValue(count, out var layout))
        {
            layout = Layouts[12];
        }

        var radius = layout.Radius;

        // Outward direction: from tree center through the socket.
        var dx = socketNode.X - treeCenterX;
        var dy = socketNode.Y - treeCenterY;
        var len = Math.Sqrt(dx * dx + dy * dy);
        // Fallback direction (straight up) for the unlikely case of a socket at the origin.
        var outDirX = len > 0.001 ? dx / len : 0.0;
        var outDirY = len > 0.001 ? dy / len : -1.0;

        // Ring center is one full radius outward from the socket.
        var cx = socketNode.X + outDirX * radius;
        var cy = socketNode.Y + outDirY * radius;

        // Entry angle: points from ring center back toward the socket, so slot 0 lands
        // exactly at the socket's position (verified: cx + sin(θ₀)*R = socket.X, etc.)
        //   sin(θ₀) = -outDirX,  cos(θ₀) = outDirY
        var entryAngle = Math.Atan2(-outDirX, outDirY);

        var sweepPerNode = Math.Tau / count;
        var nodes = new Node[count];
        var notableIdx = 0;

        for (var i = 0; i < count; i++)
        {
            var angle = entryAngle + i * sweepPerNode;
            NodeType type;
            string name;
            if (layout.NotableSlots.Contains(i))
            {
                type = NodeType.Notable;
                name = notableIdx < spec.NotableNames.Count
                    ? spec.NotableNames[notableIdx++]
                    : "Cluster Notable";
            }
            else
            {
                type = NodeType.Normal;
                name = "Cluster Passive";
            }

            nodes[i] = new Node
            {
                Id   = ClusterNodeId(socketNode.Id, i),
                Name = name,
                Type = type,
                X    = cx + Math.Sin(angle) * radius,
                Y    = cy - Math.Cos(angle) * radius,
            };
        }

        // Wire bidirectional ring links between adjacent cluster nodes.
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            nodes[i].LinkedNodes.Add(nodes[next]);
            nodes[next].LinkedNodes.Add(nodes[i]);
        }

        // Arc connectors around the ring (one per adjacent pair, including wrap-around).
        // Slot 0 is at the socket position — no separate socket→slot0 line is needed.
        var connectors = new List<Connector>(count);
        for (var i = 0; i < count; i++)
        {
            connectors.Add(new ArcConnector(
                nodes[i].Id, nodes[(i + 1) % count].Id,
                cx, cy, radius,
                entryAngle + i * sweepPerNode, sweepPerNode));
        }

        return new ClusterSubgraph
        {
            SocketNodeId  = socketNode.Id,
            Size          = spec.Size,
            CircleRadius  = radius,
            ClusterCenterX = cx,
            ClusterCenterY = cy,
            Nodes         = nodes,
            Connectors    = connectors,
        };
    }
}
