using TK2.Reconstructed;
using UnityEngine;

namespace TK2.Customization;

// These adapters execute the reconstructed C# against actual interop objects.
// Editing the linked src/Reconstructed files changes the behavior compiled into the one pack DLL.
public static class ReadableGame
{
    public static void AddVelocity(PixelKartPhysics physics, Vector3 added)
    {
        Vector3 pending = physics.vFrameVelocityAddOverride;
        var result = KartLogic.AddVelocity(new(pending.x, pending.y, pending.z), new(added.x, added.y, added.z));
        physics.vFrameVelocityAddOverride = new Vector3(result.X, result.Y, result.Z);
    }

    public static void JumpInput(PixelKartPhysics physics, bool pressed)
    {
        if (!pressed) return;
        var controller = physics.kartController;
        var state = new JumpState
        {
            ExtraJumpAllowsTrick = physics.bExtraJumpEffectRunningAllowAlwaysTrick,
            PendingTrick = physics.bPendingTrickFromAcceptedJump,
            JumpedAt = physics.fPhysicsKartJumped_InUnityTime,
            TimeSinceNotGrounded = physics.fTimeSinceNotGrounded,
            GracePeriod = physics.fAllowJumpAfterPlayerMissesGroundForTime
        };
        KartLogic.JumpInput(state, pressed, Time.time, controller.Motor.GroundingStatus.FoundAnyGround,
            controller.IsPlayingReplayData(), controller._bReplayReceivedGrounedNow);
        physics.fPhysicsKartJumped_InUnityTime = state.JumpedAt;
        physics.bPendingTrickFromAcceptedJump = state.PendingTrick;
        physics.bPlayerJumpedWhenGrounedOrJustAfterMissedGround_RunJump = state.RunJump;
        physics.playerJumpInputClickedUnityTimeHistoryBeforeGrouned.Clear();
        foreach (float time in state.PressesBeforeGround) physics.playerJumpInputClickedUnityTimeHistoryBeforeGrouned.Add(time);
    }

    public static PixelSDK_Camera? GetCamera(PixelGameKartCamera camera)
    {
        var player = camera.orginalCameraNoSpecPlayer;
        int? index = CameraLogic.GetCameraIndex((int)player.ePlayerType, (int)player.eAntPlayerNr, camera.bIsSpectatorCamera);
        return index.HasValue ? PixelSDK.pixelSdkCamerasManager.playersCameras[index.Value] : null;
    }
}
