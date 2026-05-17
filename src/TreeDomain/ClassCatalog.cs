namespace PathOfAvalonia.TreeDomain;

public sealed class ClassCatalog
{
    public required IReadOnlyList<CharacterClassInfo> Classes { get; init; }

    public IReadOnlyList<string> ClassNames => Classes.Select(c => c.Name).ToArray();

    public IReadOnlyList<string> AscendancyNames(int classIndex) =>
        TryGetClass(classIndex, out var cls)
            ? cls.Ascendancies.Select(a => a.DisplayName).ToArray()
            : Classes[0].Ascendancies.Select(a => a.DisplayName).ToArray();

    public string? AscendancyTreeName(int classIndex, int ascendancyIndex)
    {
        if (!TryGetClass(classIndex, out var cls)
            || ascendancyIndex <= 0
            || ascendancyIndex >= cls.Ascendancies.Count)
        {
            return null;
        }
        return cls.Ascendancies[ascendancyIndex].TreeName;
    }

    public int ResolveAscendancyIndex(int classIndex, int importedAscendancyId)
    {
        if (!TryGetClass(classIndex, out var cls))
        {
            return 0;
        }
        return importedAscendancyId >= 0 && importedAscendancyId < cls.Ascendancies.Count
            ? importedAscendancyId
            : 0;
    }

    public int ResolveClassIndexFromImportedId(int importedClassId)
    {
        foreach (var cls in Classes)
        {
            if (cls.ExternalIntegerId == importedClassId || cls.ClassIndex == importedClassId)
            {
                return cls.ClassIndex;
            }
        }
        return 0;
    }

    private bool TryGetClass(int classIndex, out CharacterClassInfo cls)
    {
        foreach (var candidate in Classes)
        {
            if (candidate.ClassIndex == classIndex)
            {
                cls = candidate;
                return true;
            }
        }
        cls = Classes[0];
        return false;
    }

    public static ClassCatalog CreatePoe1() => new()
    {
        Classes = new[]
        {
            Class(0, "Scion", ("Ascendant", "Ascendant"), ("Scavenger", "Reliquarian")),
            Class(1, "Marauder", ("Juggernaut", "Juggernaut"), ("Berserker", "Berserker"), ("Chieftain", "Chieftain")),
            Class(2, "Ranger", ("Deadeye", "Deadeye"), ("Raider", "Raider"), ("Pathfinder", "Pathfinder"), ("Warden", "Warden")),
            Class(3, "Witch", ("Necromancer", "Necromancer"), ("Occultist", "Occultist"), ("Elementalist", "Elementalist")),
            Class(4, "Duelist", ("Slayer", "Slayer"), ("Gladiator", "Gladiator"), ("Champion", "Champion")),
            Class(5, "Templar", ("Inquisitor", "Inquisitor"), ("Hierophant", "Hierophant"), ("Guardian", "Guardian")),
            Class(6, "Shadow", ("Assassin", "Assassin"), ("Trickster", "Trickster"), ("Saboteur", "Saboteur")),
        },
    };

    private static CharacterClassInfo Class(int index, string name, params (string DisplayName, string TreeName)[] ascendancies)
    {
        var list = new List<AscendancyInfo>
        {
            new(0, "None", string.Empty, null),
        };
        for (var i = 0; i < ascendancies.Length; i++)
        {
            var a = ascendancies[i];
            list.Add(new AscendancyInfo(i + 1, a.DisplayName, a.TreeName, null));
        }
        return new CharacterClassInfo(index, index, name, list);
    }
}

public sealed record CharacterClassInfo(
    int ClassIndex,
    int? ExternalIntegerId,
    string Name,
    IReadOnlyList<AscendancyInfo> Ascendancies);

public sealed record AscendancyInfo(
    int AscendancyIndex,
    string DisplayName,
    string TreeName,
    string? InternalId);

