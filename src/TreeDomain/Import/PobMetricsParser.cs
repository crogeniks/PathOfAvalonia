using System.Xml.Linq;

namespace PathOfAvalonia.TreeDomain.Import;

internal static class PobMetricsParser
{
    public static ImportedBuildMetrics Parse(string xml)
    {
        var buildEl = PobXmlHelpers.TryExtractElement(xml, "Build");
        if (buildEl is null)
        {
            return ImportedBuildMetrics.Empty;
        }

        var playerStats = buildEl.Elements("PlayerStat")
            .Select(ParsePlayerStat)
            .Where(stat => !string.IsNullOrWhiteSpace(stat.Stat))
            .ToArray();
        var dpsRows = buildEl.Elements("FullDPSSkill")
            .Select(ParseFullDpsSkill)
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .ToArray();

        if (playerStats.Length == 0 && dpsRows.Length == 0)
        {
            return ImportedBuildMetrics.Empty;
        }

        return ImportedBuildMetrics.Empty with
        {
            Source = ImportedMetricSource.SavedXmlSnapshot,
            PlayerStats = playerStats,
            SkillDps = dpsRows,
        };
    }

    private static ImportedStatMetric ParsePlayerStat(XElement el)
    {
        var stat = PobXmlHelpers.AttrString(el, "stat") ?? string.Empty;
        var value = PobXmlHelpers.AttrString(el, "value") ?? el.Value.Trim();
        return new ImportedStatMetric(stat, PobXmlHelpers.StatLabel(stat), PobXmlHelpers.ParseDouble(value), value);
    }

    private static ImportedSkillDpsMetric ParseFullDpsSkill(XElement el)
    {
        var stat = PobXmlHelpers.AttrString(el, "stat") ?? PobXmlHelpers.AttrString(el, "name") ?? string.Empty;
        var value = PobXmlHelpers.AttrString(el, "value") ?? el.Value.Trim();
        return new ImportedSkillDpsMetric(
            PobXmlHelpers.StatLabel(stat),
            PobXmlHelpers.ParseDouble(value),
            value,
            PobXmlHelpers.AttrInt(el, "count") ?? 1,
            PobXmlHelpers.AttrString(el, "skillPart"),
            PobXmlHelpers.AttrString(el, "source"));
    }
}
