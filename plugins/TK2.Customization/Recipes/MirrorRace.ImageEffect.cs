using System;
using UnityEngine;

namespace TK2.Customization;

/// <summary>Horizontal image flip after the camera has rendered the scene.</summary>
public sealed class MirrorImageEffect : MonoBehaviour
{
    private Camera? _camera;
    private static bool _loggedCallback;

    public MirrorImageEffect(IntPtr pointer) : base(pointer) { }

    public void Awake() => _camera = GetComponent<Camera>();

    public void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!MirrorRace.ShouldMirror(_camera)) { Graphics.Blit(source, destination); return; }

        Graphics.Blit(source, destination, new Vector2(-1f, 1f), new Vector2(1f, 0f));
        if (_loggedCallback) return;
        _loggedCallback = true;
        Plugin.Instance?.Log.LogInfo("Mirror Race is flipping the completed local camera image.");
    }
}
