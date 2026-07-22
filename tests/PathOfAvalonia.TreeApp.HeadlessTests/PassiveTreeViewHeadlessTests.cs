using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PathOfAvalonia.TreeApp.Controls;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.HeadlessTests;

public sealed class PassiveTreeViewHeadlessTests
{
    [AvaloniaFact]
    public void Poe1DiffRingRemainsVisibleAboveAllocatedFrameWhenZoomedIn()
    {
        var tree = CoreUserJourneyHeadlessTests.CreateTree(GameId.PathOfExile1, "3.29.0");
        var baseline = WithoutNode(tree, 2);
        var spec = new PassiveSpec(tree);
        spec.Toggle(2);
        var viewModel = new PassiveTreeViewModel(spec);
        viewModel.SetDiff(TreeDiff.Compare(tree, baseline));

        using var data = File.OpenRead(Poe1Asset("3_29_0", "data.json"));
        var sprites = SpriteMap.LoadPoe1FromGggTree(data, "3_29_0/assets");
        var view = new PassiveTreeView(viewModel, sprites, new Poe1FileImageAssetResolver());
        var window = new Window
        {
            Width = 1000,
            Height = 600,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        _ = window.CaptureRenderedFrame();

        try
        {
            var target = TreeToView(view, tree.Nodes[2], tree.Bounds);
            var windowTarget = view.TranslatePoint(target, window);
            Assert.NotNull(windowTarget);

            window.MouseWheel(windowTarget.Value, new Vector(0, 3));
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);

            Assert.True(
                CountGreenPixels(frame, windowTarget.Value, minimumRadius: 54, maximumRadius: 74) > 50,
                "The added-node diff ring should remain visible outside the allocated frame at high zoom.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClickAllocatesButDragOnlyNavigatesTheTree()
    {
        var tree = CoreUserJourneyHeadlessTests.CreateTree(GameId.PathOfExile1, "test");
        var spec = new PassiveSpec(tree);
        var view = new PassiveTreeView(
            new PassiveTreeViewModel(spec),
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            new NullImageAssetResolver());
        var window = new Window
        {
            Width = 1000,
            Height = 600,
            Content = view,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        _ = window.CaptureRenderedFrame();

        try
        {
            var target = TreeToView(view, tree.Nodes[2], tree.Bounds);

            Click(window, view, target);
            Assert.Contains(2, spec.AllocatedNodes);

            Click(window, view, target);
            Assert.DoesNotContain(2, spec.AllocatedNodes);

            Drag(window, view, target, target + new Vector(60, 35));
            Assert.DoesNotContain(2, spec.AllocatedNodes);
        }
        finally
        {
            window.Close();
        }
    }

    private static Point TreeToView(Control view, Node node, TreeBounds bounds)
    {
        var scale = Math.Min(view.Bounds.Width / bounds.Width, view.Bounds.Height / bounds.Height) * 0.95;
        var offsetX = view.Bounds.Width * 0.5 - bounds.CenterX * scale;
        var offsetY = view.Bounds.Height * 0.5 - bounds.CenterY * scale;
        return new Point(node.X * scale + offsetX, node.Y * scale + offsetY);
    }

    private static TreeModel WithoutNode(TreeModel tree, int removedNodeId) => new()
    {
        GameId = tree.GameId,
        Version = "baseline",
        Classes = tree.Classes,
        Nodes = tree.Nodes.Where(pair => pair.Key != removedNodeId).ToDictionary(),
        ClusterNodeTemplates = tree.ClusterNodeTemplates,
        Connectors = tree.Connectors
            .Where(connector => connector.FromId != removedNodeId && connector.ToId != removedNodeId)
            .ToArray(),
        Bounds = tree.Bounds,
        Groups = tree.Groups,
        SkillsPerOrbit = tree.SkillsPerOrbit,
        OrbitRadii = tree.OrbitRadii,
        OrbitAngles = tree.OrbitAngles,
    };

    private static int CountGreenPixels(Bitmap bitmap, Point centre, double minimumRadius, double maximumRadius)
    {
        using var copy = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var framebuffer = copy.Lock();
        bitmap.CopyPixels(framebuffer);

        var pixels = new byte[framebuffer.RowBytes * framebuffer.Size.Height];
        Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);
        var minimumRadiusSquared = minimumRadius * minimumRadius;
        var maximumRadiusSquared = maximumRadius * maximumRadius;
        var count = 0;
        for (var y = Math.Max(0, (int)(centre.Y - maximumRadius));
             y < Math.Min(framebuffer.Size.Height, (int)Math.Ceiling(centre.Y + maximumRadius));
             y++)
        {
            for (var x = Math.Max(0, (int)(centre.X - maximumRadius));
                 x < Math.Min(framebuffer.Size.Width, (int)Math.Ceiling(centre.X + maximumRadius));
                 x++)
            {
                var dx = x - centre.X;
                var dy = y - centre.Y;
                var radiusSquared = dx * dx + dy * dy;
                if (radiusSquared < minimumRadiusSquared || radiusSquared > maximumRadiusSquared)
                {
                    continue;
                }

                var pixel = y * framebuffer.RowBytes + x * 4;
                var blue = pixels[pixel];
                var green = pixels[pixel + 1];
                var red = pixels[pixel + 2];
                if (green > 120 && green > red + 35 && green > blue + 35)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static string Poe1Asset(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", .. parts]));

    private static void Click(Window window, Control control, Point controlPoint)
    {
        var windowPoint = control.TranslatePoint(controlPoint, window);
        Assert.NotNull(windowPoint);
        window.MouseMove(windowPoint.Value);
        window.MouseDown(windowPoint.Value, MouseButton.Left);
        window.MouseUp(windowPoint.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Drag(Window window, Control control, Point start, Point end)
    {
        var windowStart = control.TranslatePoint(start, window);
        var windowEnd = control.TranslatePoint(end, window);
        Assert.NotNull(windowStart);
        Assert.NotNull(windowEnd);
        window.MouseMove(windowStart.Value);
        window.MouseDown(windowStart.Value, MouseButton.Left);
        window.MouseMove(windowEnd.Value);
        window.MouseUp(windowEnd.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class NullImageAssetResolver : ITreeImageAssetResolver
    {
        public Bitmap? LoadBitmap(string relativePath) => null;
        public Bitmap? LoadBackground(string treeVersion) => null;
    }

    private sealed class Poe1FileImageAssetResolver : ITreeImageAssetResolver
    {
        public Bitmap? LoadBitmap(string relativePath)
        {
            var path = Poe1Asset(relativePath.Split('/'));
            return File.Exists(path) ? new Bitmap(path) : null;
        }

        public Bitmap? LoadBackground(string treeVersion) => null;
    }
}
