using System;
using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TK2.Customization;

internal static partial class AntigravityFeature
{
    internal static void DrawStatus()
    {
        if (ShowStatus?.Value == true && Plugin.Instance?.AntigravityEnabled.Value == true &&
            Plugin.OfflineLabAllowed && MenuManager.Instance == null)
            GUI.Label(new Rect(18, Screen.height - 190, 1400, 48), Status);
    }

    private static void SetStatus(string status)
    {
        if (!string.Equals(Status, status, StringComparison.Ordinal)) Status = status;
    }


}
