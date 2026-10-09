using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TK2.Customization;

internal static class GamepadOverlay
{
    private static ConfigEntry<bool> _enabled = null!, _startVisible = null!, _showReplayInputs = null!;
    private static ConfigEntry<string> _hotkey = null!, _corner = null!;
    private static ConfigEntry<float> _scale = null!, _opacity = null!;
    private static bool _visible, _hasSample, _faulted;
    private static float _nextResolve;
    private static InputOverlayModel _sample;
    private static Ant_KartInput? _localInput;
    private static PTK_PlayerReplayInput_ReplaySerializer? _replayInput;
    private static string _liveDevice = "GAMEPAD / KEYBOARD";
    private static readonly FieldInfo? RewiredPlayer = AccessTools.Field(typeof(Ant_KartInput), "player");
    private static GUIStyle? _title, _small, _pill;

    internal static bool Visible => _enabled?.Value == true && _visible && _hasSample;

    internal static void Install(Plugin plugin)
    {
        _enabled = plugin.Config.Bind("GamepadOverlay", "Enabled", false, "Show live driving inputs or recorded leaderboard-ghost inputs in a compact in-game HUD card.");
        _startVisible = plugin.Config.Bind("GamepadOverlay", "StartVisible", true, "Show automatically during a local race or replay when this module is enabled.");
        _showReplayInputs = plugin.Config.Bind("GamepadOverlay", "ShowReplayInputs", true, "Show hidden control actions recorded in leaderboard ghosts. While enabled, mandatory online protection blocks uploads and online-room actions.");
        _hotkey = plugin.Config.Bind("GamepadOverlay", "ToggleKey", "F9", "Toggle the input card during a race or replay. Set to None to disable.");
        _corner = plugin.Config.Bind("GamepadOverlay", "Corner", "TopCenter", "Screen position for the input card.");
        _scale = plugin.Config.Bind("GamepadOverlay", "Scale", 1f, new ConfigDescription("HUD card scale.", new AcceptableValueRange<float>(0.7f, 1.4f)));
        _opacity = plugin.Config.Bind("GamepadOverlay", "Opacity", 0.92f, new ConfigDescription("HUD card background opacity.", new AcceptableValueRange<float>(0.45f, 1f)));
        ResetState();
    }

    internal static void Tick(Plugin plugin)
    {
        if (!_enabled.Value) { ResetState(); return; }
        if (Enum.TryParse(_hotkey.Value, true, out KeyCode key) && key != KeyCode.None && Input.GetKeyDown(key))
            _visible = !_visible;
        try { SampleInput(); _faulted = false; }
        catch (Exception ex)
        {
            _hasSample = false;
            _localInput = null;
            _replayInput = null;
            _nextResolve = Time.unscaledTime;
            if (!_faulted) plugin.Log.LogWarning("Gamepad viewer input sample failed; retrying: " + ex.Message);
            _faulted = true;
        }
    }

    private static void SampleInput()
    {
        if (Time.unscaledTime >= _nextResolve)
        {
            ResolveInputs();
            _nextResolve = Time.unscaledTime + .25f;
        }
        bool replayActive = false;
        PTK_ReplayManager manager = PTK_ReplayManager.Instance;
        if (manager != null)
        {
            try { replayActive = manager.IsPlayingReplay(); }
            catch { replayActive = false; }
        }

        if (replayActive && _showReplayInputs.Value)
        {
            if (_replayInput != null && _replayInput.bReplayDataReceived)
            {
                var input = _replayInput;
                _sample = InputOverlayModel.CreateReplay(input.fTurningInput_Get, input.fAccelInput_Get,
                    input.bJumpButton_Get_Click, input.bDriftInput_Get, input.bBoostInput_Get_Click,
                    input.bUseWeaponInput_Get_Click, input.bIsBreakingInput_Get,
                    input.bBackDirInputButtonDown_Get, input.bIsTargetLockUp_Get_Clcik,
                    input.bIsTargetLockDown_Get_Click, input.bWeaponPickupButtomClicked_Get);
                _hasSample = true;
            }
            else _hasSample = false;
            return;
        }

        if (Ant_CurrentGameConfiguration.eCurrentRaceState != Ant_CurrentGameConfiguration.ERaceState.E_RACE_RUNNING)
        { _hasSample = false; return; }
        if (_localInput?.inputData == null) { _hasSample = false; return; }
        var data = _localInput.inputData;
        _sample = InputOverlayModel.CreateLive(data.fTurningInput, data.fAccelInput,
            data.bJumpButton_ClickedDown, data.bDriftInput, data.bBoostInput_ClickedDown,
            data.bUseWeaponInput_ClickDown, data.bIsBreakingInput, data.bBackDirInputButtonDown,
            data.bIsTargetLockUp_ClickUp, data.bIsTargetLockDown_ClickDown,
            data.bWeaponPickupButtomClicked_ClickedDown, _liveDevice);
        _hasSample = true;
    }

