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
    private readonly IImportService _importService;
    private bool _syncingClass;

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
    public IReadOnlyList<string> ClassNames => CharacterClasses.Names;

    [ObservableProperty] private int _selectedClassIndex;
    [ObservableProperty] private string _importInput = string.Empty;
    [ObservableProperty] private string _importStatus = string.Empty;
    [ObservableProperty] private bool _importStatusIsError;

    public IBrush ImportStatusForeground =>
        string.IsNullOrEmpty(ImportStatus) ? StatusDefaultBrush :
        ImportStatusIsError ? StatusErrorBrush : StatusSuccessBrush;

    public MainWindowViewModel(PassiveSpec spec, IImportService importService, EquipmentViewModel equipment)
    {
        _spec = spec;
        _importService = importService;
        Equipment = equipment;
        TreeViewModel = new PassiveTreeViewModel(spec);
        _selectedClassIndex = spec.SelectedClassIndex;
        _spec.SpecChanged += OnSpecChanged;
    }

    private void OnSpecChanged()
    {
        _syncingClass = true;
        SelectedClassIndex = _spec.SelectedClassIndex;
        _syncingClass = false;
    }

    partial void OnSelectedClassIndexChanged(int value)
    {
        if (!_syncingClass && value >= 0)
            _spec.SetClass(value);
    }

    partial void OnImportInputChanged(string value)
    {
        // Guard: don't overwrite _pastedBuildCode when we set our own placeholder.
        if (value.StartsWith(PlaceholderPrefix, StringComparison.Ordinal)
            && value.EndsWith(PlaceholderSuffix, StringComparison.Ordinal))
            return;

        if (value.Length > 500 && PobBuildCodeDecoder.LooksLikeBuildCode(value.Trim()))
        {
            _pastedBuildCode = value;
            ImportInput = $"{PlaceholderPrefix}{value.Length}{PlaceholderSuffix}";
        }
        else
        {
            _pastedBuildCode = null;
        }
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
            return;
        try
        {
            var build = _importService.Import(text);
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
}
