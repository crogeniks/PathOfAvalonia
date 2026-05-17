using System;
using Avalonia.Controls;
using PathOfAvalonia.TreeApp.ViewModels;

namespace PathOfAvalonia.TreeApp.Views;

public partial class GameWorkspaceView : UserControl
{
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

        var root = this.FindControl<Grid>("TreeRoot")!;
        if (root.Children.Count == 1)
        {
            root.Children.Insert(0, new PassiveTreeView(vm.TreePanel.TreeViewModel, vm.Workspace.Sprites, vm.ImageResolver));
        }

        var inputBox = this.FindControl<TextBox>("ImportInput");
        if (inputBox is not null)
        {
            inputBox.TextChanged += (_, _) =>
            {
                var placeholder = vm.TreePanel.TryReplaceBuildCode(inputBox.Text ?? string.Empty);
                if (placeholder != null)
                {
                    inputBox.Text = placeholder;
                    inputBox.CaretIndex = placeholder.Length;
                }
            };
        }
    }
}
