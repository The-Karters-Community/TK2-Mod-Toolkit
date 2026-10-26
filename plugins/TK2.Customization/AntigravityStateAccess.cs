using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    private static bool TryGetAttached(PixelKartPhysics physics, out KartState entry)
    {
        if (!CanReadState(physics)) { entry = null!; return false; }
        var controller = physics.kartController;
        if (controller != null) return TryGet(controller, out entry) && entry.Attached;
        entry = null!; return false;
    }

    private static bool CanReadState(PixelKartPhysics physics)
    {
        var plugin = Plugin.Instance;
        if (States.Count == 0 || plugin?.AntigravityEnabled.Value != true || physics == null) return false;
        var controller = physics.kartController;
        return controller != null && AirShiftFeature.Running(controller);
    }

    private static bool CanReadController(PixelEasyCharMoveKartController controller)
    {
        var plugin = Plugin.Instance;
        return States.Count != 0 && plugin?.AntigravityEnabled.Value == true && AirShiftFeature.Running(controller);
    }

    private static bool TryGet(PixelEasyCharMoveKartController controller, out KartState entry) =>
        States.TryGetValue(controller.GetInstanceID(), out entry!) && entry.KartSceneHandle == controller.gameObject.scene.handle;

    private static bool IsLocalHuman(PixelEasyCharMoveKartController controller) =>
        controller != null && controller.parentPlayer != null &&
        controller.parentPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL;

    private static void UpdateCameraEngagement(KartState entry, Vector3 up, float nativeSlopeLimit)
    {
        float worldTilt = Vector3.Angle(Vector3.up, up);
        float enter = Mathf.Clamp(nativeSlopeLimit - 8f, 15f, 75f);
        float exit = Mathf.Clamp(enter - 8f, 8f, enter);
        entry.CameraEngaged = entry.CameraEngaged
            ? worldTilt >= exit
            : worldTilt >= enter;
    }

    internal static bool TryGetActiveFrame(Ant_Player player, out Vector3 up, out Vector3 forward)
    {
        up = Vector3.zero;
        forward = Vector3.zero;
        var plugin = Plugin.Instance;
        if (player == null || plugin?.AntigravityEnabled.Value != true || !Plugin.OfflineLabAllowed || States.Count == 0)
            return false;
        foreach (var entry in States.Values)
        {
            if (entry.Controller == null || entry.Controller.parentPlayer != player ||
                !entry.Attached || !entry.CameraEngaged) continue;
            up = entry.Up;
            forward = entry.Forward;
            return up.sqrMagnitude > .9f && forward.sqrMagnitude > .9f;
        }
        return false;
    }

}
