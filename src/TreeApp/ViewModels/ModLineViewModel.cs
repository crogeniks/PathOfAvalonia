using Avalonia.Media;

namespace PathOfAvalonia.TreeApp.ViewModels;

public sealed class ModLineViewModel
{
    public string Text { get; init; } = string.Empty;
    public IBrush Brush { get; init; } = Brushes.White;
}
