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
    private static GUIStyle? _title, _small, _pill, _section, _secondary, _status;
    private static Texture2D? _disc;

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
        // inputData is already normalized for gameplay: ProcessRacingInput swaps the
        // Drift/Boost route while drifting. Read named Rewired actions instead so this
        // viewer reports the action bound to the physical/keyboard input.
        bool drift = _localInput.player != null && _localInput.player.GetButton("Drift_left_right");
        bool boost = _localInput.player != null && _localInput.player.GetButton("Boost");
        _sample = InputOverlayModel.CreateLive(data.fTurningInput, data.fAccelInput,
            data.bJumpButton_ClickedDown, drift, boost,
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
        const float baseWidth = 432, baseHeight = 204;
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
        GUI.Label(new Rect(16*s, 9*s, 205*s, 22*s), "INPUT VIEWER", _title);
        DrawStatus(new Rect(328*s, 9*s, 88*s, 20*s), _sample.IsReplay ? "GHOST" : "LIVE", _sample.IsReplay);
        GUI.Label(new Rect(16*s, 32*s, 260*s, 17*s), _sample.IsReplay ? "RECORDED GAME ACTIONS" : _sample.Device, _small);
        DrawPanel(new Rect(14*s, 56*s, 191*s, 132*s), _opacity.Value);
        DrawPanel(new Rect(211*s, 56*s, 207*s, 132*s), _opacity.Value);
        DrawSteering(new Rect(25*s, 66*s, 169*s, 51*s), _sample.Steering, s);
        DrawAcceleration(new Rect(25*s, 126*s, 169*s, 51*s), _sample.Acceleration, s);
        GUI.Label(new Rect(224*s, 63*s, 170*s, 17*s), "ACTIONS", _section);
        DrawButton(new Rect(224*s, 85*s, 87*s, 31*s), "JUMP", _sample.Jump, new Color(.18f,.72f,.95f), s);
        DrawButton(new Rect(317*s, 85*s, 87*s, 31*s), "DRIFT", _sample.Drift, new Color(.75f,.52f,.98f), s);
        DrawButton(new Rect(224*s, 122*s, 87*s, 31*s), "BOOST", _sample.Boost, new Color(1f,.57f,.12f), s);
        DrawButton(new Rect(317*s, 122*s, 87*s, 31*s), "ITEM", _sample.Weapon, new Color(.98f,.3f,.34f), s);
        GUI.Label(new Rect(224*s, 160*s, 182*s, 16*s),
            (_sample.Brake ? "BRAKE  " : "") + (_sample.LookBack ? "LOOK BACK  " : "") +
            (_sample.TargetUp || _sample.TargetDown ? "TARGET  " : "") + (_sample.Pickup ? "PICKUP" : ""), _secondary);
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
        float cx = rect.x + rect.width * .5f, cy = rect.y + 29*scale;
        DrawCircle(new Vector2(cx, cy), 16*scale, new Color(.07f,.11f,.17f,1), new Color(.26f,.36f,.49f,1), scale);
        DrawCircle(new Vector2(cx + value * 10*scale, cy), 6*scale,
            Mathf.Abs(value) > .08f ? new Color(.1f,.76f,1f,1) : new Color(.55f,.65f,.78f,1), Color.clear, scale);
        GUI.Label(new Rect(rect.x, rect.y+39*scale, rect.width, 12*scale), "STEER  " + value.ToString("+0.00;-0.00;0.00"), _secondary);
    }

    private static void DrawAcceleration(Rect rect, float value, float scale)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 16*scale), "THROTTLE / REVERSE", _small);
        float x = rect.x + 4*scale, y = rect.y + 20*scale, w = rect.width - 8*scale;
        DrawBar(new Rect(x, y, w, 8*scale), new Color(.07f,.11f,.17f,1));
        float fill = w * Mathf.Abs(value);
        if (fill > 0) DrawBar(new Rect(value >= 0 ? x : x + w - fill, y, fill, 8*scale),
            value >= 0 ? new Color(1f,.57f,.12f,1) : new Color(.24f,.67f,1f,1));
        GUI.Label(new Rect(rect.x, rect.y+34*scale, rect.width, 12*scale),
            (value >= 0 ? "THROTTLE  " : "REVERSE  ") + Mathf.Abs(value).ToString("0.00"), _secondary);
    }

    private static void DrawButton(Rect rect, string label, bool pressed, Color accent, float scale)
    {
        Color old = GUI.color;
        GUI.color = pressed ? new Color(accent.r, accent.g, accent.b, .28f) : new Color(.055f,.08f,.12f,.98f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = pressed ? accent : new Color(.26f,.35f,.48f,.9f);
        float border = Mathf.Max(1, scale);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax-border, rect.width, border), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, border, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax-border, rect.y, border, rect.height), Texture2D.whiteTexture);
        GUI.color = pressed ? Color.white : new Color(.75f,.82f,.9f,1);
        GUI.Label(rect, label, _pill);
        GUI.color = old;
    }

    private static void DrawStatus(Rect rect, string label, bool replay)
    {
        Color old = GUI.color;
        GUI.color = replay ? new Color(.37f,.7f,1f,.2f) : new Color(.17f,.88f,.64f,.2f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = replay ? new Color(.55f,.8f,1f,1) : new Color(.36f,1f,.74f,1);
        GUI.Label(rect, "● " + label, _status);
        GUI.color = old;
    }

    private static void DrawPanel(Rect rect, float opacity)
    {
        Color old = GUI.color;
        GUI.color = new Color(.035f,.055f,.08f,Mathf.Clamp01(opacity * .82f));
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(.18f,.27f,.38f,Mathf.Clamp01(opacity * .95f));
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax-1, rect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 1, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax-1, rect.y, 1, rect.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void DrawCircle(Vector2 center, float radius, Color fill, Color outline, float scale)
    {
        if (_disc == null)
        {
            const int size = 64;
            _disc = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f - size * .5f) / (size * .5f);
                    float dy = (y + .5f - size * .5f) / (size * .5f);
                    byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt((1f - Mathf.Sqrt(dx * dx + dy * dy)) * size * 32f + 128f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            _disc.SetPixels32(pixels);
            _disc.Apply(false, true);
        }
        Color old = GUI.color;
        Rect outer = new Rect(center.x-radius, center.y-radius, radius*2, radius*2);
        if (outline.a > 0) { GUI.color = outline; GUI.DrawTexture(outer, _disc); }
        float inset = outline.a > 0 ? Mathf.Max(1f, scale) : 0f;
        GUI.color = fill;
        GUI.DrawTexture(new Rect(outer.x+inset, outer.y+inset, outer.width-inset*2, outer.height-inset*2), _disc);
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
        _section = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _section.normal.textColor = new Color(.52f,.68f,.83f,1);
        _secondary = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleLeft };
        _secondary.normal.textColor = new Color(.74f,.81f,.89f,1);
        _status = new GUIStyle(GUI.skin.label) { fontSize = 9, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    internal static void Restore() { ResetState(); _faulted = false; }
    private static void ResetState() { _visible = _startVisible?.Value == true; _hasSample = false; _nextResolve = 0; _localInput = null; _replayInput = null; }
}
