using PathOfAvalonia.TreeDomain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PathOfAvalonia.TreeApp.ViewModels;

public sealed partial class GameChoiceViewModel(GameDefinition game, bool isLastUsed) : ObservableObject
{
    public GameId Id => game.Id;
    public string DisplayName => game.DisplayName;
    public string Version => game.DefaultTreeVersion;
    [ObservableProperty] private bool _isLastUsed = isLastUsed;
    public string SupportStatus => game.Id == GameId.PathOfExile1
        ? "Passive tree, imports, equipment"
        : "Passive tree, icons, build import, equipment";
}
