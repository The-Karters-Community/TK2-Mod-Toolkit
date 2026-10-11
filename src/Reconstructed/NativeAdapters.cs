using System;
using TK2.Reconstructed;

namespace TK2.Customization;

// Reviewed normal-object-state translations of the actual native bodies.
// IL2CPP runtime initialization/exception machinery stays in the generated bindings.
public static partial class ReadableGame
{
    // PixelGameKartCamera.InitOriginalProperties @ 0x1804eff80.
    public static void InitOriginalProperties(PixelGameKartCamera camera)
    {
        camera.fOriginalKartRottationY_SmoothDamp = camera.kartRotationY.fSmoothDampInTime;
        var far = camera.farPitchDownCam;
        camera.vOriginalFarPitchDownOffset = far.vCamPositionOffset;
        camera.fOriginalFarPitchDown = far.fCamPitchAngle;
        camera.fOriginalFarYaw = far.fCamYawAngle;
        camera.fOriginalFov = far.fCamFOV;
        var up = camera.slope_Up_CamAdd;
        camera.vOriginalFarPitchDownOffset_SlopeUp = up.vCamPositionOffset;
        camera.fOriginalFarPitchDown_slopeUp = up.fCamPitchAngle;
        var down = camera.slope_Down_CamAdd;
        camera.vOriginalFarPitchDownOffset_slopeDown = down.vCamPositionOffset;
        camera.fOriginalFarPitchDown_slopeDown = down.fCamPitchAngle;
        camera.fDistToMoveZ_OnVelChangeOriginal = camera.fDistToMoveZ_OnVelChange;
    }

    // Ant_BoostManager.GetCurrentBoostReservesTime @ 0x180667ce0.
    // The interop implicit conversion preserves the game's ObscuredFloat decoding.
    public static float GetCurrentBoostReservesTime(Ant_BoostManager boost) => boost.fBoostingReserves;

    // Ant_BoostManager.AddReserves @ 0x180665db0; native behavior is decoded addition then re-encoding.
    public static void AddReserves(Ant_BoostManager boost, float amount) =>
        boost.fBoostingReserves = BoostLogic.AddReserves(boost.fBoostingReserves, amount);

    // Ant_BoostManager.KeepReservesAtMinimusVal @ 0x1806682a0.
    // The native floor is applied only while eCurrentRaceState is nonzero.
    public static void KeepReservesAtMinimum(Ant_BoostManager boost, float minimum)
    {
        int raceState = (int)Ant_CurrentGameConfiguration.eCurrentRaceState;
        boost.fBoostingReserves = BoostLogic.KeepReservesAtMinimum(boost.fBoostingReserves, minimum, raceState);
    }

    // Ant_BoostManager.GetCurrentBoostMaxSpeedIncrease @ 0x180667b50.
    // ObscuredFloat fields are decoded by the interop conversion, matching the native getter.
    public static float GetCurrentBoostMaxSpeedIncrease(Ant_BoostManager boost) =>
        BoostLogic.GetCurrentBoostMaxSpeedIncrease(
            boost.fCurrentSpeedIncreaseFromTripleBoostReserves,
            boost.fCurrentContinuousBoostMaxSpeedIncrease,
            boost.fCurrentInstantBoostMaxSpeedIncrease,
            boost.fCurrentMaxSpeedIncreaseFromReserves_Preview,
            boost.fCurrentConstBoostingTimeLeft_PowerUpBooster,
            boost.fPowerUp_Booster_MaxSpeedIncrease,
            boost.fCurrentConstBoostingTimeLeft_PowerUpStar,
            boost.fPowerUp_Star_MaxSpeedIncrease,
            boost.fCurrentConstBoostingTimeFromBoostpad,
            boost.fMaxSpeedIncreaseForBoostPad);

    // Ant_BoostManager.GetCurrentBoostAccelMultiplier @ 0x1806679e0.
    // GetKartVelocity reads the controller Motor.BaseVelocity; native null paths throw.
    public static float GetCurrentBoostAccelMultiplier(Ant_BoostManager boost)
    {
        var controller = boost.kartController;
        var velocity = controller.GetKartVelocity();
        var forward = controller.kartPhysics.vForward_SteeringChar_AlignedToSurfacce_Normalized;
        return BoostLogic.GetCurrentBoostAccelMultiplier(
            forward.x, forward.y, forward.z,
            velocity.x, velocity.y, velocity.z,
            boost.fCurrentConstBoostingTimeFromBoostpad,
            boost.fMinimumSpeedForcedAfterEnteringBoostpad,
            boost.fAccelerationMultiplierAfterEnteringBoostpad);
    }

    // Ant_BoostManager.WhatZoneIsThePlayerIn @ 0x1806697c0.
    public static Ant_BoostManager.EBoostZone WhatZoneIsThePlayerIn(Ant_BoostManager boost, int boostIndex) =>
        (Ant_BoostManager.EBoostZone)BoostLogic.GetBoostZone(
            boostIndex,
            boost.fCurrentBoostFillTime,
            boost.fCurrentBoostFillTime_NotClampedTimeSinceLoading,
            boost.iCurrentBoosterNumberCount);

