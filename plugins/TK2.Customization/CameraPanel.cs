using System;
using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// IMGUI avoids an extra injected UI behaviour or third-party overlay dependency.
internal static class CameraPanel
{
    private static bool _open, _advanced, _cursorOwned, _failed;
    private static bool _previousCursorVisible;
    private static CursorLockMode _previousCursorLock;
    private static GUIStyle? _title, _label, _hint;
    private const float Width = 440;

    internal static void Tick(Plugin p)
    {
        try
        {
            if (!p.CameraEnabled.Value || !p.CameraPanelEnabled.Value || !CameraFeature.RaceAvailable)
            { if (_open) Close(p); return; }
            if (Enum.TryParse(p.CameraPanelKey.Value, out KeyCode key) && key != KeyCode.None && Input.GetKeyDown(key))
            {
                if (_open) Close(p);
                else
                {
                    _failed = false; _open = true;
                    _previousCursorVisible = Cursor.visible; _previousCursorLock = Cursor.lockState;
                    _cursorOwned = true;
                }
            }
            if (_open && Input.GetKeyDown(KeyCode.Escape)) Close(p);
            if (_open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        catch (Exception ex) { Fail(p, ex); }
    }

    internal static void Close(Plugin p)
    {
        _open = false;
        if (_cursorOwned)
        {
            if (Cursor.lockState == CursorLockMode.None)
            { Cursor.lockState = _previousCursorLock; Cursor.visible = _previousCursorVisible; }
            _cursorOwned = false;
        }
        try { LiveConfig.Flush(p); }
        catch (Exception ex) { p.Log.LogWarning($"Camera config save will retry: {ex.Message}"); }
    }

    private static void Fail(Plugin p, Exception ex)
    {
        if (!_failed) p.Log.LogWarning($"Camera panel unavailable: {ex.Message}");
        _failed = true;
        Close(p);
    }

    internal static void Draw(Plugin p)
    {
        if (!_open || _failed) return;
        Matrix4x4 matrix = GUI.matrix;
        Color color = GUI.color, background = GUI.backgroundColor, content = GUI.contentColor;
        int depth = GUI.depth;
        bool enabled = GUI.enabled;
        try
        {
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _hint ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            float height = _advanced ? 680 : 440;
            float scale = Math.Max(.1f, Math.Min(p.CameraPanelScale.Value,
                Math.Min(Screen.width / (Width + 40), Screen.height / (height + 40))));
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.depth = -10000;
            GUI.color = new Color(.94f, .96f, .99f, 1);
            GUI.backgroundColor = new Color(.17f, .21f, .28f, 1);
            GUI.contentColor = new Color(.94f, .96f, .99f, 1);
            float x = Screen.width / scale - Width - 20, y = 20;
            GUI.Box(new Rect(x, y, Width, height), "");
            GUI.Label(new Rect(x + 18, y + 12, 330, 26), "TK2 Mod Toolkit · Camera", _title);
            if (GUI.Button(new Rect(x + Width - 48, y + 12, 30, 25), "×")) { Close(p); return; }
            y += 47;
            Toggle(p, p.CameraEnabled, "Enable camera overrides", x, ref y);
            Toggle(p, p.PreserveKartFraming, "Keep kart size when FOV changes", x, ref y);
            Slider(p, p.Fov, "Field of view", 35, 110, 1, x, ref y);
            Slider(p, p.CameraDistance, "Distance multiplier", .25f, 4, .05f, x, ref y);
            Slider(p, p.CameraHeight, "Height offset", -5, 8, .05f, x, ref y);
            Slider(p, p.CameraLateral, "Side offset", -4, 4, .05f, x, ref y);
            Toggle(p, p.AimAtKart, "Aim toward kart", x, ref y);
            if (GUI.Button(new Rect(x + 18, y, 185, 26), _advanced ? "Hide advanced settings" : "Advanced settings")) _advanced = !_advanced;
            if (GUI.Button(new Rect(x + 216, y, 206, 26), "Reset camera to defaults")) Reset(p);
            y += 34;
            if (_advanced)
            {
                bool oldEnabled = GUI.enabled;
                GUI.enabled = p.AimAtKart.Value;
                Slider(p, p.CameraTargetHeight, "Look-at target height", -1, 3, .05f, x, ref y);
                GUI.enabled = oldEnabled;
                Slider(p, p.CameraPitch, "Pitch offset", -45, 45, 1, x, ref y);
                Slider(p, p.CameraYaw, "Yaw offset", -90, 90, 1, x, ref y);
                Slider(p, p.CameraRoll, "Roll offset", -30, 30, 1, x, ref y);
                Slider(p, p.CameraSmoothing, "Smoothing seconds", 0, 2, .05f, x, ref y);
            }
            GUI.Label(new Rect(x + 18, y, Width - 36, 34), LiveConfig.Status, _hint);
            GUI.Label(new Rect(x + 18, y + 36, Width - 36, 28), $"{p.CameraPanelKey.Value} / Esc closes · +/- fine tune · Shift = finer", _hint);
        }
        catch (Exception ex) { Fail(p, ex); }
        finally
        { GUI.matrix = matrix; GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content; GUI.depth = depth; GUI.enabled = enabled; }
    }

    private static void Toggle(Plugin p, ConfigEntry<bool> entry, string text, float x, ref float y)
    {
        bool value = GUI.Toggle(new Rect(x + 18, y, Width - 36, 24), entry.Value, text);
        LiveConfig.Change(p, entry, value, Time.unscaledTime); y += 26;
    }

    private static void Slider(Plugin p, ConfigEntry<float> entry, string text, float low, float high, float step, float x, ref float y)
    {
        GUI.Label(new Rect(x + 18, y, Width - 36, 22), $"{text}: {entry.Value:0.00}    [{low:0.##} … {high:0.##}]", _label);
        float value = GUI.HorizontalSlider(new Rect(x + 18, y + 27, Width - 126, 18), entry.Value, low, high);
        float delta = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? step * .1f : step;
        if (GUI.Button(new Rect(x + Width - 98, y + 22, 36, 23), "−")) value -= delta;
        if (GUI.Button(new Rect(x + Width - 56, y + 22, 36, 23), "+")) value += delta;
        if (value != entry.Value)
        {
            value = Mathf.Clamp((float)Math.Round(value, 3), low, high);
            LiveConfig.Change(p, entry, value, Time.unscaledTime);
        }
        y += 48;
    }

    private static void Reset(Plugin p)
    {
        // Reset camera geometry while retaining panel accessibility and current on/off choice.
        foreach (var entry in new[] { p.Fov, p.CameraDistance, p.CameraHeight, p.CameraLateral,
            p.CameraTargetHeight, p.CameraPitch, p.CameraYaw, p.CameraRoll, p.CameraSmoothing })
            LiveConfig.Change(p, entry, (float)entry.DefaultValue, Time.unscaledTime);
        LiveConfig.Change(p, p.PreserveKartFraming, true, Time.unscaledTime);
        LiveConfig.Change(p, p.AimAtKart, false, Time.unscaledTime);
    }
}
