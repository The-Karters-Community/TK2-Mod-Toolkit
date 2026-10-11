using System;

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

public enum BoostZone
{
    None = 0,
    Early = 1,
    Nice = 2,
    Great = 3,
    VeryGood = 4,
    Perfect = 5,
    Insane = 6
}

public readonly record struct BoostPadTriggerPlan(
    bool ShouldFire,
    bool ResetInstantVelocity,
    float LastTriggerTime,
    float BoostPadTime,
    float FireLength,
    float FireInstantVelocity);

public readonly record struct BoostStopPlan(
    float BoostReserves,
    float TripleBoostSpeedIncreaseReservesNormalized,
    float[] FillTimes,
    float CurrentAirTime,
    int BoosterCount);

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

    // AddReserves adds the supplied amount directly; it does not clamp or validate it.
    public static float AddReserves(float currentReserves, float amount) => currentReserves + amount;

    // KeepReservesAtMinimusVal applies its floor only when the global race-state value is nonzero.
    public static float KeepReservesAtMinimum(float currentReserves, float minimum, int raceState)
    {
        if (raceState != 0 && currentReserves < minimum) currentReserves = minimum;
        return currentReserves;
    }

    // GetCurrentBoostMaxSpeedIncrease sums four decoded accumulated bonuses and the
    // configured bonuses for each currently active power-up or boost pad.
    public static float GetCurrentBoostMaxSpeedIncrease(
        float tripleBoostReserveBonus,
        float continuousBoostBonus,
        float instantBoostBonus,
        float reservePreviewBonus,
        float boosterTimeRemaining,
        float boosterBonus,
        float starTimeRemaining,
        float starBonus,
        float boostPadTimeRemaining,
        float boostPadBonus) =>
        tripleBoostReserveBonus + continuousBoostBonus + instantBoostBonus + reservePreviewBonus
        + (boosterTimeRemaining > 0f ? boosterBonus : 0f)
        + 0f
        + (starTimeRemaining > 0f ? starBonus : 0f)
        + (boostPadTimeRemaining > 0f ? boostPadBonus : 0f);

    // GetCurrentBoostAccelMultiplier compares the magnitude of the velocity projected
    // onto the forward vector against the boost-pad minimum. Keep the native float
    // operation order before Math.Sqrt; the vector is expected to be normalized.
    public static float GetCurrentBoostAccelMultiplier(
        float forwardX, float forwardY, float forwardZ,
        float velocityX, float velocityY, float velocityZ,
        float boostPadTimeRemaining, float minimumForcedSpeed, float accelerationMultiplier)
    {
        float forwardDotVelocity = forwardY * velocityY + forwardX * velocityX + forwardZ * velocityZ;
        float forwardZComponent = forwardDotVelocity * forwardZ;
        double projectedSpeed = Math.Sqrt((double)(
            forwardDotVelocity * forwardX * forwardDotVelocity * forwardX
            + forwardDotVelocity * forwardY * forwardDotVelocity * forwardY
            + forwardZComponent * forwardZComponent));
        if (boostPadTimeRemaining > 0f && (float)projectedSpeed < minimumForcedSpeed)
            return accelerationMultiplier;
        return 1f;
    }

    // WhatZoneIsThePlayerIn. Callers supply the manager's three aligned boost slots
    // and a valid index; IL2CPP null/invalid-array logging paths are outside this model.
    public static BoostZone GetBoostZone(int boostIndex, float[] currentFillTimes,
        float[] timeSinceLoading, int currentBoosterCount)
    {
        int fullSlots = 0;
        int lowSlots = 0;
        foreach (float fillTime in currentFillTimes)
        {
            if (1f <= fillTime) fullSlots++;
            else if (!(0.1f < fillTime)) lowSlots++;
        }

        if (fullSlots == 1 && lowSlots == 2
            && (currentFillTimes[boostIndex] < 1f || currentBoosterCount < 3))
            return BoostZone.Perfect;

        for (int i = 0; i < currentFillTimes.Length; i++)
            if (i != boostIndex && 1f <= currentFillTimes[i])
                return BoostZone.Great;

        float elapsedFillTime = timeSinceLoading[boostIndex];
        if (elapsedFillTime <= 0.55f) return BoostZone.Early;
        if (0.94f < elapsedFillTime && elapsedFillTime <= 1.05f) return BoostZone.Perfect;
        return BoostZone.Great;
    }

    // Ant_BoostManager.BoostPadTriggerEnter. Diagnostic Debug.Log calls are omitted.
    public static BoostPadTriggerPlan PlanBoostPadTrigger(bool resetInstantVelocityBeforeFiring,
        float currentTime, float lastTriggerTime, float minimumSamePadInterval,
        float currentBoostPadTime, float strength, float length,
        float defaultBoostPadLengthTime, float defaultInstantVelocity)
    {
        if (!resetInstantVelocityBeforeFiring)
        {
            float newLastTriggerTime = currentTime;
            if (currentTime - lastTriggerTime < minimumSamePadInterval)
                return new(false, false, newLastTriggerTime, currentBoostPadTime, 0f, 0f);

            lastTriggerTime = newLastTriggerTime;
        }

        float fireLength = length * defaultBoostPadLengthTime;
        return new(true, resetInstantVelocityBeforeFiring, lastTriggerTime,
            currentBoostPadTime + fireLength, fireLength, strength * defaultInstantVelocity);
    }

    // Ant_BoostManager.StopBoost clears the active reserve, boost slots, air time and booster count.
    public static BoostStopPlan PlanStopBoost(float[] fillTimes)
    {
        float[] stoppedFillTimes = (float[])fillTimes.Clone();
        stoppedFillTimes[0] = 0f;
        stoppedFillTimes[1] = 0f;
        stoppedFillTimes[2] = 0f;
        return new(0f, 0f, stoppedFillTimes, 0f, 0);
    }

    // Ant_BoostManager.SetWheelBurningBoostFillTime writes the requested slot directly.
    public static void SetWheelBurningBoostFillTime(float[] fillTimes, int boxIndex, float loadedTime) =>
        fillTimes[boxIndex] = loadedTime;

    // IsManualBoostWithingFireRange uses slot zero and includes both boundaries.
    public static bool IsManualBoostWithinFireRange(float[] fillTimes, float minimumTime, float maximumTime)
    {
        float fillTime = fillTimes[0];
        if (fillTime < minimumTime) return false;
        return fillTime <= maximumTime;
    }

    // GetTriggeredBoostClickedOnBoostingRangeStrengthNormalized.
    // The upper clamp is a native float global measured as 1.0f in the matching binary.
    public static float GetTriggeredBoostStrengthNormalized(float[] fillTimes, int boostIndex,
        float minimumTime, float maximumBestBoostTime)
    {
        float normalized = (fillTimes[boostIndex] - minimumTime) / (maximumBestBoostTime - minimumTime);
        if (normalized < 0f) return 0f;
        if (1f < normalized) return 1f;
        return normalized;
    }

    // FireSliderBoost's strength slice: clamp the fill ratio, then apply the configured power curve.
    public static float GetSliderBoostStrength(float fillTime, float minimumTime,
        float maximumBestBoostTime, float exponent)
    {
        float normalized = (fillTime - minimumTime) / (maximumBestBoostTime - minimumTime);
        if (normalized < 0f) normalized = 0f;
        else if (1f < normalized) normalized = 1f;
        return MathF.Pow(normalized, exponent);
    }

    // IsPlayerAchivedMaximumBoostLevel uses equality, not a greater-than-or-equal check.
    public static bool HasMaximumManualBoostLevel(int boosterCount) => boosterCount == 3;

    // ForceReservesMinVal raises the reserve only when it is below the supplied floor.
    // currentReserves is a decoded ObscuredFloat value; callers own the native encoding.
    public static float ForceReservesMinimum(float currentReserves, float minimum)
    {
        if (currentReserves < minimum) currentReserves = minimum;
        return currentReserves;
    }

    // WillWallCollisionReduceReserves first requires positive reserves, then suppresses
    // the loss while the player's tank weapon is triggered or running.
    public static bool WillWallCollisionReduceReserves(float reserveTime, bool tankWeaponTriggeredOrRunning) =>
        reserveTime > 0f && !tankWeaponTriggeredOrRunning;

    // WallCollisionOccuredInTimeFromLastOne resets lateral velocity, applies the native
    // reserve multiplier unless the tank weapon is active, then invokes its event if present.
    public const float WallCollisionReserveMultiplier = 0.7f;
    public static void OnWallCollision(ref float loadedLateralVelocity, ref float reserveTime,
        bool tankWeaponTriggeredOrRunning, Action? onWallCollision)
    {
        loadedLateralVelocity = 0f;
        if (WillWallCollisionReduceReserves(reserveTime, tankWeaponTriggeredOrRunning))
            reserveTime *= WallCollisionReserveMultiplier;
        onWallCollision?.Invoke();
    }

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
