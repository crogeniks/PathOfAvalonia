using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.ViewModels;

public sealed partial class GameWorkspaceViewModel : ObservableObject
{
    private const string NoDiffVersion = "None";
    private readonly IGameAssetService _assets;
    private readonly IBuildLibraryService? _buildLibrary;
    private readonly IUserSettingsService? _settings;
    private readonly Func<GameDefinition, string, Task> _switchTreeVersion;
    private Func<Guid, Task>? _openSavedBuild;
    private int _diffLoadRequest;
    private bool _trackingChanges = true;
    private bool _isDirty;
    private Guid? _savedBuildId;

    public GameWorkspaceViewModel(
        BuildWorkspaceState state,
        TreeSelectionViewModel treeSelection,
        BuildImportExportViewModel importExport,
        ITreeImageAssetResolver imageResolver,
        IGameAssetService assets,
        Func<GameDefinition, string, Task> switchTreeVersion,
        IRelayCommand backToLandingCommand,
        AtlasTreeViewModel? atlas = null,
        IBuildLibraryService? buildLibrary = null,
        IUserSettingsService? settings = null)
    {
        State = state;
        TreeSelection = treeSelection;
        ImportExport = importExport;
        ImageResolver = imageResolver;
        _assets = assets;
        _buildLibrary = buildLibrary;
        _settings = settings;
        _switchTreeVersion = switchTreeVersion;
        BackToLandingCommand = backToLandingCommand;
        Atlas = atlas;
        SelectedTreeVersion = state.Spec.Tree.Version;
        TreeVersionOptions = state.Game.TreeVersions;
        DiffTreeVersionOptions = [NoDiffVersion, .. state.Game.TreeVersions.Where(version => version != state.Spec.Tree.Version)];
        state.Spec.SpecChanged += MarkDirty;
        state.Equipment.EquipmentChanged += MarkDirty;
        if (Atlas is not null)
        {
            Atlas.StateChanged += MarkDirty;
        }
        _ = RefreshSavedBuildsAsync();
    }

    public BuildWorkspaceState State { get; }
    public TreeSelectionViewModel TreeSelection { get; }
    public BuildImportExportViewModel ImportExport { get; }
    [Obsolete("Use State. This projection does not own separate workspace state.")]
    public BuildWorkspaceState Workspace => State;
    [Obsolete("Use ImportExport or TreeSelection.")]
    public BuildImportExportViewModel TreePanel => ImportExport;
    public ITreeImageAssetResolver ImageResolver { get; }
    public IRelayCommand BackToLandingCommand { get; }
    public AtlasTreeViewModel? Atlas { get; }
    public bool HasAtlasTree => Atlas is not null;
    public string GameName => State.Game.DisplayName;
    public string TreeVersion => State.Spec.Tree.Version;
    // Keep the current PoE1 tree visible even while it has only one version, so the
    // version-selection affordance is already in place when the 3.29 tree arrives.
    public bool HasTreeVersionOptions => State.Game.Id == GameId.PathOfExile1 || TreeVersionOptions.Count > 1;
    public bool HasDiffVersionOptions => DiffTreeVersionOptions.Count > 1;
    public IReadOnlyList<string> TreeVersionOptions { get; }
    public IReadOnlyList<string> DiffTreeVersionOptions { get; }
    public string DiffSummary => State.Tree.Diff.HasChanges
        ? $"+{State.Tree.Diff.AddedCount} ~{State.Tree.Diff.ChangedCount} -{State.Tree.Diff.RemovedCount}"
        : string.Empty;
    public bool SupportsEquipment => State.Game.FeatureFlags.SupportsEquipmentImport;
    public bool IsDirty => _isDirty;
    public Guid? SavedBuildId => _savedBuildId;
    public bool HasSavedBuildSelection => SelectedSavedBuild is not null;
    public bool CanDeleteSavedBuild => SelectedSavedBuild is not null || SavedBuildId is not null;
    public string BuildToDeleteName => SelectedSavedBuild?.Name ?? BuildName;

