using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.ClusterJewels;
using SkiaSharp;

namespace PathOfAvalonia.TreeApp.Controls;

public sealed partial class PassiveTreeView
{
    private long _connectorStateRevision = -1;
    private IReadOnlySet<int> _connectorAllocatedSnapshot = new HashSet<int>();
    private IReadOnlyDictionary<int, PassiveAllocationSet> _connectorAllocationSets =
        new Dictionary<int, PassiveAllocationSet>();
    private IReadOnlyList<Connector> _clusterConnectorSnapshot = [];
    private IReadOnlySet<(int Min, int Max)> _connectorHoverPathEdges =
        new HashSet<(int Min, int Max)>();

    private void DrawConnectors(
        DrawingContext context,
        IReadOnlyDictionary<int, ClusterSubgraph> activeClusters,
        Rect visibleTree,
        IReadOnlySet<int> allocated)
    {
        if (_connectorStateRevision != _vm.VisualRevision)
        {
            _connectorStateRevision = _vm.VisualRevision;
            _connectorAllocatedSnapshot = allocated.ToHashSet();
            _connectorAllocationSets = _connectorAllocatedSnapshot.ToDictionary(id => id, _vm.AllocationSetOf);
            _clusterConnectorSnapshot = activeClusters.Values
                .SelectMany(cluster => cluster.Connectors)
                .ToArray();
            _connectorHoverPathEdges = _vm.HoverPath.Edges;
        }
        context.Custom(new ConnectorDrawOperation(
            new Rect(Bounds.Size),
            _drawableBaseConnectors,
            _clusterConnectorSnapshot,
            visibleTree,
            _connectorAllocatedSnapshot,
            _connectorAllocationSets,
            _connectorHoverPathEdges,
            _scale,
            _offsetX,
            _offsetY));
    }

    private sealed class ConnectorDrawOperation(
        Rect bounds,
        IReadOnlyList<Connector> baseConnectors,
        IReadOnlyList<Connector> clusterConnectors,
        Rect visibleTree,
        IReadOnlySet<int> allocated,
        IReadOnlyDictionary<int, PassiveAllocationSet> allocationSets,
        IReadOnlySet<(int Min, int Max)> hoverPathEdges,
        double scale,
        double offsetX,
        double offsetY) : ICustomDrawOperation
    {
        private const float RadiansToDegrees = 180f / MathF.PI;

        public Rect Bounds => bounds;

        public bool HitTest(Point point) => bounds.Contains(point);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var saved = canvas.Save();
            canvas.Translate((float)offsetX, (float)offsetY);
            canvas.Scale((float)scale);

            var strokeWidth = (float)Math.Max(0.5 / scale, ConnectorThicknessTree);
            using var normal = Paint(ConnectorColor, strokeWidth);
            using var required = Paint(RequiredPathConnectorColor, strokeWidth);
            using var active = Paint(AllocatedColor, strokeWidth);
            using var weaponSet1 = Paint(WeaponSet1Color, strokeWidth);
            using var weaponSet2 = Paint(WeaponSet2Color, strokeWidth);
            using var hover = Paint(HoverPathColor, strokeWidth);

            foreach (var connector in baseConnectors)
            {
                Draw(connector);
            }
            foreach (var connector in clusterConnectors)
            {
                Draw(connector);
            }

            canvas.RestoreToCount(saved);
            return;

            void Draw(Connector connector)
            {
                if (!ConnectorIntersects(connector, visibleTree))
                {
                    return;
                }

                var edge = (Math.Min(connector.FromId, connector.ToId), Math.Max(connector.FromId, connector.ToId));
                var paint = allocated.Contains(connector.FromId) && allocated.Contains(connector.ToId)
                    ? AllocationPaint(connector.FromId, connector.ToId)
                    : hoverPathEdges.Contains(edge)
                        ? hover
                        : connector.RequiredAllocatedNodeId is null ? normal : required;

                switch (connector)
                {
                    case LineConnector line:
                        canvas.DrawLine(
                            (float)line.X1,
                            (float)line.Y1,
                            (float)line.X2,
                            (float)line.Y2,
                            paint);
                        break;
                    case ArcConnector arc:
                        var radius = (float)arc.Radius;
                        var oval = new SKRect(
                            (float)arc.Cx - radius,
                            (float)arc.Cy - radius,
                            (float)arc.Cx + radius,
                            (float)arc.Cy + radius);
                        canvas.DrawArc(
                            oval,
                            (float)arc.StartAngle * RadiansToDegrees - 90f,
                            (float)arc.SweepAngle * RadiansToDegrees,
                            useCenter: false,
                            paint);
                        break;
                }
            }

            SKPaint AllocationPaint(int fromId, int toId)
            {
                var fromSet = allocationSets.GetValueOrDefault(fromId, PassiveAllocationSet.Normal);
                var toSet = allocationSets.GetValueOrDefault(toId, PassiveAllocationSet.Normal);
                if (fromSet == toSet)
                {
                    return fromSet switch
                    {
                        PassiveAllocationSet.WeaponSet1 => weaponSet1,
                        PassiveAllocationSet.WeaponSet2 => weaponSet2,
                        _ => active,
                    };
                }
                if (fromSet == PassiveAllocationSet.Normal)
                {
                    return toSet == PassiveAllocationSet.WeaponSet1 ? weaponSet1 : weaponSet2;
                }
                if (toSet == PassiveAllocationSet.Normal)
                {
                    return fromSet == PassiveAllocationSet.WeaponSet1 ? weaponSet1 : weaponSet2;
                }
                return active;
            }
        }

        public void Dispose()
        {
        }

        private static SKPaint Paint(Color color, float strokeWidth)
        {
            return new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                IsAntialias = true,
                Color = new SKColor(color.R, color.G, color.B, color.A),
            };
        }
    }
}
