using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.ViewModels;

/// <summary>Owns build decoding, variant selection, file import/export, and import feedback.</summary>
public sealed partial class BuildImportExportViewModel : ObservableObject
{
    private const string PlaceholderPrefix = "<pasted build code — ";
    private const string PlaceholderSuffix = " chars, press Import>";
    private static readonly IBrush StatusDefaultBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD));
    private static readonly IBrush StatusSuccessBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0xE0, 0x90));
    private static readonly IBrush StatusErrorBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x8A));

    private readonly BuildWorkspaceState _state;
    private readonly IImportStrategy _importStrategy;
    private readonly IBuildPlannerFileService _files;
    private bool _syncingVariants;
    private string? _pastedBuildCode;
    private ImportedBuild? _lastImportedBuild;
    private ImportResult? _lastImportResult;

    public BuildImportExportViewModel(BuildWorkspaceState state, IImportStrategy importStrategy, IBuildPlannerFileService files)
    {
        _state = state;
        _importStrategy = importStrategy;
        _files = files;
    }

    public bool IsImportSupported => _importStrategy.IsSupported;
    public bool IsImportUnsupported => !IsImportSupported;
    public bool SupportsBuildPlannerExport => _state.Spec.Tree.GameId == GameId.PathOfExile2;
    public bool SupportsBuildPlannerImport => SupportsBuildPlannerExport;
    public bool CanExportBuildPlanner => SupportsBuildPlannerExport && _lastImportedBuild is not null;
    public ImportedBuild? CurrentImportedBuild => _lastImportedBuild;
    public string UnsupportedImportStatus => "Build import is not available for this game yet.";
    public string ImportPrompt => _state.Spec.Tree.GameId == GameId.PathOfExile2 ? "Paste a Path of Building 2 build code or pobb.in URL" : "Paste a PoE tree URL, PoB build code, or pobb.in URL:";
    public string ImportPlaceholder => _state.Spec.Tree.GameId == GameId.PathOfExile2 ? "https://pobb.in/... or PoB2 code" : "https://pobb.in/... or PoB code";
    public IBrush ImportStatusForeground => string.IsNullOrEmpty(ImportStatus) ? StatusDefaultBrush : ImportStatusIsError ? StatusErrorBrush : StatusSuccessBrush;

    [ObservableProperty] public partial string ImportInput { get; set; } = string.Empty;
    [ObservableProperty] public partial string ImportStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ImportStatusIsError { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ImportedVariantOptionViewModel> PassiveTreeVariantOptions { get; set; } = [];
    [ObservableProperty] public partial IReadOnlyList<ImportedVariantOptionViewModel> ItemSetVariantOptions { get; set; } = [];
    [ObservableProperty] public partial bool HasPassiveTreeVariants { get; set; }
    [ObservableProperty] public partial bool HasItemSetVariants { get; set; }
    [ObservableProperty] public partial int SelectedPassiveTreeVariantIndex { get; set; }
    [ObservableProperty] public partial int SelectedItemSetVariantIndex { get; set; }

    partial void OnImportInputChanged(string value)
    {
        if (!(value.StartsWith(PlaceholderPrefix, StringComparison.Ordinal) && value.EndsWith(PlaceholderSuffix, StringComparison.Ordinal))) _pastedBuildCode = null;
    }

    // Called by the Avalonia-only input handler after a paste; the full code stays out of TextBox layout.
    internal string? TryReplaceBuildCode(string text)
    {
        if (text.Length <= 500 || !PobBuildCodeDecoder.LooksLikeBuildCode(text.Trim())) return null;
        _pastedBuildCode = text;
        return $"{PlaceholderPrefix}{text.Length}{PlaceholderSuffix}";
    }

    partial void OnImportStatusChanged(string value) => OnPropertyChanged(nameof(ImportStatusForeground));
    partial void OnImportStatusIsErrorChanged(bool value) => OnPropertyChanged(nameof(ImportStatusForeground));

    partial void OnSelectedPassiveTreeVariantIndexChanged(int value)
    {
        if (_syncingVariants || _lastImportedBuild is null || value < 0) return;
        try
        {
            _lastImportedBuild = _lastImportedBuild.WithPassiveTreeVariant(value);
            _lastImportResult = _state.Spec.ApplyImport(_lastImportedBuild);
            _state.Equipment.LoadBuild(_lastImportedBuild);
            SetSuccess(BuildImportStatus(_lastImportResult));
        }
        catch (ArgumentOutOfRangeException) { }
    }

    partial void OnSelectedItemSetVariantIndexChanged(int value)
    {
        if (_syncingVariants || _lastImportedBuild is null || value < 0) return;
        try
        {
            _lastImportedBuild = _lastImportedBuild.WithItemSetVariant(value);
            _state.Equipment.LoadBuild(_lastImportedBuild);
            if (_lastImportResult is not null) SetSuccess(BuildImportStatus(_lastImportResult with { Build = _lastImportedBuild }));
        }
        catch (ArgumentOutOfRangeException) { }
    }

    [RelayCommand]
    private async Task Import()
    {
        var text = _pastedBuildCode ?? ImportInput;
        if (string.IsNullOrWhiteSpace(text)) return;
        try { ApplyImportedBuild(await _importStrategy.ImportAsync(text)); }
        catch (Exception ex) { ResetVariantState(); SetError($"Import failed: {ex.Message}"); }
    }

    [RelayCommand]
    private void Clear()
    {
        _state.Spec.Clear();
        _state.Equipment.Clear();
        ResetVariantState();
        _pastedBuildCode = null;
        ImportInput = string.Empty;
        SetSuccess("cleared");
    }

    [RelayCommand]
    private async Task ExportBuildPlanner()
    {
        if (_lastImportedBuild is not { } build) return;
        try
        {
            var result = await _files.ExportAsync(new BuildWorkspaceExportRequest(_state.Spec.Tree, _state.Spec.Classes, build), default);
            if (result is null) return;
            var exported = result.FileCount == 1 ? result.Name : $"{result.FileCount} build files to {result.Name}";
            SetSuccess(result.SkippedNodeCount == 0 ? $"Exported {exported}" : $"Exported {exported}; skipped {result.SkippedNodeCount} node(s) without Build Planner ids");
        }
        catch (Exception ex) { SetError($"Export failed: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task ImportBuildPlanner()
    {
        try
        {
            var result = await _files.ImportAsync(new BuildWorkspaceImportRequest(_state.Spec.Tree), default);
            if (result is null) return;
            ApplyImportedBuild(result.Build);
            if (result.SkippedPassiveCount > 0) ImportStatus += $"; skipped {result.SkippedPassiveCount} unknown Build Planner passive id(s)";
        }
        catch (Exception ex) { ResetVariantState(); SetError($"Build Planner import failed: {ex.Message}"); }
    }

    private void ApplyImportedBuild(ImportedBuild build)
    {
        _syncingVariants = true;
        _lastImportedBuild = build;
        PassiveTreeVariantOptions = build.PassiveTreeVariants.Select(variant => new ImportedVariantOptionViewModel(variant.Index, variant.DisplayName)).ToArray();
        ItemSetVariantOptions = build.ItemSetVariants.Select(variant => new ImportedVariantOptionViewModel(variant.Index, variant.DisplayName)).ToArray();
        HasPassiveTreeVariants = PassiveTreeVariantOptions.Count > 1;
        HasItemSetVariants = ItemSetVariantOptions.Count > 1;
        SelectedPassiveTreeVariantIndex = build.ActivePassiveTreeVariantIndex;
        SelectedItemSetVariantIndex = build.ActiveItemSetVariantIndex;
        _syncingVariants = false;
        OnExportStateChanged();
        _lastImportResult = _state.Spec.ApplyImport(build);
        _state.Equipment.LoadBuild(build);
        SetSuccess(BuildImportStatus(_lastImportResult));
    }

    private void ResetVariantState()
    {
        _lastImportedBuild = null;
        _lastImportResult = null;
        _syncingVariants = true;
        PassiveTreeVariantOptions = [];
        ItemSetVariantOptions = [];
        HasPassiveTreeVariants = false;
        HasItemSetVariants = false;
        SelectedPassiveTreeVariantIndex = 0;
        SelectedItemSetVariantIndex = 0;
        _syncingVariants = false;
        OnExportStateChanged();
    }

    private void SetSuccess(string status) { ImportStatus = status; ImportStatusIsError = false; }
    private void SetError(string status) { ImportStatus = status; ImportStatusIsError = true; }
    private void OnExportStateChanged() { OnPropertyChanged(nameof(CanExportBuildPlanner)); OnPropertyChanged(nameof(CurrentImportedBuild)); }

    private static string BuildImportStatus(ImportResult result)
    {
        var build = result.Build;
        var status = $"{build.Source}: {result.Applied} nodes applied, {result.Skipped} skipped";
        if (result.WeaponSet1Allocations > 0) status += $", weapon set 1: {result.WeaponSet1Allocations}";
        if (result.WeaponSet2Allocations > 0) status += $", weapon set 2: {result.WeaponSet2Allocations}";
        if (build.PassiveTreeVariants.Count > 1 && build.PassiveTreeVariants.FirstOrDefault(v => v.Index == build.ActivePassiveTreeVariantIndex) is { } selectedPassive) status += $", tree: {selectedPassive.DisplayName}";
        if (build.ItemSetVariants.Count > 1 && build.ItemSetVariants.FirstOrDefault(v => v.Index == build.ActiveItemSetVariantIndex) is { } selectedItemSet) status += $", item set: {selectedItemSet.DisplayName}";
        if (build.Items.Count > 0) status += $", {build.Items.Count} items imported";
        var skillGroupCount = build.Skills.SkillSets.Sum(set => set.Groups.Count);
        if (skillGroupCount > 0) status += $", {skillGroupCount} skill groups imported";
        if (build.Metrics.Source == ImportedMetricSource.SavedXmlSnapshot) status += ", DPS: saved snapshot";
        var unsupported = new List<string>();
        if (result.UnsupportedClusterJewels > 0) unsupported.Add($"{result.UnsupportedClusterJewels} cluster nodes");
        if (result.UnsupportedAttributeOverrides > 0) unsupported.Add($"{result.UnsupportedAttributeOverrides} attribute override{(result.UnsupportedAttributeOverrides == 1 ? string.Empty : "s")}");
        if (result.UnsupportedSocketedJewels > 0) unsupported.Add($"{result.UnsupportedSocketedJewels} socketed jewel{(result.UnsupportedSocketedJewels == 1 ? string.Empty : "s")}");
        return unsupported.Count > 0 ? status + "; unsupported: " + string.Join(", ", unsupported) : status;
    }
}

public sealed record ImportedVariantOptionViewModel(int Index, string DisplayName);
