using System.ComponentModel;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeApp.Views;

namespace PathOfAvalonia.TreeApp;

public partial class MainWindow : Window
{
    public MainWindow() : this(App.Services.GetRequiredService<ShellViewModel>())
    {
    }

    public MainWindow(ShellViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += OnShellPropertyChanged;
        UpdateShellHost(vm);
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ShellViewModel vm
            && (e.PropertyName == nameof(ShellViewModel.CurrentPage)
                || e.PropertyName == nameof(ShellViewModel.ActiveWorkspace)))
        {
            UpdateShellHost(vm);
        }
    }

    private void UpdateShellHost(ShellViewModel vm)
    {
        var host = this.FindControl<ContentControl>("ShellHost")!;
        if (vm.CurrentPage == ShellPage.Workspace && vm.ActiveWorkspace is { } workspace)
        {
            host.Content = new GameWorkspaceView { DataContext = workspace };
        }
        else
        {
            host.Content = new LandingView { DataContext = vm };
        }
    }
}

