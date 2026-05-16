using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp;

public partial class MainWindow : Window
{
    // Parameterless ctor used by Avalonia's runtime XAML loader (hot reload / design mode).
    public MainWindow() : this(
        App.Services.GetRequiredService<MainWindowViewModel>(),
        App.Services.GetRequiredService<SpriteMap>()) { }

    public MainWindow(MainWindowViewModel vm, SpriteMap sprites)
    {
        InitializeComponent();
        DataContext = vm;

        // PassiveTreeView is a canvas-rendering Control — it can't be declared in AXAML
        // because it needs the live PassiveSpec instance. Insert it behind the overlay.
        var root = this.FindControl<Grid>("Root")!;
        root.Children.Insert(0, new PassiveTreeView(vm.TreeViewModel, sprites));

        // Replace large build codes with a short placeholder directly in the TextBox.
        // Direct TextBox.Text write bypasses the TwoWay binding's reentrancy guard,
        // which would otherwise suppress the source→target update from inside the
        // PropertyChanged cycle triggered by the user's paste.
        var inputBox = this.FindControl<TextBox>("ImportInput")!;
        inputBox.TextChanged += (_, _) =>
        {
            var placeholder = vm.TryReplaceBuildCode(inputBox.Text ?? string.Empty);
            if (placeholder != null)
            {
                inputBox.Text = placeholder;
                inputBox.CaretIndex = placeholder.Length;
            }
        };
    }
}