    private static void ResolveInputs()
    {
        _localInput = null;
        foreach (var input in UnityEngine.Object.FindObjectsByType<Ant_KartInput>(FindObjectsSortMode.None))
        {
            if (input != null && input.antPlayer != null && input.antPlayer.ePlayerType == Ant_Player.EPlayerType.E_HUMAN_LOCAL)
            { _localInput = input; _liveDevice = DetectLastDevice(input); break; }
        }
        _replayInput = null;
        PTK_ReplayManager manager = PTK_ReplayManager.Instance;
        if (manager == null) return;
        Ant_Player expected = manager.antPlayerGhost;
        foreach (var input in UnityEngine.Object.FindObjectsByType<PTK_PlayerReplayInput_ReplaySerializer>(FindObjectsSortMode.None))
        {
            if (input == null) continue;
            if (_replayInput == null) _replayInput = input;
            if (expected != null && input.GetComponentInParent<Ant_Player>() == expected) { _replayInput = input; break; }
        }
    }

    private static string DetectLastDevice(Ant_KartInput input)
    {
        try
        {
            object? player = RewiredPlayer?.GetValue(input);
            if (player == null) return "GAMEPAD / KEYBOARD";
            PropertyInfo? controllersProperty = player.GetType().GetProperty("controllers", BindingFlags.Public | BindingFlags.Instance);
            object? controllers = controllersProperty?.GetValue(player);
            if (controllers == null) return "GAMEPAD / KEYBOARD";
            MethodInfo? lastActive = controllers.GetType().GetMethod("GetLastActiveController", Type.EmptyTypes);
            object? controller = lastActive?.Invoke(controllers, null);
            if (controller == null) return "GAMEPAD / KEYBOARD";
            PropertyInfo? type = controller.GetType().GetProperty("type", BindingFlags.Public | BindingFlags.Instance);
            string name = type?.GetValue(controller)?.ToString() ?? controller.GetType().Name;
            return name.IndexOf("keyboard", StringComparison.OrdinalIgnoreCase) >= 0 ? "KEYBOARD" :
                name.IndexOf("mouse", StringComparison.OrdinalIgnoreCase) >= 0 ? "KEYBOARD / MOUSE" : "GAMEPAD";
        }
        catch { return "GAMEPAD / KEYBOARD"; }
    }

