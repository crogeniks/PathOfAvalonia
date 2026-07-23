using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Atlas;

namespace PathOfAvalonia.TreeApp.ViewModels;

public sealed record AtlasAggregatedStatItemViewModel(
    string Text,
    int SourceCount,
    double Opacity);

public sealed record AtlasAggregatedStatGroupViewModel(
    string Type,
    IReadOnlyList<AtlasAggregatedStatItemViewModel> Stats);

public sealed partial class AtlasTreeViewModel : ObservableObject
{
    private const string NoDiffVersion = "None";
    private static readonly IBrush NormalPointBrush = new SolidColorBrush(Color.FromRgb(0xC6, 0xB3, 0x6A));
    private static readonly IBrush LimitPointBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x58, 0x58));
    private readonly GameDefinition _game;
    private readonly IGameAssetService _assets;
    private readonly IGameAssetLayoutRegistry _assetLayouts;
    private IReadOnlyDictionary<int, string> _searchIndex;
    private int _versionLoadRequest;
    private int _diffLoadRequest;
    private bool _suppressVersionSelectionLoad;
    private AtlasPassiveSpec _spec;
    private int? _hoverNodeId;
    private AtlasHoverPath _hoverPath = AtlasHoverPath.Empty;
    private AtlasTreeDiff _diff = AtlasTreeDiff.Empty;
    private IReadOnlyList<AggregatedAtlasStatGroup> _aggregatedStatGroups = [];
    private HashSet<int> _searchResultNodeIds = [];
    private HashSet<int> _highlightedClusterIconNodeIds = [];

    public AtlasTreeViewModel(
        GameDefinition game,
        AtlasTreeModel tree,
        SpriteMap sprites,
        ITreeImageAssetResolver imageResolver,
        IGameAssetService assets,
        IGameAssetLayoutRegistry assetLayouts)
    {
        _game = game;
        _assets = assets;
        _assetLayouts = assetLayouts;
        _spec = new AtlasPassiveSpec(tree);
        _searchIndex = BuildSearchIndex(tree.Nodes.Values);
        Sprites = sprites;
        ImageResolver = imageResolver;
        VersionOptions = game.AtlasTreeVersions;
        SelectedVersion = tree.Version;
        RebuildDiffVersionOptions();
        _spec.SpecChanged += OnSpecChanged;
        RefreshAllocationSummary();
    }

    public event Action? RedrawRequested;
    public event Action? CanvasChanged;
    public event Action? StateChanged;

    public AtlasPassiveSpec Spec => _spec;
    public AtlasTreeModel Tree => _spec.Tree;
    public SpriteMap Sprites { get; private set; }
    public ITreeImageAssetResolver ImageResolver { get; private set; }
    public IReadOnlyList<string> VersionOptions { get; }
    public IReadOnlyList<string> DiffVersionOptions { get; private set; } = [NoDiffVersion];
    public IReadOnlyList<AtlasAggregatedStatGroupViewModel> AggregatedStatGroups { get; private set; } = [];
    public IReadOnlySet<int> AllocatedNodes => _spec.AllocatedNodes;
    public IReadOnlySet<int> SearchResultNodeIds => _searchResultNodeIds;
    public IReadOnlySet<int> HighlightedClusterIconNodeIds => _highlightedClusterIconNodeIds;
    public AtlasHoverPath HoverPath => _hoverPath;
    public IReadOnlySet<int> HoverPathNodes => _hoverPath.NodeIds;
    public AtlasTreeDiff Diff => _diff;
    public int? HoverNodeId => _hoverNodeId;
    public AtlasNode? HoverNode => _hoverNodeId is { } id && Tree.Nodes.TryGetValue(id, out var node) ? node : null;
    public int SearchResultCount => _searchResultNodeIds.Count;
    public bool HasActiveSearch => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasAggregatedStats => AggregatedStatGroups.Count > 0;
    public bool HasDiffVersionOptions => DiffVersionOptions.Count > 1;
    public bool IsDirty => AllocatedPointCount > 0;
    public int AllocatedPointCount => _spec.AllocatedPointCount;
    public int PointLimit => Tree.PointLimit;
    public bool IsPointLimitReached => AllocatedPointCount >= PointLimit;
    public string PointUsage => $"{AllocatedPointCount} / {PointLimit} points";
    public IBrush PointUsageBrush => IsPointLimitReached ? LimitPointBrush : NormalPointBrush;
    public string DiffSummary => Diff.HasChanges
        ? $"+{Diff.AddedCount} ~{Diff.ChangedCount} -{Diff.RemovedCount}"
        : string.Empty;
    public long VisualRevision { get; private set; }

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedVersion { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedDiffVersion { get; set; } = NoDiffVersion;
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    public bool IsAllocated(int nodeId) => _spec.IsAllocated(nodeId);

    public void SetHover(int? nodeId)
    {
        if (_hoverNodeId == nodeId)
        {
            return;
        }
        _hoverNodeId = nodeId;
        _hoverPath = nodeId is { } id ? _spec.HoverPathTo(id) : AtlasHoverPath.Empty;
        RequestRedraw();
    }

    public void ToggleNode(int nodeId) => _spec.Toggle(nodeId);
    public void AllocateHoverPath() => _spec.AllocatePath(_hoverPath);

    public bool HighlightSimilarClusters(int nodeId)
    {
        if (!Tree.Nodes.TryGetValue(nodeId, out var source) || source.Type != AtlasNodeType.ClusterIcon)
        {
            return false;
        }
        _highlightedClusterIconNodeIds = Tree.Nodes.Values
            .Where(node => node.Type == AtlasNodeType.ClusterIcon && SameClusterCategory(source, node))
            .Select(node => node.Id)
            .ToHashSet();
        RequestRedraw();
        return _highlightedClusterIconNodeIds.Count > 0;
    }

    public void ClearClusterHighlights()
    {
        if (_highlightedClusterIconNodeIds.Count == 0)
        {
            return;
        }
        _highlightedClusterIconNodeIds = [];
        RequestRedraw();
    }

    public void SetDiff(AtlasTreeDiff? diff)
    {
        _diff = diff ?? AtlasTreeDiff.Empty;
        OnPropertyChanged(nameof(Diff));
        OnPropertyChanged(nameof(DiffSummary));
        RequestRedraw();
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateSearchResults(value);
        RefreshAggregatedSearchPresentation();
        OnPropertyChanged(nameof(HasActiveSearch));
        RequestRedraw();
    }

    async partial void OnSelectedVersionChanged(string value)
    {
        if (_suppressVersionSelectionLoad || string.IsNullOrWhiteSpace(value) || value == Tree.Version)
        {
            return;
        }
        await LoadVersionAsync(value);
    }

    public async Task RestoreStateAsync(string? version, IEnumerable<int> allocatedNodeIds)
    {
        if (!string.IsNullOrWhiteSpace(version)
            && VersionOptions.Contains(version, StringComparer.Ordinal)
            && version != Tree.Version)
        {
            _suppressVersionSelectionLoad = true;
            SelectedVersion = version;
            _suppressVersionSelectionLoad = false;
            await LoadVersionAsync(version);
        }

        _spec.RestoreConnectedAllocations(allocatedNodeIds);
    }

    private async Task LoadVersionAsync(string value)
    {
        var request = ++_versionLoadRequest;
        IsLoading = true;
        StatusMessage = $"Loading Atlas {value}…";
        try
        {
            var treeTask = _assets.LoadAtlasTreeAsync(_game, value);
            var spritesTask = _assets.LoadAtlasSpritesAsync(_game, value);
            await Task.WhenAll(treeTask, spritesTask);
            if (request != _versionLoadRequest || SelectedVersion != value)
            {
                return;
            }
            var previousAllocations = _spec.AllocatedNodes.ToArray();
            var newSpec = new AtlasPassiveSpec(await treeTask);
            newSpec.RestoreConnectedAllocations(previousAllocations);
            ReplaceCanvas(
                newSpec,
                await spritesTask,
                new AtlasTreeImageAssetResolver(_game, _assets, _assetLayouts, value));
            RebuildDiffVersionOptions();
            SelectedDiffVersion = NoDiffVersion;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            if (request == _versionLoadRequest)
            {
                StatusMessage = $"Could not load Atlas {value}: {ex.Message}";
                SelectedVersion = Tree.Version;
            }
        }
        finally
        {
            if (request == _versionLoadRequest)
            {
                IsLoading = false;
            }
        }
    }

    async partial void OnSelectedDiffVersionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == NoDiffVersion || value == Tree.Version)
        {
            _diffLoadRequest++;
            SetDiff(AtlasTreeDiff.Empty);
            return;
        }
        var request = ++_diffLoadRequest;
        try
        {
            var baseline = await _assets.LoadAtlasTreeAsync(_game, value);
            if (request == _diffLoadRequest && SelectedDiffVersion == value)
            {
                SetDiff(AtlasTreeDiff.Compare(Tree, baseline));
            }
        }
        catch (Exception ex)
        {
            if (request == _diffLoadRequest)
            {
                SetDiff(AtlasTreeDiff.Empty);
                StatusMessage = $"Could not compare Atlas {value}: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void Reset() => _spec.Clear();

    private void ReplaceCanvas(
        AtlasPassiveSpec spec,
        SpriteMap sprites,
        ITreeImageAssetResolver imageResolver)
    {
        _spec.SpecChanged -= OnSpecChanged;
        _spec = spec;
        Sprites = sprites;
        ImageResolver = imageResolver;
        _spec.SpecChanged += OnSpecChanged;
        _hoverNodeId = null;
        _hoverPath = AtlasHoverPath.Empty;
        _diff = AtlasTreeDiff.Empty;
        _highlightedClusterIconNodeIds = [];
        _searchIndex = BuildSearchIndex(spec.Tree.Nodes.Values);
        UpdateSearchResults(SearchText);
        RefreshAllocationSummary();
        OnPropertyChanged(nameof(Spec));
        OnPropertyChanged(nameof(Tree));
        OnPropertyChanged(nameof(Sprites));
        OnPropertyChanged(nameof(ImageResolver));
        CanvasChanged?.Invoke();
        StateChanged?.Invoke();
    }

    private void RebuildDiffVersionOptions()
    {
        DiffVersionOptions = [NoDiffVersion, .. VersionOptions.Where(version => version != Tree.Version)];
        OnPropertyChanged(nameof(DiffVersionOptions));
        OnPropertyChanged(nameof(HasDiffVersionOptions));
    }

    private void OnSpecChanged()
    {
        if (_hoverNodeId is { } id)
        {
            _hoverPath = _spec.HoverPathTo(id);
        }
        RefreshAllocationSummary();
        StateChanged?.Invoke();
        RequestRedraw();
    }

    private void RefreshAllocationSummary()
    {
        _aggregatedStatGroups = AtlasPassiveStatAggregator.AggregateGroups(Tree, _spec.AllocatedNodes);
        RefreshAggregatedSearchPresentation();
        OnPropertyChanged(nameof(HasAggregatedStats));
        OnPropertyChanged(nameof(AllocatedPointCount));
        OnPropertyChanged(nameof(PointLimit));
        OnPropertyChanged(nameof(IsPointLimitReached));
        OnPropertyChanged(nameof(PointUsage));
        OnPropertyChanged(nameof(PointUsageBrush));
        OnPropertyChanged(nameof(IsDirty));
    }

    private void RefreshAggregatedSearchPresentation()
    {
        var terms = SearchTerms(SearchText);
        AggregatedStatGroups = _aggregatedStatGroups
            .Select(group => new AtlasAggregatedStatGroupViewModel(
                group.Type,
                group.Stats
                    .Select(stat => new AtlasAggregatedStatItemViewModel(
                        stat.Text,
                        stat.SourceCount,
                        AggregatedStatOpacity(group.Type, stat, terms)))
                    .ToArray()))
            .ToArray();
        OnPropertyChanged(nameof(AggregatedStatGroups));
    }

    private double AggregatedStatOpacity(
        string groupType,
        AggregatedAtlasStat stat,
        IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return 1;
        }

        var sourceNames = stat.SourceNodeIds
            .Select(nodeId => Tree.Nodes.TryGetValue(nodeId, out var node) ? node.Name : string.Empty);
        var searchableText = string.Join('\n', new[] { groupType, stat.Text }.Concat(sourceNames));
        return terms.All(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase))
            ? 1
            : 0.25;
    }

    private void UpdateSearchResults(string searchText)
    {
        var terms = SearchTerms(searchText);
        _searchResultNodeIds = terms.Count == 0
            ? []
            : _searchIndex
                .Where(pair => terms.All(term => pair.Value.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(pair => pair.Key)
                .ToHashSet();
        OnPropertyChanged(nameof(SearchResultNodeIds));
        OnPropertyChanged(nameof(SearchResultCount));
    }

    private static IReadOnlyDictionary<int, string> BuildSearchIndex(IEnumerable<AtlasNode> nodes) =>
        nodes
            .Where(node => node.Type is not (AtlasNodeType.Start or AtlasNodeType.ClusterIcon))
            .ToDictionary(
                node => node.Id,
                node => string.Join('\n', new[] { node.Name, node.Type.ToString() }.Concat(node.Stats)));

    private static IReadOnlyList<string> SearchTerms(string searchText) =>
        searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool SameClusterCategory(AtlasNode source, AtlasNode candidate) =>
        !string.IsNullOrWhiteSpace(source.Icon)
            ? string.Equals(source.Icon, candidate.Icon, StringComparison.Ordinal)
            : string.Equals(source.Name, candidate.Name, StringComparison.OrdinalIgnoreCase);

    private void RequestRedraw()
    {
        VisualRevision++;
        RedrawRequested?.Invoke();
    }
}
