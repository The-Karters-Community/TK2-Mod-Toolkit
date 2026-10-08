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
