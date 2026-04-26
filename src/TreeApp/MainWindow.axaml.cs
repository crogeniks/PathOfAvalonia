using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp;

public partial class MainWindow : Window
{
    private PassiveSpec? _spec;
    private EquipmentView? _equipmentView;

    // Multi-KB PoB build codes lag Avalonia's TextBox on every click/select because
    // TextPresenter has no virtualization. After paste, if the text looks like a
    // build code, we stash the full string here and replace the TextBox contents
    // with a short marker so the control only ever lays out ~40 chars.
    private string? _pastedBuildCode;
    private const string PlaceholderPrefix = "<pasted build code — ";
    private const string PlaceholderSuffix = " chars, press Import>";

    public MainWindow()
    {
        InitializeComponent();

        _equipmentView = this.FindControl<EquipmentView>("EquipmentView")!;

        var root = this.FindControl<Grid>("Root")!;

        var treeUri = new Uri("avares://PathOfAvalonia.TreeApp/Assets/tree_3_28.json");
        TreeModel tree;
        using (var stream = AssetLoader.Open(treeUri))
        {
            tree = TreeLoader.LoadFromJson(stream, "3.28");
        }
        var spritesUri = new Uri("avares://PathOfAvalonia.TreeApp/Assets/sprites_3_28.json");
        SpriteMap sprites;
        using (var stream = AssetLoader.Open(spritesUri))
        {
            sprites = SpriteMap.LoadFromJson(stream);
        }
        _spec = new PassiveSpec(tree);

        // View goes in first so the overlay (Border defined in XAML) paints on top.
        root.Children.Insert(0, new PassiveTreeView(tree, _spec, sprites));

        var classSelector = this.FindControl<ComboBox>("ClassSelector")!;
        classSelector.ItemsSource = CharacterClasses.Names;
        classSelector.SelectedIndex = _spec.SelectedClassIndex;
        classSelector.SelectionChanged += (_, _) =>
        {
            if (classSelector.SelectedIndex >= 0)
            {
                _spec?.SetClass(classSelector.SelectedIndex);
            }
        };
        _spec.SpecChanged += () =>
        {
            if (classSelector.SelectedIndex != _spec.SelectedClassIndex)
            {
                classSelector.SelectedIndex = _spec.SelectedClassIndex;
            }
        };

        var input = this.FindControl<TextBox>("ImportInput")!;
        this.FindControl<Button>("ImportButton")!.Click += (_, _) => RunImport();
        this.FindControl<Button>("ClearButton")!.Click += (_, _) => ClearSpec();
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                RunImport();
                e.Handled = true;
            }
        };
        input.TextChanged += (_, _) => OnInputTextChanged(input);
    }

    private void OnInputTextChanged(TextBox input)
    {
        var current = input.Text ?? string.Empty;
        // When the TextBox already shows our placeholder, this TextChanged is either
        // the echo of us swapping it in or a user-initiated edit of the marker. Either
        // way, don't touch _pastedBuildCode — the stashed real code is still the truth.
        if (current.StartsWith(PlaceholderPrefix, StringComparison.Ordinal)
            && current.EndsWith(PlaceholderSuffix, StringComparison.Ordinal))
        {
            return;
        }
        if (current.Length > 500 && PobBuildCodeDecoder.LooksLikeBuildCode(current.Trim()))
        {
            _pastedBuildCode = current;
            input.Text = $"{PlaceholderPrefix}{current.Length}{PlaceholderSuffix}";
            input.CaretIndex = input.Text!.Length;
        }
        else
        {
            _pastedBuildCode = null;
        }
    }

    private void RunImport()
    {
        if (_spec is null)
        {
            return;
        }
        var input = this.FindControl<TextBox>("ImportInput")!;
        var status = this.FindControl<TextBlock>("ImportStatus")!;
        var text = _pastedBuildCode ?? input.Text ?? string.Empty;
        try
        {
            var build = BuildImporter.Import(text);
            var result = _spec.ApplyImport(build);
            _equipmentView?.LoadItems(build.Items);
            status.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0xE0, 0x90));
            status.Text = $"{build.Source}: {result.Applied} nodes (+{result.ClusterSkipped} cluster skipped)";
        }
        catch (Exception ex)
        {
            status.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x8A));
            status.Text = $"Import failed: {ex.Message}";
        }
    }

    private void ClearSpec()
    {
        if (_spec is null)
        {
            return;
        }
        _spec.Clear();
        _equipmentView?.ClearItems();
        _pastedBuildCode = null;
        this.FindControl<TextBox>("ImportInput")!.Text = string.Empty;
        this.FindControl<TextBlock>("ImportStatus")!.Text = "cleared";
    }
}
