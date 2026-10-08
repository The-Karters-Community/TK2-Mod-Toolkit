using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

public sealed partial class Plugin
{
    internal ConfigEntry<bool> CameraPanelEnabled = null!, AimAtKart = null!;
    internal ConfigEntry<string> CameraPanelKey = null!;
    internal ConfigEntry<float> CameraLateral = null!, CameraPitch = null!, CameraYaw = null!, CameraRoll = null!;
    internal ConfigEntry<float> CameraTargetHeight = null!, CameraSmoothing = null!, CameraPanelScale = null!;

    private void BindCamera()
    {
        CameraEnabled = Config.Bind("Camera", "Enabled", false, "Customize the local racing camera. All camera values reload live.");
        Fov = CameraNumber("FieldOfView", 65, 35, 110, "Vertical FOV in degrees.");
        PreserveKartFraming = Config.Bind("Camera", "PreserveKartFraming", true, "Compensate distance when FOV changes so the kart keeps its apparent size.");
        CameraDistance = CameraNumber("DistanceMultiplier", 1, .25f, 4, "Distance relative to the native racing camera.");
        CameraHeight = CameraNumber("HeightOffset", 0, -5, 8, "Extra world-space camera height.");
        CameraLateral = CameraNumber("LateralOffset", 0, -4, 4, "Horizontal offset along the native camera's right axis.");
        AimAtKart = Config.Bind("Camera", "AimAtKart", false, "Aim toward the kart instead of keeping the native camera rotation.");
        CameraTargetHeight = CameraNumber("TargetHeight", .5f, -1, 3, "Height of the look-at target above the kart; used with AimAtKart.");
        CameraPitch = CameraNumber("PitchOffset", 0, -45, 45, "Extra pitch in degrees, relative to the selected camera rotation.");
        CameraYaw = CameraNumber("YawOffset", 0, -90, 90, "Extra yaw in degrees.");
        CameraRoll = CameraNumber("RollOffset", 0, -30, 30, "Extra roll in degrees.");
        CameraSmoothing = CameraNumber("SmoothingSeconds", .12f, 0, 2, "Adjustment smoothing time; zero applies changes instantly.");
        CameraPanelEnabled = Config.Bind("Camera", "PanelEnabled", true, "Allow the in-race camera panel even when camera overrides are currently off.");
        CameraPanelKey = Config.Bind("Camera", "PanelHotkey", "F8",
            new ConfigDescription("Press this key during a local race to open or close the camera panel. Escape closes it too.",
                new AcceptableValueList<string>("F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
                    "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
                    "Insert", "Home", "End", "BackQuote", "None")));
        CameraPanelScale = CameraNumber("PanelScale", 1, .7f, 1.6f, "In-race panel scale; automatically reduced to fit the screen.");
    }

    private ConfigEntry<float> CameraNumber(string key, float value, float low, float high, string description) =>
        Config.Bind("Camera", key, value, new ConfigDescription(description, new AcceptableValueRange<float>(low, high)));
}