    // FireSliderBoost strength curve slice @ 0x180666590; this does not reproduce the enclosing state changes.
    public static float GetSliderBoostStrength(Ant_BoostManager boost, int boostIndex) =>
        BoostLogic.GetSliderBoostStrength(
            boost.fCurrentBoostFillTime[boostIndex],
            boost.fMinimumTimeForBoost,
            boost.fMaximumTimeForBestBoost,
            boost.fPowForBoostClickSliderNormalized);

    // Ant_BoostManager.BoostPadTriggerEnter @ 0x180665fa0.
    // The game-owned FireBoost method remains responsible for its normal state/event behavior.
    public static bool BoostPadTriggerEnter(Ant_BoostManager boost, float strength, float length,
        bool resetInstantVelocityBeforeFiring)
    {
        float currentTime = resetInstantVelocityBeforeFiring ? 0f : UnityEngine.Time.time;
        float lastTriggerTime = resetInstantVelocityBeforeFiring ? 0f : boost._fLastBoostPadTriggerTime;
        var plan = BoostLogic.PlanBoostPadTrigger(
            resetInstantVelocityBeforeFiring,
            currentTime,
            lastTriggerTime,
            boost.fMinTimeBetweenSameBoostPadTrigger,
            boost.fCurrentConstBoostingTimeFromBoostpad,
            strength,
            length,
            boost.fDefaultBoostPadLengthTime,
            boost.fDefaultBoostPadInstantVelAfterEntering);

        if (!resetInstantVelocityBeforeFiring)
            boost._fLastBoostPadTriggerTime = plan.LastTriggerTime;
        if (!plan.ShouldFire) return false;

        boost.fCurrentConstBoostingTimeFromBoostpad = plan.BoostPadTime;
        if (plan.ResetInstantVelocity)
            boost.kartController.kartPhysics.ResetInstantBoostVelocity();
        boost.FireBoost(Ant_BoostManager.EBoostType.EBOOST_BOOSTPAD, 0f,
            plan.FireInstantVelocity, plan.FireLength);
        return true;
    }

    // Ant_BoostManager.StopBoost @ 0x180669140.
    public static void StopBoost(Ant_BoostManager boost)
    {
        var plan = BoostLogic.PlanStopBoost(boost.fCurrentBoostFillTime);
        boost.fBoostingReserves = plan.BoostReserves;
        boost.fTripleBoostSpeedIncreaseReservesNormalized = plan.TripleBoostSpeedIncreaseReservesNormalized;
        Array.Copy(plan.FillTimes, boost.fCurrentBoostFillTime, plan.FillTimes.Length);
        boost.fCurrentAirTime = plan.CurrentAirTime;
        boost.iCurrentBoosterNumberCount = plan.BoosterCount;
    }

    // Ant_BoostManager.SetWheelBurningBoostFillTime @ 0x180668aa0.
    public static void SetWheelBurningBoostFillTime(Ant_BoostManager boost, int boxIndex, float loadedTime) =>
        BoostLogic.SetWheelBurningBoostFillTime(boost.fCurrentBoostFillTime, boxIndex, loadedTime);

    // HpBarController.GetCurrentHP shares 0x18056d2b0 with PlayerGlobalStats.GetCurrentHP.
    // The native getter reads synchronized visible HP, rather than private currentHp.
    public static int GetCurrentHP(HpBarController hp) => hp.player.visualInstanceSyncedParams.iPlayerHP;

    // HpBarController.ActivateImmunityNow @ 0x18056c150.
    public static void ActivateImmunityNow(HpBarController hp, EImmunitySource source, bool active)
    {
        hp._immunitySource = (EImmunitySource)HealthLogic.ChangeSource((uint)hp._immunitySource, (uint)source, active);
        RefreshImmunityState(hp);
    }

    // HpBarController.RefreshImmunityState @ 0x18056ee90.
    public static void RefreshImmunityState(HpBarController hp)
    {
        uint sources = (uint)hp._immunitySource;
        var player = hp.player;
        if (sources != 0 && player.playerLogicModCommandsReadOnly != null)
            player.playerLogicModCommandsReadOnly.StopFrozenWheelsEffect();
        hp.kartController.SetDeathGroundFriction_NoY(0);
        var visual = player.visualInstanceSyncedParams;
        visual.visualColliders.SetActive(HealthLogic.CollidersActive(sources));
        visual.iPlayerHP = hp.currentHp;
        visual.bIsImmune = HealthLogic.IsImmune(sources, sources == 0 && player.IsPlayerTransformedIntoGhostInGameMode());
        visual.bIsDeathImmune = HealthLogic.IsDeathImmune(sources);
        visual.eCurrentDamageVFX = hp.eCurrentContinousDamageVFX;
    }
}
