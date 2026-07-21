namespace PathOfAvalonia.TreeDomain;

public readonly record struct PassivePointUsage(
    int Total,
    int WeaponSet1,
    int WeaponSet2);

/// <summary>
/// Estimates the minimum campaign level needed for an allocated passive tree.
/// This mirrors Path of Building's EstimatePlayerProgress act/quest-point model.
/// </summary>
public static class CharacterProgression
{
    private static readonly ActProgress[] Poe1Acts =
    [
        new(1, 0),
        new(12, 2),
        new(22, 4),
        new(32, 6),
        new(40, 7),
        new(44, 9),
        new(50, 12),
        new(54, 15),
        new(60, 18),
        new(64, 20),
        new(67, 23),
    ];

    // Derived from PoE2 Data/QuestRewards using the same cumulative per-act
    // construction as Modules/Build.lua. Quest rewards grant paired weapon-set
    // points, which are handled separately below.
    private static readonly ActProgress[] Poe2Acts =
    [
        new(1, 0),
        new(12, 4),
        new(28, 8),
        new(44, 12),
        new(51, 16),
        new(64, 22),
        new(62, 24),
    ];

    public static int MinimumLevel(GameId gameId, PassivePointUsage usage)
    {
        var pointsRequiredFromLevels = Math.Max(usage.Total, 0);
        var acts = Poe1Acts;
        if (gameId == GameId.PathOfExile2)
        {
            pointsRequiredFromLevels -= Math.Min(
                Math.Max(usage.WeaponSet1, 0),
                Math.Max(usage.WeaponSet2, 0));
            acts = Poe2Acts;
        }

        for (var index = 0; index < acts.Length; index++)
        {
            var act = acts[index];
            var level = Math.Min(
                Math.Max(pointsRequiredFromLevels + 1 - act.QuestPoints, act.MinimumLevel),
                100);
            if (index == acts.Length - 1 || level <= acts[index + 1].MinimumLevel)
            {
                return level;
            }
        }

        return 100;
    }

    private readonly record struct ActProgress(int MinimumLevel, int QuestPoints);
}
