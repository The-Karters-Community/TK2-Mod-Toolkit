using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// Hold jump while airborne: once a trick has been made, the kart glides until
// jump is released or the kart lands. Input capture follows the proven Air Dash
// and Mid-air drift pattern: Ant_KartInput is captured in its own update, and
// the held state is read live inside the physics velocity step.
internal static class AirGlideFeature
{
    private static ConfigEntry<bool> Enabled = null!, ShowStatus = null!;
    private static ConfigEntry<float> DescentRate = null!, DescentAccel = null!, MomentumDecay = null!;

    private sealed class Glide
    {
        internal bool Gliding, TrickSeen, ParachuteOut;
        internal float GlideTime, VerticalSpeed;
        internal Ant_KartInput? Controls;
        internal GameObject? Parachute;
    }

    private static readonly Dictionary<int, Glide> States = new();
    private static string Status = "Air Glide: hold jump and do a trick to glide";

    internal static void Install(Plugin p)
    {
        Enabled = p.Config.Bind("AirGlide", "Enabled", false, "Offline controlled glide. Hold jump while airborne after a trick to deploy the parachute and glide; release jump to drop. Works in offline mode only.");
        ShowStatus = p.Config.Bind("AirGlide", "ShowStatus", true, "Show glide status while testing.");
        DescentRate = p.Config.Bind("AirGlide", "DescentRate", 6f, "Initial vertical descent speed in metres per second when gliding starts.");
        DescentAccel = p.Config.Bind("AirGlide", "DescentAccel", 4f, "How much the descent speed increases per second while gliding.");
        MomentumDecay = p.Config.Bind("AirGlide", "MomentumDecay", 0.02f, "Fraction of horizontal speed lost per second while gliding. 0 preserves momentum fully.");
        FeatureHook.Install(p.Harmony, typeof(AirGlideFeature), Hooks());
    }

    internal static FeatureHook[] Hooks() => new[] {
        new FeatureHook(typeof(Ant_KartInput), nameof(Ant_KartInput.ProcessRacingInput), Type.EmptyTypes, Postfix: nameof(InputPostfix)),
        new FeatureHook(typeof(PixelKartPhysics), nameof(PixelKartPhysics.UpdateVelocity),
            new[] { typeof(Vector3).MakeByRefType(), typeof(float) }, Postfix: nameof(VelocityPostfix)),
        new FeatureHook(typeof(PlayerRaceLogic), nameof(PlayerRaceLogic.OnRaceResetted), Type.EmptyTypes, Postfix: nameof(Reset)),
    };

    private static Glide State(PixelKartPhysics physics)
    {
        int id = physics.kartController.parentPlayer.GetInstanceID();
        if (!States.TryGetValue(id, out var glide)) States[id] = glide = new Glide();
        return glide;
    }

    // Ant_KartInput.player is a Rewired player; "Jump" is the game's own action.
    private static bool JumpHeld(Glide glide)
    {
        var player = glide.Controls?.player;
        if (player == null) return false;
        try { return player.GetButton("Jump"); }
        catch (Exception ex) { throw new InvalidOperationException("The game's Rewired Jump action is unavailable: " + ex.Message); }
    }

    private static void InputPostfix(Ant_KartInput __instance)
    {
        if (!Enabled.Value) return;
        try {
            var kart = __instance.boostManager?.kartController;
            if (kart?.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            State(kart.kartPhysics).Controls = __instance;
        } catch (Exception ex) { Fail(ex); }
    }

    private static void VelocityPostfix(PixelKartPhysics __instance, ref Vector3 __0, float __1)
    {
        if (!Enabled.Value || !Plugin.OfflineLabAllowed) return;
        try {
            if (__instance.kartController.parentPlayer?.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            var glide = State(__instance);

            // Back on the ground: clear everything for the next jump.
            if (__instance.fTimeSinceNotGrounded <= 0f) {
                if (glide.Gliding) StopGlide(glide, deploy: true);
                glide.TrickSeen = false;
                Status = "Air Glide: airborne, hold jump and do a trick to glide";
                return;
            }

            // Releasing jump cancels the glide and lets the kart fall normally.
            if (!JumpHeld(glide)) {
                if (glide.Gliding) StopGlide(glide, deploy: true);
                Status = glide.TrickSeen
                    ? "Air Glide: jump released - normal fall"
                    : "Air Glide: airborne, hold jump and do a trick to glide";
                return;
            }

            // A completed trick is what arms the glide, so a plain held jump never glides.
            if (!glide.TrickSeen)
                glide.TrickSeen = __instance.bAlreadyMadeTrick || __instance.bIsPlayerMadeTrickBeforeLanding;
            if (!glide.TrickSeen) return;

            if (!glide.Gliding) {
                glide.Gliding = true;
                glide.GlideTime = 0f;
                glide.VerticalSpeed = 0f;
                Plugin.Instance!.SessionModified = true;
                Plugin.Instance.Log.LogInfo("Air Glide: parachute deployed after a trick with jump held.");
            }

            glide.GlideTime += __1;
            float retention = 1f - MomentumDecay.Value * __1;
            __0.x *= retention;
            __0.z *= retention;
            glide.VerticalSpeed += DescentAccel.Value * __1;
            float descent = DescentRate.Value + glide.VerticalSpeed;
            __0.y = -descent;
            __instance.vLastVelocity = __0;

            if (!glide.ParachuteOut) DeployParachute(__instance, glide, true);
            Status = $"Air Glide: gliding - {glide.GlideTime:0.0}s, descent {descent:0.0} m/s";
        } catch (Exception ex) { Fail(ex); }
    }

    private static void StopGlide(Glide glide, bool deploy)
    {
        glide.Gliding = false;
        glide.GlideTime = 0f;
        glide.VerticalSpeed = 0f;
        if (deploy && glide.ParachuteOut) {
            glide.Parachute?.SetActive(false);
            glide.ParachuteOut = false;
        }
    }

    private static void DeployParachute(PixelKartPhysics physics, Glide glide, bool show)
    {
        try {
            var visual = physics.kartController.parentPlayer?.kartVisualTransform;
            if (visual == null) return;
            if (glide.Parachute == null)
                glide.Parachute = visual.GetComponentInChildren<PTK_KartParachuteManager>(true)?.parachuteWithScaleOffset;
            glide.Parachute?.SetActive(show);
            glide.ParachuteOut = show && glide.Parachute != null;
        } catch {
            // The parachute is cosmetic; gliding must work without it.
            glide.ParachuteOut = false;
        }
    }

    // Postfix for PlayerRaceLogic.OnRaceResetted; a no-argument postfix is the
    // established shape for this hook, so it keeps one unambiguous Reset overload.
    internal static void Reset()
    {
        States.Clear();
        Status = "Air Glide: hold jump and do a trick to glide";
    }

    internal static void DrawStatus()
    {
        if (Enabled != null && Enabled.Value && ShowStatus.Value && Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 220, 850, 28), Status);
    }

    private static void Fail(Exception ex)
    {
        Enabled.Value = false;
        Reset();
        Plugin.Instance?.Log.LogError($"Air Glide stopped after an actual error: {ex.Message}");
    }
}
