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
        root.Children.Insert(0, new PassiveTreeView(vm.Spec.Tree, vm.Spec, sprites));

        // CaretIndex is a pure UI concern the ViewModel can't reach: reset to end
        // whenever ImportInput changes so the placeholder marker looks clean.
        var inputBox = this.FindControl<TextBox>("ImportInput")!;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ImportInput))
                inputBox.CaretIndex = inputBox.Text?.Length ?? 0;
        };
    }
}
