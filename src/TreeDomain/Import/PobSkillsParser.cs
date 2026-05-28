using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PathOfAvalonia.TreeDomain.Import;

internal static class PobSkillsParser
{
    public static ImportedSkills Parse(string xml)
    {
        var skillsEl = PobXmlHelpers.TryExtractElement(xml, "Skills");
        if (skillsEl is null)
        {
            return ImportedSkills.Empty;
        }

        var activeSetIndex = PobXmlHelpers.OneBasedToZero((int?)skillsEl.Attribute("activeSkillSet") ?? 1);
        var mainSocketGroup = PobXmlHelpers.OneBasedToZero(PobXmlHelpers.AttrInt(skillsEl, "mainSocketGroup") ?? ExtractBuildMainSocketGroup(xml) ?? 1);
        var skillSets = skillsEl.Elements("SkillSet").ToArray();
        if (skillSets.Length == 0)
        {
            var groups = ParseSkillGroups(skillsEl.Elements("Skill")).ToArray();
            return groups.Length == 0
                ? ImportedSkills.Empty
                : new ImportedSkills([new ImportedSkillSet(0, 0, "Skills", groups)], 0, mainSocketGroup);
        }

        var parsedSets = skillSets
            .Select((setEl, index) => new ImportedSkillSet(
                index,
                PobXmlHelpers.AttrInt(setEl, "id") ?? index + 1,
                SkillSetDisplayName(setEl, index),
                ParseSkillGroups(setEl.Elements("Skill")).ToArray()))
            .Where(set => set.Groups.Count > 0)
            .ToArray();

        if (parsedSets.Length == 0)
        {
            return ImportedSkills.Empty;
        }

        if (activeSetIndex < 0 || activeSetIndex >= parsedSets.Length)
        {
            activeSetIndex = 0;
        }
        return new ImportedSkills(parsedSets, activeSetIndex, mainSocketGroup);
    }

    private static IEnumerable<ImportedSkillGroup> ParseSkillGroups(IEnumerable<XElement> skillElements)
    {
        var index = 0;
        foreach (var skillEl in skillElements)
        {
            var gems = skillEl.Elements("Gem").Select(ParseGem).Where(gem => !string.IsNullOrWhiteSpace(gem.NameSpec)).ToArray();
            var label = SkillLabel(skillEl, gems, index);
            yield return new ImportedSkillGroup(
                index,
                label,
                PobXmlHelpers.AttrString(skillEl, "slot"),
                PobXmlHelpers.AttrString(skillEl, "source"),
                PobXmlHelpers.AttrBool(skillEl, "enabled", true),
                PobXmlHelpers.AttrBool(skillEl, "includeInFullDPS", false) || PobXmlHelpers.AttrBool(skillEl, "includeInFullDps", false),
                PobXmlHelpers.AttrInt(skillEl, "count") ?? PobXmlHelpers.AttrInt(skillEl, "groupCount") ?? 1,
                PobXmlHelpers.OneBasedToZero(PobXmlHelpers.AttrInt(skillEl, "mainActiveSkill") ?? PobXmlHelpers.AttrInt(skillEl, "mainActiveSkillIndex") ?? 1),
                PobXmlHelpers.OneBasedToZero(PobXmlHelpers.AttrInt(skillEl, "mainActiveSkillCalcs") ?? PobXmlHelpers.AttrInt(skillEl, "mainActiveSkillCalcsIndex") ?? 1),
                gems);
            index++;
        }
    }

    private static ImportedGem ParseGem(XElement gemEl) =>
        new(
            PobXmlHelpers.AttrString(gemEl, "nameSpec") ?? PobXmlHelpers.AttrString(gemEl, "name") ?? string.Empty,
            PobXmlHelpers.AttrString(gemEl, "gemId"),
            PobXmlHelpers.AttrString(gemEl, "skillId"),
            PobXmlHelpers.AttrString(gemEl, "variantId"),
            PobXmlHelpers.AttrInt(gemEl, "level"),
            PobXmlHelpers.AttrInt(gemEl, "quality"),
            PobXmlHelpers.AttrBool(gemEl, "enabled", true),
            PobXmlHelpers.AttrBool(gemEl, "enableGlobal1", false),
            PobXmlHelpers.AttrBool(gemEl, "enableGlobal2", false),
            PobXmlHelpers.AttrInt(gemEl, "count") ?? 1,
            PobXmlHelpers.AttrInt(gemEl, "skillPart"),
            PobXmlHelpers.AttrInt(gemEl, "skillPartCalcs"),
            PobXmlHelpers.AttrInt(gemEl, "skillStageCount"),
            PobXmlHelpers.AttrInt(gemEl, "skillStageCountCalcs"),
            PobXmlHelpers.AttrInt(gemEl, "skillMineCount"),
            PobXmlHelpers.AttrInt(gemEl, "skillMineCountCalcs"),
            PobXmlHelpers.AttrString(gemEl, "skillMinion"),
            PobXmlHelpers.AttrString(gemEl, "skillMinionCalcs"),
            PobXmlHelpers.AttrInt(gemEl, "skillMinionItemSet"),
            PobXmlHelpers.AttrInt(gemEl, "skillMinionItemSetCalcs"),
            PobXmlHelpers.AttrInt(gemEl, "skillMinionSkill"),
            PobXmlHelpers.AttrInt(gemEl, "skillMinionSkillCalcs"));

    private static string SkillSetDisplayName(XElement element, int index) =>
        PobXmlHelpers.DisplayName(element, "Skill Set", index);

    private static string SkillLabel(XElement skillEl, IReadOnlyList<ImportedGem> gems, int index)
    {
        var label = PobXmlHelpers.AttrString(skillEl, "label")
            ?? PobXmlHelpers.AttrString(skillEl, "title")
            ?? PobXmlHelpers.AttrString(skillEl, "name");
        if (!string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        var activeGem = gems.FirstOrDefault(gem => gem.Enabled && !string.IsNullOrWhiteSpace(gem.NameSpec))
            ?? gems.FirstOrDefault(gem => !string.IsNullOrWhiteSpace(gem.NameSpec));
        return activeGem is not null ? activeGem.NameSpec : $"Skill Group {index + 1}";
    }

    private static int? ExtractBuildMainSocketGroup(string xml)
    {
        var m = Regex.Match(xml, "<Build\\b[^>]*\\bmainSocketGroup=\"(\\d+)\"", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var value) ? value : null;
    }
}
