using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

/// <summary>Compensates screen-space nameplates and countdown glyph UVs for the camera image flip.</summary>
public sealed partial class MirrorRace
{
    private readonly Dictionary<int, MarkerPlacement> _markerPlacements = new();
    private readonly Dictionary<int, BoardTexture> _boardTextures = new();
    private bool _loggedMarker;
    private bool _loggedBoard;

    private readonly record struct MarkerPlacement(RectTransform Root, Vector3 NativePosition);
    private readonly record struct BoardTexture(Material Material, Vector2 Scale, Vector2 Offset);

    private static void PatchCountdown(Harmony harmony, string methodName, Type[] parameters)
    {
        MethodInfo target = AccessTools.DeclaredMethod(typeof(PTK_CounterBoardController), methodName, parameters)
            ?? throw new MissingMethodException(typeof(PTK_CounterBoardController).FullName, methodName);
        harmony.Patch(target,
            prefix: new HarmonyMethod(typeof(MirrorRace), nameof(BeforeCountdownUpdate)),
            postfix: new HarmonyMethod(typeof(MirrorRace), nameof(AfterCountdownUpdate)));
    }

    private static void BeforeWaypointUpdate(Waypoint_Indicator __instance)
    {
        var module = _instance;
        if (module == null || __instance == null) return;
        if (module._markerPlacements.Remove(__instance.GetInstanceID(), out var saved) && saved.Root != null)
            saved.Root.position = saved.NativePosition;
    }

    private static void AfterWaypointUpdate(Waypoint_Indicator __instance)
    {
        var module = _instance;
        if (module == null || __instance == null || !module.MirrorActive) return;
        var camera = __instance.mainCamera;
        var root = __instance.wpParentRectTransform;
        var canvas = __instance.mainCanvas;
        if (!ShouldMirror(camera) || root == null || canvas == null) return;
        var marker = __instance.GetComponentInParent<PTK_MarkerIndicator>();
        if (marker == null || marker.markerIndicatorImpl != __instance) return;
        var parent = root.parent?.TryCast<RectTransform>();
        if (parent == null) return;

        // Native CreateMarkerForOtherPlayer passes the CAMERA as distCalTarget.
        // Preserve the native racer tracking, height offset and edge clamping;
        // reflect its finished placement instead of projecting a guessed target.
        Camera? uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 nativePosition = root.position;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, nativePosition);
        Rect viewport = camera!.pixelRect;
        screen.x = MirrorScreenX(screen.x, viewport.xMin, viewport.xMax);
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, uiCamera, out Vector3 mirrored)) return;
        module._markerPlacements[__instance.GetInstanceID()] = new(root, nativePosition);
        root.position = mirrored;
        if (module._loggedMarker) return;
        module._loggedMarker = true;
        Plugin.Instance?.Log.LogInfo("Mirror Race nameplates follow native racer placement within the local camera viewport.");
    }

    internal static float MirrorScreenX(float x, float left, float right) => left + right - x;

    private static void BeforeCountdownUpdate(PTK_CounterBoardController __instance)
    {
        var module = _instance;
        if (module == null || __instance == null) return;
        if (module._boardTextures.Remove(__instance.GetInstanceID(), out var saved)) RestoreBoard(saved);
    }

    private static void AfterCountdownUpdate(PTK_CounterBoardController __instance)
    {
        var module = _instance;
        if (module == null || __instance == null || !module.MirrorActive) return;
        int id = __instance.GetInstanceID();
        // A nested ChangeTileMatOffsets call may already have compensated this board.
        if (module._boardTextures.ContainsKey(id)) return;
        var material = __instance.numbersTileMatInstance;
        if (material == null) return;
        Vector2 scale = material.mainTextureScale;
        Vector2 offset = material.mainTextureOffset;
        module._boardTextures[id] = new(material, scale, offset);
        // Reflect within the selected atlas tile; keep its number and vertical UVs.
        material.mainTextureScale = new Vector2(-scale.x, scale.y);
        material.mainTextureOffset = new Vector2(offset.x + scale.x, offset.y);
        if (module._loggedBoard) return;
        module._loggedBoard = true;
        Plugin.Instance?.Log.LogInfo("Mirror Race countdown glyphs compensated without delaying the camera flip.");
    }

    private static void RestoreBoard(BoardTexture saved)
    {
        if (saved.Material == null) return;
        saved.Material.mainTextureScale = saved.Scale;
        saved.Material.mainTextureOffset = saved.Offset;
    }

    private void RestorePresentation()
    {
        foreach (var saved in _markerPlacements.Values)
            if (saved.Root != null) saved.Root.position = saved.NativePosition;
        _markerPlacements.Clear();
        foreach (var saved in _boardTextures.Values) RestoreBoard(saved);
        _boardTextures.Clear();
    }
}
