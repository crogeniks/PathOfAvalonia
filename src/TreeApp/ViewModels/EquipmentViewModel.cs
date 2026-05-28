using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.ViewModels;

public partial class EquipmentViewModel : ObservableObject
{
    private IReadOnlyList<ImportedSkillSet> _importedSkillSets = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial ObservableCollection<ItemGroupViewModel> Groups { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMetrics))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial ImportedBuildMetricsViewModel? Metrics { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkillGroups))]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    public partial ObservableCollection<ImportedSkillGroupViewModel> SkillGroups { get; set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkillSetVariants))]
    public partial ObservableCollection<ImportedSkillSetOptionViewModel> SkillSetOptions { get; set; } = new();

    [ObservableProperty]
    public partial int SelectedSkillSetIndex { get; set; }

    [ObservableProperty]
    public partial string EmptyMessage { get; set; } = "Import a build to see equipment.";

    public bool HasItems => Groups.Any(group => group.Items.Count > 0);
    public bool HasMetrics => Metrics is not null;
    public bool HasSkillGroups => SkillGroups.Count > 0;
    public bool HasContent => HasItems || HasMetrics || HasSkillGroups;
    public bool HasSkillSetVariants => SkillSetOptions.Count > 1;

    public void LoadBuild(ImportedBuild build)
    {
        Metrics = build.Metrics.Source != ImportedMetricSource.None || !string.IsNullOrWhiteSpace(build.Metrics.ErrorMessage)
            ? ImportedBuildMetricsViewModel.FromImported(build.Metrics)
            : null;
        _importedSkillSets = build.Skills.SkillSets;
        SkillSetOptions = new ObservableCollection<ImportedSkillSetOptionViewModel>(
            _importedSkillSets.Select(set => new ImportedSkillSetOptionViewModel(set.Index, set.DisplayName)));
        SelectedSkillSetIndex = build.Skills.ActiveSkillSetIndex >= 0 && build.Skills.ActiveSkillSetIndex < _importedSkillSets.Count
            ? build.Skills.ActiveSkillSetIndex
            : 0;
        RefreshSkillGroups();

        var groups = new List<ItemGroupViewModel>();
        var activeItems = build.Items.ToArray();

        var equipment = activeItems.Where(item => GroupName(item) == "Equipment").ToArray();
        if (equipment.Length > 0)
        {
            groups.Add(new ItemGroupViewModel(
                "Equipment",
                equipment.Select(item => ItemViewModel.FromImported(item))));
        }

        var flaskCharmItems = activeItems.Where(item => GroupName(item) == "Flasks & Charms").ToArray();
        if (flaskCharmItems.Length > 0)
        {
            groups.Add(new ItemGroupViewModel(
                "Flasks & Charms",
                flaskCharmItems.Select(item => ItemViewModel.FromImported(item))));
        }

        var jewelItems = activeItems
            .Where(item => GroupName(item) == "Jewels")
            .Select(item => ItemViewModel.FromImported(item))
            .ToArray();
        if (jewelItems.Length > 0)
        {
            groups.Add(new ItemGroupViewModel("Jewels", jewelItems));
        }

        var socketedJewels = SocketedJewels(build, activeItems.Select(item => item.Id).ToHashSet()).ToArray();
        if (socketedJewels.Length > 0)
        {
            groups.Add(new ItemGroupViewModel("Socketed Tree Jewels", socketedJewels));
        }

        if (groups.Count == 0)
        {
            EmptyMessage = "Import a build to view equipment.";
            Groups = new ObservableCollection<ItemGroupViewModel>();
            return;
        }
        EmptyMessage = string.Empty;
        Groups = new ObservableCollection<ItemGroupViewModel>(groups);
    }

    public void Clear()
    {
        Groups = new ObservableCollection<ItemGroupViewModel>();
        Metrics = null;
        _importedSkillSets = [];
        SkillSetOptions = new ObservableCollection<ImportedSkillSetOptionViewModel>();
        SelectedSkillSetIndex = 0;
        SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>();
        EmptyMessage = "Import a build to view equipment.";
    }

    public void MarkUnsupported()
    {
        Groups = new ObservableCollection<ItemGroupViewModel>();
        Metrics = null;
        _importedSkillSets = [];
        SkillSetOptions = new ObservableCollection<ImportedSkillSetOptionViewModel>();
        SelectedSkillSetIndex = 0;
        SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>();
        EmptyMessage = "Equipment is not available for this game yet.";
    }

    partial void OnSelectedSkillSetIndexChanged(int value) => RefreshSkillGroups();

    private void RefreshSkillGroups()
    {
        if (SelectedSkillSetIndex < 0 || SelectedSkillSetIndex >= _importedSkillSets.Count)
        {
            SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>();
            return;
        }

        var set = _importedSkillSets[SelectedSkillSetIndex];
        SkillGroups = new ObservableCollection<ImportedSkillGroupViewModel>(
            set.Groups.Select(group => ImportedSkillGroupViewModel.FromImported(set, group)));
    }

    private static IEnumerable<ItemViewModel> SocketedJewels(ImportedBuild build, HashSet<int> activeItemIds)
    {
        var seen = new HashSet<int>();
        foreach (var socketed in build.SocketedJewels.OrderBy(jewel => jewel.SocketNodeId))
        {
            if (activeItemIds.Contains(socketed.ItemId)
                || !seen.Add(socketed.ItemId)
                || !build.ItemsById.TryGetValue(socketed.ItemId, out var item))
            {
                continue;
            }

            yield return ItemViewModel.FromImported(item, $"Jewel {socketed.SocketNodeId}");
        }
    }

    private static string GroupName(ImportedItem item)
    {
        if (item.Slot.Contains("Flask", StringComparison.OrdinalIgnoreCase)
            || item.Slot.Contains("Charm", StringComparison.OrdinalIgnoreCase)
            || item.BaseType.Contains("Charm", StringComparison.OrdinalIgnoreCase))
        {
            return "Flasks & Charms";
        }

        if (item.Slot.Contains("Jewel", StringComparison.OrdinalIgnoreCase)
            || item.BaseType.Contains("Jewel", StringComparison.OrdinalIgnoreCase))
        {
            return "Jewels";
        }

        return "Equipment";
    }
}