    [ObservableProperty] public partial string BuildName { get; set; } = "Unnamed build";
    [ObservableProperty] public partial string BuildStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial IReadOnlyList<SavedBuildOptionViewModel> SavedBuildOptions { get; set; } = [];
    [ObservableProperty] public partial SavedBuildOptionViewModel? SelectedSavedBuild { get; set; }
    [ObservableProperty] public partial bool IsConfirmingBuildDelete { get; set; }
    [ObservableProperty] public partial string SelectedTreeVersion { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedDiffTreeVersion { get; set; } = NoDiffVersion;

    partial void OnBuildNameChanged(string value) => MarkDirty();

    partial void OnSelectedSavedBuildChanged(SavedBuildOptionViewModel? value)
    {
        OnPropertyChanged(nameof(HasSavedBuildSelection));
        OnPropertyChanged(nameof(CanDeleteSavedBuild));
        OnPropertyChanged(nameof(BuildToDeleteName));
    }

    public void SetOpenSavedBuildHandler(Func<Guid, Task> handler) => _openSavedBuild = handler;

    public async Task RestoreSavedBuildAsync(SavedBuild savedBuild)
    {
        _trackingChanges = false;
        try
        {
            ImportExport.RestoreBuild(savedBuild.CharacterBuild);
            if (Atlas is not null)
            {
                await Atlas.RestoreStateAsync(savedBuild.AtlasTreeVersion, savedBuild.AtlasNodeIds);
            }
            BuildName = savedBuild.Name;
            _savedBuildId = savedBuild.Id;
            OnPropertyChanged(nameof(SavedBuildId));
            OnPropertyChanged(nameof(CanDeleteSavedBuild));
            BuildStatus = $"Loaded {savedBuild.Name}";
        }
        finally
        {
            _trackingChanges = true;
        }
        MarkClean();
        await RefreshSavedBuildsAsync();
    }

    [RelayCommand]
    private Task SaveBuild() => SaveBuildCoreAsync(SavedBuildId ?? Guid.NewGuid());

    [RelayCommand]
    private Task SaveBuildAs() => SaveBuildCoreAsync(Guid.NewGuid());

    [RelayCommand]
    private async Task OpenSavedBuild()
    {
        if (IsDirty)
        {
            BuildStatus = "Save the current changes before opening another build.";
            return;
        }
        if (SelectedSavedBuild is { } selected && _openSavedBuild is not null)
        {
            await _openSavedBuild(selected.Id);
        }
    }

    [RelayCommand]
    private void RequestDeleteSavedBuild()
    {
        if (CanDeleteSavedBuild)
        {
            IsConfirmingBuildDelete = true;
        }
    }

    [RelayCommand]
    private void CancelDeleteSavedBuild() => IsConfirmingBuildDelete = false;

    [RelayCommand]
    private async Task ConfirmDeleteSavedBuild()
    {
        IsConfirmingBuildDelete = false;
        if (_buildLibrary is null || (SelectedSavedBuild?.Id ?? SavedBuildId) is not { } id)
        {
            return;
        }

        try
        {
            await _buildLibrary.DeleteAsync(id);
            if (SavedBuildId == id)
            {
                _savedBuildId = null;
                OnPropertyChanged(nameof(SavedBuildId));
                _isDirty = true;
                OnPropertyChanged(nameof(IsDirty));
            }
            if (_settings?.LastBuildId == id)
            {
                _settings.LastBuildId = null;
                _settings.Save();
            }
            BuildStatus = "Deleted saved build";
            await RefreshSavedBuildsAsync();
            OnPropertyChanged(nameof(CanDeleteSavedBuild));
        }
        catch (Exception ex)
        {
            BuildStatus = $"Could not delete build: {ex.Message}";
        }
    }

    [RelayCommand]
    private void NewBuild()
    {
        _trackingChanges = false;
        try
        {
            ImportExport.ClearBuild();
            Atlas?.Spec.Clear();
            BuildName = "Unnamed build";
            _savedBuildId = null;
            SelectedSavedBuild = null;
            BuildStatus = "New build";
            OnPropertyChanged(nameof(SavedBuildId));
            OnPropertyChanged(nameof(CanDeleteSavedBuild));
            if (_settings is not null)
            {
                _settings.LastBuildId = null;
                _settings.Save();
            }
        }
        finally
        {
            _trackingChanges = true;
        }
        MarkClean();
    }

    partial void OnSelectedTreeVersionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == State.Spec.Tree.Version)
        {
            return;
        }

