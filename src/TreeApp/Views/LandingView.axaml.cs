using Avalonia.Controls;
using Avalonia.Interactivity;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Views;

public partial class LandingView : UserControl
{
    public LandingView()
    {
        InitializeComponent();
    }

    private void OpenClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell
            && sender is Button { Tag: GameId gameId })
        {
            shell.SelectGameCommand.Execute(gameId);
        }
    }
}
