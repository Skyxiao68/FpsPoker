using UnityEngine;

public static class FpsAiDifficulty
{
    public const int MaxSkillLevel = 5;

    private static readonly string[] TierNames =
    {
        "GREENHORN",
        "DRIFTER",
        "GUNSLINGER",
        "OUTLAW",
        "LEGEND"
    };

    private static readonly string[] TierTraits =
    {
        "Slow draw, loose aim",
        "Quicker draw, steadier aim",
        "Pre-aims angles, aims high",
        "Fast draw, punishes repeat peeks",
        "Pinpoint aim, hunts you down"
    };

    private static readonly float[] SkillByLevel = { 0f, 0.25f, 0.45f, 0.6f, 0.8f, 1f };

    public static float SkillForLevel(int level)
    {
        int index = Mathf.Clamp(level, 1, SkillByLevel.Length) - 1;
        return SkillByLevel[index];
    }

    public static string TierName(int level)
    {
        return TierNames[TierIndex(level)];
    }

    public static string TierTrait(int level)
    {
        return TierTraits[TierIndex(level)];
    }

    private static int TierIndex(int level)
    {
        return Mathf.Clamp(level, 1, TierNames.Length) - 1;
    }

    public static FPSPersonality CreateForLevel(FPSPersonality source, int level)
    {
        FPSPersonality scaled = ScriptableObject.Instantiate(source);
        ApplySkill(scaled, source, SkillForLevel(level));
        return scaled;
    }

    public static void ApplySkill(FPSPersonality target, FPSPersonality source, float skill)
    {
        float s = Mathf.Clamp01(skill);

        target.reactionTime = Mathf.Lerp(source.reactionTime,
            source.reactionTime * 0.55f, s);
        target.aimError = Mathf.Lerp(source.aimError,
            source.aimError * 0.35f, s);
        target.fireAngleTolerance = Mathf.Lerp(source.fireAngleTolerance,
            Mathf.Max(Mathf.Min(1.5f, source.fireAngleTolerance),
                source.fireAngleTolerance * 0.4f), s);
        target.aimTurnSpeed = Mathf.Lerp(source.aimTurnSpeed,
            source.aimTurnSpeed * 1.35f, s);
        target.targetLeadAccuracy = Mathf.Lerp(source.targetLeadAccuracy,
            Mathf.Max(source.targetLeadAccuracy, 0.9f), s);
        target.recoilControl = Mathf.Lerp(source.recoilControl,
            Mathf.Max(source.recoilControl, 0.95f), s);
        target.headAimBias = Mathf.Lerp(source.headAimBias,
            Mathf.Max(source.headAimBias, 0.7f), s * s);

        target.gameSense = Mathf.Lerp(source.gameSense,
            Mathf.Max(source.gameSense, 1f), s);
        target.shotDiscipline = Mathf.Lerp(source.shotDiscipline,
            Mathf.Max(source.shotDiscipline, 1f), s);

        target.memoryDuration = Mathf.Lerp(source.memoryDuration,
            source.memoryDuration * 1.75f, s);
        target.fieldOfView = Mathf.Lerp(source.fieldOfView,
            Mathf.Max(source.fieldOfView, Mathf.Min(170f, source.fieldOfView + 35f)), s);

        target.strafeAmplitude = Mathf.Lerp(source.strafeAmplitude,
            Mathf.Max(source.strafeAmplitude, 0.95f), s);
        target.strafeFrequency = Mathf.Lerp(source.strafeFrequency,
            source.strafeFrequency * 1.6f, s);
        target.repathInterval = Mathf.Lerp(source.repathInterval,
            Mathf.Min(source.repathInterval, Mathf.Max(0.15f, source.repathInterval * 0.65f)), s);
    }
}