        _ = _switchTreeVersion(State.Game, value);
    }

    async partial void OnSelectedDiffTreeVersionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == NoDiffVersion || value == State.Spec.Tree.Version)
        {
            _diffLoadRequest++;
            State.Tree.SetDiff(TreeDiff.Empty);
            OnPropertyChanged(nameof(DiffSummary));
            return;
        }

        var request = ++_diffLoadRequest;
        try
        {
            var baseline = await _assets.LoadTreeAsync(State.Game, value);
            if (request != _diffLoadRequest || SelectedDiffTreeVersion != value)
            {
                return;
            }

            State.Tree.SetDiff(TreeDiff.Compare(State.Spec.Tree, baseline));
            OnPropertyChanged(nameof(DiffSummary));
        }
        catch
        {
            if (request == _diffLoadRequest)
            {
                State.Tree.SetDiff(TreeDiff.Empty);
                OnPropertyChanged(nameof(DiffSummary));
            }
        }
    }

    private async Task SaveBuildCoreAsync(Guid id)
    {
        if (_buildLibrary is null)
        {
            BuildStatus = "Local build storage is unavailable.";
            return;
        }

        try
        {
            var name = string.IsNullOrWhiteSpace(BuildName) ? "Unnamed build" : BuildName.Trim();
            var saved = await _buildLibrary.SaveAsync(new SavedBuild(
                id,
                name,
                State.Game.Id,
                State.Spec.Tree.Version,
                ImportExport.CaptureBuild(),
                Atlas?.Tree.Version,
                Atlas?.Spec.AllocatedNodes.Order().ToArray() ?? [],
                DateTimeOffset.UtcNow));
            _trackingChanges = false;
            BuildName = saved.Name;
            _trackingChanges = true;
            _savedBuildId = saved.Id;
            OnPropertyChanged(nameof(SavedBuildId));
            OnPropertyChanged(nameof(CanDeleteSavedBuild));
            if (_settings is not null)
            {
                _settings.LastGameId = State.Game.Id;
                _settings.LastBuildId = saved.Id;
                _settings.Save();
            }
            MarkClean();
            BuildStatus = Atlas is null
                ? $"Saved {saved.Name} locally"
                : $"Saved {saved.Name} with Atlas passives";
            await RefreshSavedBuildsAsync();
        }
        catch (Exception ex)
        {
            _trackingChanges = true;
            BuildStatus = $"Could not save build: {ex.Message}";
        }
    }

    private async Task RefreshSavedBuildsAsync()
    {
        if (_buildLibrary is null)
        {
            return;
        }

        try
        {
            var selectedId = SavedBuildId ?? SelectedSavedBuild?.Id;
            var builds = await _buildLibrary.ListAsync(State.Game.Id);
            SavedBuildOptions = builds
                .Select(build => new SavedBuildOptionViewModel(
                    build.Id,
                    build.Name,
                    $"{build.TreeVersion} · {build.UpdatedAt.LocalDateTime:g}"))
                .ToArray();
            SelectedSavedBuild = selectedId is { } id
                ? SavedBuildOptions.FirstOrDefault(build => build.Id == id)
                : SavedBuildOptions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            BuildStatus = $"Could not read saved builds: {ex.Message}";
        }
    }

    private void MarkDirty()
    {
        if (!_trackingChanges || _isDirty)
        {
            return;
        }
        _isDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }

    private void MarkClean()
    {
        State.Equipment.IsDirty = false;
        if (!_isDirty)
        {
            return;
        }
        _isDirty = false;
        OnPropertyChanged(nameof(IsDirty));
    }

}

public sealed record SavedBuildOptionViewModel(Guid Id, string Name, string Details);
