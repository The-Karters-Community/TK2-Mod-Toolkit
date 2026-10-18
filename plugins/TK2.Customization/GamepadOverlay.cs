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
    private static ConfigEntry<float> _positionX = null!, _positionY = null!;
    private static bool _visible, _hasSample, _faulted, _replayActive, _liveInputAvailable;
    private static float _nextResolve;
    private static float _ghostJumpPulseUntil, _ghostBoostPulseUntil;
    private static InputOverlayModel _sample;
    private static Ant_KartInput? _localInput;
    private static PTK_PlayerReplayInput_ReplaySerializer? _replayInput;
    private static Ant_Player? _resolvedReplayTarget;
    private static string _liveDevice = "GAMEPAD / KEYBOARD";
    private static GUIStyle? _title, _small, _pill, _status;
    private static Texture2D? _disc;

    internal static bool Visible => _enabled?.Value == true && _visible && (_hasSample || _liveInputAvailable || _replayActive);

    internal static void Install(Plugin plugin)
    {
        _enabled = plugin.Config.Bind("GamepadOverlay", "Enabled", false, "Show live driving inputs or recorded leaderboard-ghost inputs in a compact in-game HUD card.");
        _startVisible = plugin.Config.Bind("GamepadOverlay", "StartVisible", true, "Show automatically during a local race or replay when this module is enabled.");
        _showReplayInputs = plugin.Config.Bind("GamepadOverlay", "ShowReplayInputs", true, "Show control actions recorded in leaderboard ghosts.");
        _hotkey = plugin.Config.Bind("GamepadOverlay", "ToggleKey", "F9", "Toggle the input card during a race or replay. Set to None to disable.");
        _corner = plugin.Config.Bind("GamepadOverlay", "Corner", "TopCenter", new ConfigDescription(
            "Preset screen position. Choose Custom to use the percentage sliders.",
            new AcceptableValueList<string>("TopLeft", "TopCenter", "TopRight", "MiddleLeft", "MiddleCenter", "MiddleRight", "BottomLeft", "BottomCenter", "BottomRight", "Custom")));
        _scale = plugin.Config.Bind("GamepadOverlay", "Scale", 1f, new ConfigDescription("HUD card scale. The card is scaled uniformly and automatically fits the current screen.", new AcceptableValueRange<float>(0.25f, 2f)));
        _opacity = plugin.Config.Bind("GamepadOverlay", "Opacity", 0.92f, new ConfigDescription("HUD card background opacity.", new AcceptableValueRange<float>(0.45f, 1f)));
        _positionX = plugin.Config.Bind("GamepadOverlay", "PositionX", 50f, new ConfigDescription("Custom horizontal position as a percentage of the available screen area.", new AcceptableValueRange<float>(0, 100)));
        _positionY = plugin.Config.Bind("GamepadOverlay", "PositionY", 0f, new ConfigDescription("Custom vertical position as a percentage of the available screen area.", new AcceptableValueRange<float>(0, 100)));
        // Cache the local kart when it is created or processes input. A timed
        // FindObjectsByType scan can otherwise stall the first visible HUD frame.
        foreach (string methodName in new[] { "Awake", "Start", "ProcessRacingInput" })
        {
            MethodInfo? target = AccessTools.DeclaredMethod(typeof(Ant_KartInput), methodName, Type.EmptyTypes);
            if (target != null)
            {
                try { plugin.Harmony.Patch(target, postfix: new HarmonyMethod(typeof(GamepadOverlay), nameof(LocalInputPostfix))); }
                catch (Exception ex)
                {
                    if (methodName == "ProcessRacingInput") throw;
                    plugin.Log.LogWarning($"Gamepad viewer could not hook Ant_KartInput.{methodName}: {ex.Message}");
                }
            }
        }
        ResetState();
    }

    internal static void Tick(Plugin plugin)
    {
        if (!_enabled.Value) { ResetState(); return; }
        if (Enum.TryParse(_hotkey.Value, true, out KeyCode key) && key != KeyCode.None && Input.GetKeyDown(key))
            _visible = !_visible;
        try { EnsureDisc(); SampleInput(); _faulted = false; }
        catch (Exception ex)
        {
            _hasSample = false;
            _liveInputAvailable = false;
            _localInput = null;
            _replayInput = null;
            _nextResolve = Time.unscaledTime;
            if (!_faulted) plugin.Log.LogWarning("Gamepad viewer input sample failed; retrying: " + ex.Message);
            _faulted = true;
        }
    }

    private static void SampleInput()
    {
        bool replayActive = false;
        bool localControlledByReplay = false;
        PTK_ReplayManager manager = PTK_ReplayManager.Instance;
        if (manager != null)
        {
            try
            {
                replayActive = manager.IsPlayingReplay();
                if (replayActive && _localInput != null && _localInput.antPlayer != null)
                    localControlledByReplay = manager.IsPlayerControlledByReplayInput(_localInput.antPlayer);
            }
            catch { replayActive = false; }
        }
        bool showReplay = InputOverlayResolution.UseReplay(_localInput != null,
            localControlledByReplay, replayActive, _showReplayInputs.Value);
        _liveInputAvailable = _localInput != null && !localControlledByReplay;
        if (showReplay != _replayActive)
        {
            _ghostJumpPulseUntil = _ghostBoostPulseUntil = 0;
            _hasSample = false;
        }
        _replayActive = showReplay;

        if (_replayActive)
        {
            ResolveReplayIfNeeded(manager);
            if (_replayInput != null && _replayInput.bReplayDataReceived)
            {
                var input = _replayInput;
                _sample = InputOverlayModel.CreateReplay(input.fTurningInput_Get, input.fAccelInput_Get,
                    Pulse(input.bJumpButton_Get_Click, ref _ghostJumpPulseUntil) || input.bDriftInput_Get,
                    Pulse(input.bBoostInput_Get_Click, ref _ghostBoostPulseUntil), input.bIsBreakingInput_Get);
                _hasSample = true;
            }
            else _hasSample = false;
            return;
        }

        if (localControlledByReplay || _localInput == null || _localInput.inputData == null)
        { _hasSample = false; return; }
        var data = _localInput.inputData;
        // Discrete inputData fields are mostly click events, and the game deliberately
        // combines Jump/Drift and swaps the boost route during a drift. Sample the
        // player's mapped actions so held keyboard/controller buttons remain lit.
        var player = _localInput.player;
        bool jumpDrift = MappedButton(player, "Jump") || MappedButton(player, "Drift_left_right");
        bool boost = MappedButton(player, "Boost");
        bool weapon = MappedButton(player, "UseWeapon");
        _sample = InputOverlayModel.CreateLive(data.fTurningInput, data.fAccelInput,
            jumpDrift, boost, weapon, data.bIsBreakingInput, _liveDevice);
        _hasSample = true;
    }

    private static void LocalInputPostfix(Ant_KartInput __instance)
    {
        if (_enabled?.Value != true || __instance == null) return;
        try
        {
            var player = __instance.antPlayer;
            if (player == null || player.ePlayerType != Ant_Player.EPlayerType.E_HUMAN_LOCAL) return;
            if (_localInput != __instance)
            {
                _localInput = __instance;
                _liveDevice = DetectLastDevice(__instance);
            }
        }
        catch { /* A partially-created kart can be retried by its next lifecycle hook. */ }
    }

    private static void ResolveReplayIfNeeded(PTK_ReplayManager? manager)
    {
        Ant_Player? expected = manager?.antPlayerGhost;
        if (expected != _resolvedReplayTarget)
        {
            _resolvedReplayTarget = expected;
            _replayInput = null;
            _nextResolve = 0;
        }
        if (!InputOverlayResolution.ShouldResolve(_replayInput != null, Time.unscaledTime, _nextResolve)) return;
        _nextResolve = InputOverlayResolution.NextRetryAt(Time.unscaledTime);
        if (manager == null) return;
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
            object? player = input.player;
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

    private static bool MappedButton(Rewired.Player? player, string action)
    {
        if (player == null) return false;
        try { return player.GetButton(action); }
        catch { return false; }
    }

    private static bool Pulse(bool pressed, ref float until)
    {
        if (pressed) until = Time.unscaledTime + .18f;
        return Time.unscaledTime < until;
    }

    internal static void Draw()
    {
        if (_enabled?.Value != true) return;
        // GUI skin styles are initialized on an early menu/countdown OnGUI pass
        // instead of the first frame on which a valid input sample appears.
        EnsureStyles();
        if (!Visible) return;
        float scale = Mathf.Clamp(_scale.Value, .25f, 2f);
        const float baseWidth = 400, baseHeight = 126;
        float availableScale = Mathf.Max(.1f, Mathf.Min((Screen.width - 24f) / (baseWidth + 36f), (Screen.height - 24f) / (baseHeight + 36f)));
        scale = Mathf.Min(scale, availableScale);
        float width = baseWidth * scale, height = baseHeight * scale, margin = 18 * scale;
        string corner = _corner.Value;
        var position = InputOverlayPlacement.Resolve(corner, Screen.width, Screen.height, width, height,
            margin, _positionX.Value, _positionY.Value);
        float x = position.X, y = position.Y;
        var previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.BeginGroup(new Rect(0, 0, baseWidth, baseHeight));
        try
        {
            DrawCard(new Rect(0, 0, baseWidth, baseHeight), _opacity.Value);
            GUI.Label(new Rect(14, 8, 66, 22), "Inputs", _title);
            GUI.Label(new Rect(79, 10, 216, 20), !_hasSample ? "Waiting for input" : _sample.IsReplay ? "Recorded ghost" : _sample.Device, _small);
            DrawStatus(new Rect(319, 10, 66, 20), !_hasSample ? "WAIT" : _sample.IsReplay ? "GHOST" : "LIVE", _sample.IsReplay);
            DrawSteering(new Rect(14, 46, 96, 61), _hasSample ? _sample.Steering : 0);
            DrawButton(new Rect(124, 45, 82, 28), "Accelerate", _hasSample && _sample.Accelerate);
            DrawButton(new Rect(213, 45, 82, 28), "Brake", _hasSample && _sample.Brake);
            DrawButton(new Rect(302, 45, 82, 28), "Reverse", _hasSample && _sample.Reverse);
            DrawButton(new Rect(124, 81, 82, 28), "Jump / drift", _hasSample && _sample.JumpDrift);
            DrawButton(new Rect(213, 81, 82, 28), "Boost", _hasSample && _sample.Boost);
            if (!_sample.IsReplay)
                DrawButton(new Rect(302, 81, 82, 28), "Weapon", _hasSample && _sample.Weapon);
        }
        finally
        {
            GUI.EndGroup();
            GUI.matrix = previousMatrix;
        }
    }

    private static void DrawCard(Rect rect, float alpha)
    {
        Fill(rect, new Color(.075f, .08f, .075f, Mathf.Clamp01(alpha)));
        Fill(new Rect(14, 36, rect.width - 28, 1), new Color(.29f, .30f, .28f, .75f));
    }

    private static void Fill(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void DrawSteering(Rect rect, float value)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 20), "Steer", _small);
        GUI.Label(new Rect(rect.x + 43, rect.y, 53, 20), value.ToString("+0.00;-0.00;0.00"), _small);
        float cy = rect.y + 39, cx = rect.x + rect.width * .5f;
        Fill(new Rect(rect.x + 4, cy - 2, rect.width - 8, 4), new Color(.28f, .29f, .27f, 1));
        Fill(new Rect(cx, cy - 6, 1, 12), new Color(.56f, .57f, .53f, 1));
        DrawCircle(new Vector2(cx + value * (rect.width * .5f - 8), cy), 6,
            Mathf.Abs(value) > .08f ? new Color(1f, .72f, .36f, 1) : new Color(.86f, .87f, .82f, 1), Color.clear);
    }

    private static void DrawButton(Rect rect, string label, bool pressed)
    {
        Fill(rect, pressed ? new Color(1f, .72f, .36f, 1) : new Color(.17f, .18f, .165f, 1));
        Color old = GUI.contentColor;
        GUI.contentColor = pressed ? new Color(.14f, .12f, .085f, 1) : new Color(.82f, .83f, .78f, 1);
        GUI.Label(rect, label, _pill);
        GUI.contentColor = old;
    }

    private static void DrawStatus(Rect rect, string label, bool replay)
    {
        Color old = GUI.contentColor;
        GUI.contentColor = !_hasSample ? new Color(.66f, .68f, .62f, 1)
            : replay ? new Color(.65f, .8f, .93f, 1) : new Color(1f, .72f, .36f, 1);
        GUI.Label(rect, label, _status);
        GUI.contentColor = old;
    }

    private static void DrawCircle(Vector2 center, float radius, Color fill, Color outline)
    {
        EnsureDisc();
        Color old = GUI.color;
        Rect outer = new Rect(center.x-radius, center.y-radius, radius*2, radius*2);
        if (outline.a > 0) { GUI.color = outline; GUI.DrawTexture(outer, _disc); }
        float inset = outline.a > 0 ? 1f : 0f;
        GUI.color = fill;
        GUI.DrawTexture(new Rect(outer.x+inset, outer.y+inset, outer.width-inset*2, outer.height-inset*2), _disc);
        GUI.color = old;
    }

    private static void EnsureDisc()
    {
        if (_disc != null) return;
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

    private static void DrawBar(Rect rect, Color color)
    {
        Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
    }

    private static void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), wordWrap = false };
        _title.normal.textColor = new Color(.94f, .95f, .90f, 1);
        _small = new GUIStyle(_title) { fontSize = 11, fontStyle = FontStyle.Normal };
        _small.normal.textColor = new Color(.72f, .74f, .68f, 1);
        _pill = new GUIStyle(_title) { fontSize = 11, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
        _pill.normal.textColor = Color.white;
        _status = new GUIStyle(_pill) { fontSize = 10 };

    }

    internal static void Restore() { ResetState(); _faulted = false; }
    private static void ResetState() { _visible = _startVisible?.Value == true; _hasSample = false; _replayActive = false; _liveInputAvailable = false; _nextResolve = 0; _localInput = null; _replayInput = null; _resolvedReplayTarget = null; _ghostJumpPulseUntil = _ghostBoostPulseUntil = 0; }
}
