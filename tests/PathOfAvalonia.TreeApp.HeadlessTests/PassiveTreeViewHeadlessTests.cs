using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PathOfAvalonia.TreeApp.Controls;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.HeadlessTests;

public sealed class PassiveTreeViewHeadlessTests
{
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
}
