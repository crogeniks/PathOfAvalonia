using System;
using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly PassiveSpec _spec;
    private readonly IImportStrategy _importStrategy;
    private bool _syncingClass;
    private bool _syncingAscendancy;

    // Multi-KB PoB build codes lag Avalonia's TextBox on every click/select.
    // After paste, we stash the full string here and replace the TextBox text
    // with a short marker so the control only ever lays out ~40 chars.
    private string? _pastedBuildCode;
    private const string PlaceholderPrefix = "<pasted build code — ";
    private const string PlaceholderSuffix = " chars, press Import>";

    private static readonly IBrush StatusDefaultBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0xE0, 0x90));
    private static readonly IBrush StatusErrorBrush   = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x8A));

    public EquipmentViewModel Equipment { get; }
    public PassiveTreeViewModel TreeViewModel { get; }
    public IReadOnlyList<string> ClassNames => _spec.Classes.ClassNames;
    public IReadOnlyList<string> AscendancyNames => _spec.Classes.AscendancyNames(SelectedClassIndex);
    public bool IsImportSupported => _importStrategy.IsSupported;
    public bool IsImportUnsupported => !_importStrategy.IsSupported;
    public string UnsupportedImportStatus => "Build import is not available for Path of Exile 2 yet.";

    [ObservableProperty] private int _selectedClassIndex;
    [ObservableProperty] private int _selectedAscendancyIndex;
    [ObservableProperty] private string _selectedAscendancyName = "None";
    [ObservableProperty] private string _importInput = string.Empty;
    [ObservableProperty] private string _importStatus = string.Empty;
    [ObservableProperty] private bool _importStatusIsError;

    public IBrush ImportStatusForeground =>
        string.IsNullOrEmpty(ImportStatus) ? StatusDefaultBrush :
        ImportStatusIsError ? StatusErrorBrush : StatusSuccessBrush;

    public MainWindowViewModel(PassiveSpec spec, IImportService importService, EquipmentViewModel equipment)
        : this(spec, new ImportServiceStrategyAdapter(importService), equipment)
    {
    }

    public MainWindowViewModel(PassiveSpec spec, IImportStrategy importStrategy, EquipmentViewModel equipment)
    {
        _spec = spec;
        _importStrategy = importStrategy;
        Equipment = equipment;
        TreeViewModel = new PassiveTreeViewModel(spec);
        _selectedClassIndex = spec.SelectedClassIndex;
        _selectedAscendancyIndex = spec.SelectedAscendancyIndex;
        _selectedAscendancyName = AscendancyNameAt(_selectedClassIndex, _selectedAscendancyIndex);
        _spec.SpecChanged += OnSpecChanged;
    }

    private void OnSpecChanged()
    {
        _syncingClass = true;
        _syncingAscendancy = true;
        SelectedClassIndex = _spec.SelectedClassIndex;
        OnPropertyChanged(nameof(AscendancyNames));
        SelectedAscendancyIndex = _spec.SelectedAscendancyIndex;
        SelectedAscendancyName = AscendancyNameAt(SelectedClassIndex, SelectedAscendancyIndex);
        _syncingClass = false;
        _syncingAscendancy = false;
    }

    partial void OnSelectedClassIndexChanged(int value)
    {
        OnPropertyChanged(nameof(AscendancyNames));
        if (!_syncingClass && value >= 0)
        {
            _spec.SetClass(value);
        }
    }

    partial void OnSelectedAscendancyIndexChanged(int value)
    {
        if (!_syncingAscendancy && value >= 0)
        {
            _spec.SetAscendancy(value);
        }
    }

    partial void OnSelectedAscendancyNameChanged(string value)
    {
        if (_syncingAscendancy)
        {
            return;
        }
        var names = AscendancyNames;
        var index = AscendancyIndexOf(names, value);
        if (index >= 0)
        {
            _spec.SetAscendancy(index);
        }
    }

    partial void OnImportInputChanged(string value)
    {
        // When the view sets the placeholder marker, don't clear _pastedBuildCode.
        if (value.StartsWith(PlaceholderPrefix, StringComparison.Ordinal)
            && value.EndsWith(PlaceholderSuffix, StringComparison.Ordinal))
        {
            return;
        }
        _pastedBuildCode = null;
    }

    // Called by the code-behind TextChanged handler, which sets TextBox.Text directly
    // (bypassing the TwoWay binding reentrancy guard that would suppress the update).
    // Returns the placeholder string to display, or null if no replacement is needed.
    internal string? TryReplaceBuildCode(string text)
    {
        if (text.Length <= 500 || !PobBuildCodeDecoder.LooksLikeBuildCode(text.Trim()))
        {
            return null;
        }

        _pastedBuildCode = text;
        return $"{PlaceholderPrefix}{text.Length}{PlaceholderSuffix}";
    }

    partial void OnImportStatusChanged(string value) =>
        OnPropertyChanged(nameof(ImportStatusForeground));

    partial void OnImportStatusIsErrorChanged(bool value) =>
        OnPropertyChanged(nameof(ImportStatusForeground));

    [RelayCommand]
    private void Import()
    {
        var text = _pastedBuildCode ?? ImportInput;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        try
        {
            var build = _importStrategy.Import(text);
            var result = _spec.ApplyImport(build);
            Equipment.LoadBuild(build);
            ImportStatus = $"{build.Source}: {result.Applied} nodes (+{result.ClusterSkipped} cluster skipped)";
            ImportStatusIsError = false;
        }
        catch (Exception ex)
        {
            ImportStatus = $"Import failed: {ex.Message}";
            ImportStatusIsError = true;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _spec.Clear();
        Equipment.Clear();
        _pastedBuildCode = null;
        ImportInput = string.Empty;
        ImportStatus = "cleared";
        ImportStatusIsError = false;
    }

    private string AscendancyNameAt(int classIndex, int ascendancyIndex)
    {
        var names = _spec.Classes.AscendancyNames(classIndex);
        return ascendancyIndex >= 0 && ascendancyIndex < names.Count ? names[ascendancyIndex] : names[0];
    }

    private static int AscendancyIndexOf(IReadOnlyList<string> names, string value)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (names[i] == value)
            {
                return i;
            }
        }
        return -1;
    }

    private sealed class ImportServiceStrategyAdapter(IImportService importService) : IImportStrategy
    {
        public bool IsSupported => true;
        public ImportedBuild Import(string text) => importService.Import(text);
    }
}