    internal static void Draw()
    {
        if (!Visible) return;
        EnsureStyles();
        float scale = Mathf.Clamp(_scale.Value, .7f, 1.4f);
        const float baseWidth = 352, baseHeight = 180;
        float availableScale = Mathf.Min((Screen.width - 24f) / baseWidth, (Screen.height - 24f) / baseHeight);
        scale = Mathf.Max(.35f, Mathf.Min(scale, availableScale));
        float width = baseWidth * scale, height = baseHeight * scale, margin = 18 * scale;
        string corner = _corner.Value;
        float x = corner.EndsWith("Right", StringComparison.OrdinalIgnoreCase) ? Screen.width - width - margin :
            corner == "TopCenter" ? (Screen.width - width) * .5f : margin;
        float y = corner.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase) ? Screen.height - height - margin :
            corner == "TopCenter" ? 210 * scale : margin;
        x = Mathf.Clamp(x, margin, Mathf.Max(margin, Screen.width - width - margin));
        y = Mathf.Clamp(y, margin, Mathf.Max(margin, Screen.height - height - margin));
        var rect = new Rect(x, y, width, height);
        DrawCard(rect, _opacity.Value);
        GUI.BeginGroup(rect);
        float s = scale;
        GUI.Label(new Rect(16*s, 9*s, 178*s, 22*s), "INPUT FEED", _title);
        GUI.Label(new Rect(174*s, 11*s, 136*s, 18*s), _sample.IsReplay ? "● REPLAY" : "● LIVE", _small);
        GUI.Label(new Rect(16*s, 32*s, 240*s, 17*s), _sample.IsReplay ? "RECORDED GHOST ACTIONS" : _sample.Device, _small);
        DrawSteering(new Rect(16*s, 57*s, 141*s, 54*s), _sample.Steering, s);
        DrawAcceleration(new Rect(169*s, 57*s, 141*s, 54*s), _sample.Acceleration, s);
        DrawButton(new Rect(16*s, 120*s, 74*s, 23*s), "JUMP", _sample.Jump, new Color(.18f,.72f,.95f), s);
        DrawButton(new Rect(96*s, 120*s, 74*s, 23*s), "DRIFT", _sample.Drift, new Color(.75f,.52f,.98f), s);
        DrawButton(new Rect(176*s, 120*s, 74*s, 23*s), "BOOST", _sample.Boost, new Color(1f,.57f,.12f), s);
        DrawButton(new Rect(256*s, 120*s, 74*s, 23*s), "ITEM", _sample.Weapon, new Color(.98f,.3f,.34f), s);
        DrawButton(new Rect(16*s, 149*s, 74*s, 23*s), "BRAKE", _sample.Brake, new Color(.84f,.88f,.94f), s);
        DrawButton(new Rect(96*s, 149*s, 74*s, 23*s), "LOOK BACK", _sample.LookBack, new Color(.35f,.78f,.68f), s);
        DrawButton(new Rect(176*s, 149*s, 74*s, 23*s), "TARGET", _sample.TargetUp || _sample.TargetDown, new Color(.38f,.65f,1f), s);
        DrawButton(new Rect(256*s, 149*s, 74*s, 23*s), "PICKUP", _sample.Pickup, new Color(.97f,.74f,.25f), s);
        GUI.EndGroup();
    }

    private static void DrawCard(Rect rect, float alpha)
    {
        Color old = GUI.color;
        GUI.color = new Color(.025f, .045f, .075f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(.1f,.7f,.94f,.95f); GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
        GUI.color = new Color(.2f,.28f,.39f,.95f);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax-1, rect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax-1, rect.y, 1, rect.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void DrawSteering(Rect rect, float value, float scale)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 16*scale), "STEERING", _small);
        float mid = rect.x + rect.width * .5f, cy = rect.y + 33*scale;
        DrawBar(new Rect(rect.x, cy-3*scale, rect.width, 7*scale), new Color(.08f,.13f,.2f,1));
        DrawBar(new Rect(mid-1*scale, cy-5*scale, 2*scale, 11*scale), new Color(.5f,.62f,.76f,1));
        float knob = mid + value * rect.width * .43f;
        DrawBar(new Rect(knob-5*scale, cy-5*scale, 10*scale, 11*scale), new Color(.12f,.74f,.96f,1));
        GUI.Label(new Rect(rect.x, rect.y+40*scale, rect.width, 13*scale), value.ToString("+0.00;-0.00;0.00"), _small);
    }

    private static void DrawAcceleration(Rect rect, float value, float scale)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 16*scale), "THROTTLE / REVERSE", _small);
        float center = rect.x + rect.width * .5f, cy = rect.y + 33*scale;
        DrawBar(new Rect(rect.x, cy-3*scale, rect.width, 7*scale), new Color(.08f,.13f,.2f,1));
        DrawBar(new Rect(center-1*scale, cy-6*scale, 2*scale, 13*scale), new Color(.5f,.62f,.76f,1));
        float left = value < 0 ? center + value * rect.width * .43f : center;
        float right = value > 0 ? center + value * rect.width * .43f : center;
        if (right > left) DrawBar(new Rect(left, cy-2*scale, right-left, 5*scale), new Color(1f,.58f,.1f,1));
        GUI.Label(new Rect(rect.x, rect.y+40*scale, rect.width, 13*scale), value.ToString("+0.00;-0.00;0.00"), _small);
    }

    private static void DrawButton(Rect rect, string label, bool pressed, Color accent, float scale)
    {
        Color old = GUI.color;
        GUI.color = pressed ? new Color(accent.r, accent.g, accent.b, .34f) : new Color(.08f,.12f,.18f,.96f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = pressed ? accent : new Color(.26f,.34f,.46f,.9f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1*scale), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax-1*scale, rect.width, 1*scale), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 1*scale, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax-1*scale, rect.y, 1*scale, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, label, _pill);
        GUI.color = old;
    }

    private static void DrawBar(Rect rect, Color color)
    {
        Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
    }

    private static void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _title.normal.textColor = Color.white;
        _small = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
        _small.normal.textColor = new Color(.68f,.8f,.91f,1);
        _pill = new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _pill.normal.textColor = Color.white;
    }

    internal static void Restore() { ResetState(); _faulted = false; }
    private static void ResetState() { _visible = _startVisible?.Value == true; _hasSample = false; _nextResolve = 0; _localInput = null; _replayInput = null; }
}