public sealed class ItemGroupViewModel
{
    public string Header { get; }
    public ObservableCollection<ItemViewModel> Items { get; }

    public ItemGroupViewModel(string header, IEnumerable<ItemViewModel> items)
    {
        Header = header;
        Items = new ObservableCollection<ItemViewModel>(items);
    }
}

public sealed record ImportedSkillSetOptionViewModel(int Index, string DisplayName);

public sealed class ImportedBuildMetricsViewModel
{
    public string SourceText { get; }
    public string BackendText { get; }
    public string ErrorMessage { get; }
    public ObservableCollection<ImportedStatMetricViewModel> PlayerStats { get; }
    public ObservableCollection<ImportedSkillDpsMetricViewModel> SkillDps { get; }
    public ObservableCollection<string> Warnings { get; }
    public bool HasBackend => !string.IsNullOrWhiteSpace(BackendText);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasPlayerStats => PlayerStats.Count > 0;
    public bool HasSkillDps => SkillDps.Count > 0;

    private ImportedBuildMetricsViewModel(ImportedBuildMetrics metrics)
    {
        SourceText = metrics.Source switch
        {
            ImportedMetricSource.PobBackend => "DPS source: PoB backend",
            ImportedMetricSource.SavedXmlSnapshot => "DPS source: Saved snapshot",
            _ => "DPS source: Unavailable",
        };
        BackendText = string.Join(" ", new[] { metrics.BackendName, metrics.BackendVersion, metrics.BackendPath }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        ErrorMessage = metrics.ErrorMessage ?? string.Empty;
        PlayerStats = new ObservableCollection<ImportedStatMetricViewModel>(
            metrics.PlayerStats
                .Where(IsKeyStat)
                .Select(stat => new ImportedStatMetricViewModel(stat.Label, stat.DisplayValue)));
        SkillDps = new ObservableCollection<ImportedSkillDpsMetricViewModel>(
            metrics.SkillDps.Select(ImportedSkillDpsMetricViewModel.FromImported));
        Warnings = new ObservableCollection<string>(metrics.Warnings);
    }

    public static ImportedBuildMetricsViewModel FromImported(ImportedBuildMetrics metrics) => new(metrics);

    private static bool IsKeyStat(ImportedStatMetric stat)
    {
        var normalized = stat.Stat.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalized.Equals("FullDPS", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("TotalDPS", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("CombinedDPS", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Life", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("EnergyShield", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Armour", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Evasion", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("FireResist", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ColdResist", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("LightningResist", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ChaosResist", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ImportedStatMetricViewModel(string Label, string Value);

public sealed class ImportedSkillDpsMetricViewModel
{
    public string Name { get; }
    public string Count { get; }
    public string Dps { get; }
    public string Detail { get; }
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    private ImportedSkillDpsMetricViewModel(ImportedSkillDpsMetric metric)
    {
        Name = metric.Name;
        Count = metric.Count.ToString();
        Dps = metric.DisplayDps;
        Detail = string.Join(" / ", new[] { metric.SkillPart, metric.Source }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    public static ImportedSkillDpsMetricViewModel FromImported(ImportedSkillDpsMetric metric) => new(metric);
}

public sealed class ImportedSkillGroupViewModel
{
    public string Header { get; }
    public string Metadata { get; }
    public ObservableCollection<ImportedGemViewModel> Gems { get; }
    public bool HasMetadata => !string.IsNullOrWhiteSpace(Metadata);

    private ImportedSkillGroupViewModel(ImportedSkillSet set, ImportedSkillGroup group)
    {
        Header = group.Label;
        var parts = new List<string> { set.DisplayName };
        if (!string.IsNullOrWhiteSpace(group.Slot))
        {
            parts.Add(group.Slot);
        }
        parts.Add(group.Enabled ? "enabled" : "disabled");
        if (group.IncludeInFullDps)
        {
            parts.Add("FullDPS");
        }
        if (group.GroupCount != 1)
        {
            parts.Add($"x{group.GroupCount}");
        }
        Metadata = string.Join(" · ", parts);
        Gems = new ObservableCollection<ImportedGemViewModel>(group.Gems.Select(ImportedGemViewModel.FromImported));
    }

    public static ImportedSkillGroupViewModel FromImported(ImportedSkillSet set, ImportedSkillGroup group) => new(set, group);
}

public sealed class ImportedGemViewModel
{
    public string Name { get; }
    public string Metadata { get; }
    public bool IsDisabled { get; }
    public bool HasMetadata => !string.IsNullOrWhiteSpace(Metadata);

    private ImportedGemViewModel(ImportedGem gem)
    {
        Name = gem.NameSpec;
        IsDisabled = !gem.Enabled;
        var parts = new List<string>();
        if (gem.Level is { } level)
        {
            parts.Add($"lvl {level}");
        }
        if (gem.Quality is { } quality)
        {
            parts.Add($"{quality}%");
        }
        if (gem.Count != 1)
        {
            parts.Add($"x{gem.Count}");
        }
        if (!gem.Enabled)
        {
            parts.Add("disabled");
        }
        Metadata = string.Join(" · ", parts);
    }

    public static ImportedGemViewModel FromImported(ImportedGem gem) => new(gem);
}
