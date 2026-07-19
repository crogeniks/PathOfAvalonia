using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.ViewModels;

/// <summary>
/// Compatibility composition facade for older callers. New workspace code uses
/// <see cref="TreeSelectionViewModel"/> and <see cref="BuildImportExportViewModel"/>
/// directly; this type contains no workflow logic.
/// </summary>
[System.Obsolete("Use TreeSelectionViewModel and BuildImportExportViewModel from BuildWorkspaceState.")]
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly BuildWorkspaceState _state;
    public MainWindowViewModel(
        PassiveSpec spec,
        IImportStrategy importStrategy,
        EquipmentViewModel equipment,
        IBuildPlannerExportService buildPlannerExportService,
        IBuildPlannerImportService buildPlannerImportService,
        IStorageProviderAccessor storageProviderAccessor)
    {
        _state = new BuildWorkspaceState(
            new GameDefinition(spec.Tree.GameId, spec.Tree.GameId.ToString(), spec.Tree.GameId.ToString(), spec.Tree.Version, string.Empty, null!, importStrategy, spec.Features),
            spec,
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            new PassiveTreeViewModel(spec),
            equipment);
        Selection = new TreeSelectionViewModel(_state);
        ImportExport = new BuildImportExportViewModel(
            _state,
            importStrategy,
            new BuildPlannerFileService(storageProviderAccessor, buildPlannerExportService, buildPlannerImportService));
    }

    public TreeSelectionViewModel Selection { get; }
    public BuildImportExportViewModel ImportExport { get; }
    public EquipmentViewModel Equipment => _state.Equipment;
    public PassiveTreeViewModel TreeViewModel => _state.Tree;

    public IReadOnlyList<string> ClassNames => Selection.ClassNames;
    public IReadOnlyList<string> AscendancyNames => Selection.AscendancyNames;
    public int SelectedClassIndex { get => Selection.SelectedClassIndex; set => Selection.SelectedClassIndex = value; }
    public int SelectedAscendancyIndex { get => Selection.SelectedAscendancyIndex; set => Selection.SelectedAscendancyIndex = value; }
    public string SelectedAscendancyName { get => Selection.SelectedAscendancyName; set => Selection.SelectedAscendancyName = value; }
    public bool IsTreeControlsCollapsed => Selection.IsTreeControlsCollapsed;
    public bool IsTreeControlsExpanded => Selection.IsTreeControlsExpanded;
    public string TreeControlsToggleText => Selection.TreeControlsToggleText;
    public IRelayCommand ToggleTreeControlsCommand => Selection.ToggleTreeControlsCommand;

    public bool IsImportSupported => ImportExport.IsImportSupported;
    public bool IsImportUnsupported => ImportExport.IsImportUnsupported;
    public bool SupportsBuildPlannerExport => ImportExport.SupportsBuildPlannerExport;
    public bool SupportsBuildPlannerImport => ImportExport.SupportsBuildPlannerImport;
    public bool CanExportBuildPlanner => ImportExport.CanExportBuildPlanner;
    public ImportedBuild? CurrentImportedBuild => ImportExport.CurrentImportedBuild;
    public string UnsupportedImportStatus => ImportExport.UnsupportedImportStatus;
    public string ImportPrompt => ImportExport.ImportPrompt;
    public string ImportPlaceholder => ImportExport.ImportPlaceholder;
    public string ImportInput { get => ImportExport.ImportInput; set => ImportExport.ImportInput = value; }
    public string ImportStatus => ImportExport.ImportStatus;
    public bool ImportStatusIsError => ImportExport.ImportStatusIsError;
    public IBrush ImportStatusForeground => ImportExport.ImportStatusForeground;
    public IReadOnlyList<ImportedVariantOptionViewModel> PassiveTreeVariantOptions => ImportExport.PassiveTreeVariantOptions;
    public IReadOnlyList<ImportedVariantOptionViewModel> ItemSetVariantOptions => ImportExport.ItemSetVariantOptions;
    public bool HasPassiveTreeVariants => ImportExport.HasPassiveTreeVariants;
    public bool HasItemSetVariants => ImportExport.HasItemSetVariants;
    public int SelectedPassiveTreeVariantIndex { get => ImportExport.SelectedPassiveTreeVariantIndex; set => ImportExport.SelectedPassiveTreeVariantIndex = value; }
    public int SelectedItemSetVariantIndex { get => ImportExport.SelectedItemSetVariantIndex; set => ImportExport.SelectedItemSetVariantIndex = value; }
    public IRelayCommand ImportCommand => ImportExport.ImportCommand;
    public IRelayCommand ClearCommand => ImportExport.ClearCommand;
    public IRelayCommand ImportBuildPlannerCommand => ImportExport.ImportBuildPlannerCommand;
    public IRelayCommand ExportBuildPlannerCommand => ImportExport.ExportBuildPlannerCommand;
    internal string? TryReplaceBuildCode(string text) => ImportExport.TryReplaceBuildCode(text);
}
