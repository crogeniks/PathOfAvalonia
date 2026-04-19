using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var root = this.FindControl<Grid>("Root")!;

        var uri = new Uri("avares://PathOfAvalonia.TreeApp/Assets/tree_3_28.json");
        using var stream = AssetLoader.Open(uri);
        var tree = TreeLoader.LoadFromJson(stream, "3.28");
        var spec = new PassiveSpec(tree);

        root.Children.Add(new PassiveTreeView(tree, spec));
    }
}
