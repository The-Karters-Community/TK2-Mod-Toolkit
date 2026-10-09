namespace TK2.Reconstructed;

// Values observed in Ant_BoostManager.EBoostType metadata for game build 0.1.4.18.
public enum BoostKind
{
    ManualOne = 0,
    ManualTwo = 1,
    ManualThree = 2,
    BoostPad = 3,
    JumpLanding = 4,
    BoosterPowerUp = 5,
    StarAura = 6
}

/// <summary>
/// Readable normal-state translations of small Ant_BoostManager methods.
/// ObscuredFloat inputs here are already decoded values; IL2CPP initialization,
/// exception helpers, and Unity object lifecycle are intentionally not modeled.
/// </summary>
public static class BoostLogic
{
    // IsBoostingConstFromBoostpad: the boost-pad timer is active strictly above zero.
    public static bool IsBoostingFromBoostPad(float timeRemaining) => timeRemaining > 0f;

    // IsBoostingConstFromReserves: the decoded reserve amount is active strictly above zero.
    public static bool IsBoostingFromReserves(float reserveTime) => reserveTime > 0f;

    // IsBoostingFromPowerUp_Booster.
    public static bool IsBoostingFromBooster(float timeRemaining) => timeRemaining > 0f;

    // IsBoostingFromPowerUp_Star.
    public static bool IsBoostingFromStar(float timeRemaining) => timeRemaining > 0f;

    // IsBoosting: the native aggregate tests reserves, the just-triggered flag,
    // boost-pad time, and Booster time. The separate Star timer is not queried here.
    public static bool IsBoosting(float reserveTime, bool justBoostTriggered,
        float boostPadTimeRemaining, float boosterTimeRemaining) =>
        reserveTime > 0f || justBoostTriggered || boostPadTimeRemaining > 0f || boosterTimeRemaining > 0f;

    // GetManualBoostFillNormalized returns the requested array slot without normalization work.
    public static float GetManualBoostFillNormalized(float[] fillTimes, int boostIndex) => fillTimes[boostIndex];

    // IsManualBoostWithingFireRange uses slot zero and includes both boundaries.
    public static bool IsManualBoostWithinFireRange(float[] fillTimes, float minimumTime, float maximumTime)
    {
        float fillTime = fillTimes[0];
        if (fillTime < minimumTime) return false;
        return fillTime <= maximumTime;
    }

    // IsPlayerAchivedMaximumBoostLevel uses equality, not a greater-than-or-equal check.
    public static bool HasMaximumManualBoostLevel(int boosterCount) => boosterCount == 3;

    // IsPhysicalManualBoostEnabled_OnlyAICanDisable, for valid game boost enum values.
    // Native player type value 2 and the debug-forced-human flag enter the AI restriction branch.
    public static bool IsPhysicalManualBoostEnabled(int playerType, bool debugHumanControllableByAi,
        bool aiManualBoostPhysicsDisabled, BoostKind boostType)
    {
        if ((playerType == 2 || debugHumanControllableByAi) && aiManualBoostPhysicsDisabled)
            return (uint)boostType > 1u && boostType != BoostKind.ManualThree;
        return true;
    }

    // KartIsBreakingOnGround is a direct field assignment.
    public static void SetBreakingOnGround(ref bool current, bool isBreaking) => current = isBreaking;
}
