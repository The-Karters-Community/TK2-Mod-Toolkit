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
    private static GUIStyle? _title, _label, _hint, _value;
    private static Vector2 _scroll;
    private const float Width = 384;

    internal static void Tick(Plugin p)
    {
        try
        {
            // Turning overrides off restores the native view; the editor must stay usable.
            if (!p.CameraPanelEnabled.Value || !CameraFeature.RaceAvailable)
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
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
                _title.normal.textColor = _label.normal.textColor = new Color(.94f, .95f, .90f, 1);
                _hint = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
                _hint.normal.textColor = new Color(.72f, .74f, .68f, 1);
                _value = new GUIStyle(_label) { alignment = TextAnchor.MiddleRight };
            }
            // Scroll a short panel on small displays rather than shrinking the text.
            float scale = Math.Max(.1f, Math.Min(p.CameraPanelScale.Value, Screen.width / (Width + 40)));
            float height = Math.Min(_advanced ? 704 : 480, Screen.height / scale - 40);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.depth = -10000;
            GUI.color = Color.white;
            GUI.backgroundColor = new Color(.25f, .26f, .23f, 1);
            GUI.contentColor = new Color(.94f, .95f, .90f, 1);
            float x = Screen.width / scale - Width - 20, y = 20;
            Fill(new Rect(x, y, Width, height), new Color(.075f, .08f, .075f, .98f));
            GUI.Label(new Rect(x + 18, y + 12, 240, 28), "Camera", _title);
            GUI.Label(new Rect(x + 18, y + 42, 300, 20), $"Changes apply live  ·  {p.CameraPanelKey.Value} / Esc to close", _hint);
            if (GUI.Button(new Rect(x + Width - 46, y + 15, 28, 28), "×")) { Close(p); return; }
            Fill(new Rect(x + 18, y + 69, Width - 36, 1), new Color(.29f, .30f, .28f, 1));
            _scroll = GUI.BeginScrollView(new Rect(x, y + 78, Width - 4, height - 154), _scroll,
                new Rect(0, 0, Width - 24, _advanced ? 548 : 320));
            try
            {
                float row = 0;
                Toggle(p, p.CameraEnabled, "Camera overrides", 0, ref row);
                GUI.Label(new Rect(18, row + 2, Width - 60, 22), "Framing", _hint);
                row += 26;
                Slider(p, p.Fov, "Field of view", 35, 110, 1, 0, ref row, "°");
                Slider(p, p.CameraDistance, "Distance", .25f, 4, .05f, 0, ref row, "×");
                Slider(p, p.CameraHeight, "Height", -5, 8, .05f, 0, ref row);
                Slider(p, p.CameraLateral, "Side offset", -4, 4, .05f, 0, ref row);
                Toggle(p, p.PreserveKartFraming, "Keep kart size when FOV changes", 0, ref row);
                Toggle(p, p.AimAtKart, "Aim toward kart", 0, ref row);
                if (GUI.Button(new Rect(18, row + 4, Width - 60, 28), _advanced ? "Hide aim, rotation & smoothing" : "Aim, rotation & smoothing…")) _advanced = !_advanced;
                row += 40;
                if (_advanced)
                {
                    GUI.enabled = enabled && p.AimAtKart.Value;
                    Slider(p, p.CameraTargetHeight, "Target height", -1, 3, .05f, 0, ref row);
                    GUI.enabled = enabled;
                    Slider(p, p.CameraPitch, "Pitch", -45, 45, 1, 0, ref row, "°");
                    Slider(p, p.CameraYaw, "Yaw", -90, 90, 1, 0, ref row, "°");
                    Slider(p, p.CameraRoll, "Roll", -30, 30, 1, 0, ref row, "°");
                    Slider(p, p.CameraSmoothing, "Smoothing", 0, 2, .05f, 0, ref row, " s");
                }
            }
            finally { GUI.EndScrollView(); }
            Fill(new Rect(x + 18, y + height - 69, Width - 36, 1), new Color(.29f, .30f, .28f, 1));
            if (GUI.Button(new Rect(x + 18, y + height - 57, 128, 27), "Reset framing")) Reset(p);
            GUI.Label(new Rect(x + 158, y + height - 57, Width - 176, 27), "Hold Shift for fine steps", _hint);
            GUI.Label(new Rect(x + 18, y + height - 27, Width - 36, 24), string.IsNullOrEmpty(GUI.tooltip) ? LiveConfig.Status : GUI.tooltip, _hint);
        }
        catch (Exception ex) { Fail(p, ex); }
        finally
        { GUI.matrix = matrix; GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content; GUI.depth = depth; GUI.enabled = enabled; }
    }

    private static void Toggle(Plugin p, ConfigEntry<bool> entry, string text, float x, ref float y)
    {
        bool value = StudioBehaviour.DrawCheckBox(new Rect(x + 18, y, Width - 94, 24), entry.Value, text);
        if (value != entry.Value) LiveConfig.Change(p, entry, value, Time.unscaledTime);
        if (GUI.Button(new Rect(x + Width - 66, y, 24, 24), new GUIContent("↺", "Reset " + text)))
            LiveConfig.Change(p, entry, (bool)entry.DefaultValue, Time.unscaledTime);
        y += 28;
    }

    private static void Slider(Plugin p, ConfigEntry<float> entry, string text, float low, float high, float step, float x, ref float y, string unit = "")
    {
        GUI.Label(new Rect(x + 18, y, Width - 158, 22), text, _label);
        GUI.Label(new Rect(x + Width - 137, y, 91, 22), entry.Value.ToString("0.##") + unit, _value);
        float value = GUI.HorizontalSlider(new Rect(x + 18, y + 25, Width - 156, 16), entry.Value, low, high);
        float delta = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? step * .1f : step;
        if (GUI.Button(new Rect(x + Width - 126, y + 19, 24, 25), "−")) value -= delta;
        if (GUI.Button(new Rect(x + Width - 96, y + 19, 24, 25), "+")) value += delta;
        if (GUI.Button(new Rect(x + Width - 66, y + 19, 24, 25), new GUIContent("↺", "Reset " + text))) value = (float)entry.DefaultValue;
        if (value != entry.Value)
            LiveConfig.Change(p, entry, Mathf.Clamp((float)Math.Round(value, 3), low, high), Time.unscaledTime);
        y += 44;
    }

    private static void Fill(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
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
