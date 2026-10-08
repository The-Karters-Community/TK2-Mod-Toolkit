using BepInEx.Configuration;
using UnityEngine;

namespace TK2.Customization;

// A complete new mod recipe compiled into the pack. Edit here or in the Toolkit workshop.
public sealed class FrameLimiter : IModRecipe
{
    public string Name => "FrameLimiter";
    public bool ChangesGameplay => false;
    private ConfigEntry<int> _limit = null!;
    private int? _originalLimit, _originalVsync;

    public void Configure(ConfigFile config)
    {
        _limit = config.Bind("Recipe." + Name, "FramesPerSecond", 120,
            new ConfigDescription("Target frame rate with VSync disabled while enabled.", new AcceptableValueRange<int>(30, 360)));
    }

    public void Tick()
    {
        _originalLimit ??= Application.targetFrameRate;
        _originalVsync ??= QualitySettings.vSyncCount;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = _limit.Value;
    }

    public void Restore()
    {
        if (_originalLimit.HasValue) Application.targetFrameRate = _originalLimit.Value;
        if (_originalVsync.HasValue) QualitySettings.vSyncCount = _originalVsync.Value;
        _originalLimit = _originalVsync = null;
    }
}
