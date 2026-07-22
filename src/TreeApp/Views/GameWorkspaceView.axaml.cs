using System;
using System.Linq;
using Avalonia.Controls;
using PathOfAvalonia.TreeApp.Controls;
using PathOfAvalonia.TreeApp.ViewModels;

namespace PathOfAvalonia.TreeApp.Views;

public partial class GameWorkspaceView : UserControl
{
    private AtlasTreeViewModel? _atlasViewModel;

    public GameWorkspaceView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not GameWorkspaceViewModel vm)
        {
            return;
        }

        var root = this.FindControl<Grid>("TreeCanvas")!;
        if (!root.Children.OfType<PassiveTreeView>().Any())
        {
            root.Children.Insert(0, new PassiveTreeView(vm.State.Tree, vm.State.Sprites, vm.ImageResolver));
        }

        if (!ReferenceEquals(_atlasViewModel, vm.Atlas))
        {
            if (_atlasViewModel is not null)
            {
                _atlasViewModel.CanvasChanged -= OnAtlasCanvasChanged;
            }
            _atlasViewModel = vm.Atlas;
            if (_atlasViewModel is not null)
            {
                _atlasViewModel.CanvasChanged += OnAtlasCanvasChanged;
            }
        }
        AttachAtlasCanvas();

        var inputBox = this.FindControl<TextBox>("ImportInput");
        if (inputBox is not null)
        {
            inputBox.TextChanged += (_, _) =>
            {
                var placeholder = vm.ImportExport.TryReplaceBuildCode(inputBox.Text ?? string.Empty);
                if (placeholder != null)
                {
                    inputBox.Text = placeholder;
                    inputBox.CaretIndex = placeholder.Length;
                }
            };
        }
    }

    private void OnAtlasCanvasChanged() => AttachAtlasCanvas();

    private void AttachAtlasCanvas()
    {
        var root = this.FindControl<Grid>("AtlasCanvas");
        if (root is null || _atlasViewModel is null)
        {
            return;
        }

        foreach (var existing in root.Children.OfType<AtlasTreeView>().ToArray())
        {
            root.Children.Remove(existing);
        }
        root.Children.Insert(0, new AtlasTreeView(
            _atlasViewModel,
            _atlasViewModel.Sprites,
            _atlasViewModel.ImageResolver));
    }
}
