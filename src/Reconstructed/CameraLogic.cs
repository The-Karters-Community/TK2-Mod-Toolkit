namespace TK2.Reconstructed;

public static class CameraLogic
{
    // PixelGameKartCamera.GetCamera @ 0x1804ee5b0. Array access is performed by the adapter.
    // ePlayerType: human local=0, ghost=4. Ghost has no camera even in spectator mode.
    public static int? GetCameraIndex(int originalPlayerType, int originalPlayerNumber, bool spectator)
    {
        if (originalPlayerType == 4) return null;
        if (!spectator || originalPlayerType == 0) return originalPlayerNumber;
        return 0;
    }

    // PixelGameKartCamera.IsCameraASpectatorCamera @ 0x1804f05a0.
    public static bool IsSpectator(bool spectatorFlag) => spectatorFlag;

    // PixelGameKartCamera.IsIntroCameraActiveAndRunning @ 0x1804f05b0.
    // Native comparison preserves NaN behavior. Threshold bytes 33 33 73 3f at VA 0x183707e34.
    public static bool IsIntroActive(float racingCameraStrength) => racingCameraStrength < 0.95f;

    // PixelGameKartCamera.EnableTribuneCamera @ 0x1804ed620.
    public static void EnableTribune(ref bool enabled, bool requested) => enabled = requested;

    // PixelGameKartCamera.ForceInstantTeleport @ 0x1804ed730.
    public static void ForceInstantTeleport(ref bool forced, ref bool tribune)
    {
        forced = true;
        tribune = false;
    }
}
