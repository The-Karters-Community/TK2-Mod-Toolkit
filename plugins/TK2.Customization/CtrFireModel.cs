using System;

namespace TK2.Customization;

// A seconds-based adaptation of VehFire_Increment; not a bit-exact PS1 integer simulation.
internal sealed class CtrFireModel
{
    internal float Reserves, FireCap;
    private float outsideTimer;
    internal void Tick(float dt, bool onPad)
    {
        if (!float.IsFinite(dt) || dt <= 0) return;
        // The adapter represents continuous pad refresh as a freeze. Freezing
        // the outside timer too prevents repeated pad callbacks minting reserves.
        if (!onPad) {
            outsideTimer = Math.Max(0, outsideTimer - dt);
            Reserves = Math.Max(0, Reserves - dt);
        }
        if (Reserves == 0) FireCap = 0;
    }
    internal void Add(float seconds, float fireCap, bool onSuperPad, bool freezePad = false)
    {
        if (!float.IsFinite(seconds) || !float.IsFinite(fireCap) || seconds < 0 || fireCap < 0) return;
        if (Reserves == 0 || FireCap < fireCap || (FireCap > 16 && !onSuperPad)) FireCap = fireCap;
        if (freezePad) {
            if (outsideTimer < seconds) { Reserves += seconds - outsideTimer; outsideTimer = seconds; }
        } else Reserves += seconds;
    }
    internal void Clear() { Reserves = 0; FireCap = 0; outsideTimer = 0; }
    internal static float DriftGain(float fill, float minimum, float maximum)
    {
        if (!float.IsFinite(fill) || !float.IsFinite(minimum) || !float.IsFinite(maximum) || maximum <= minimum || fill < minimum) return 0;
        return Math.Clamp((fill - minimum) / (maximum - minimum), 0f, 1f) * 2f;
    }
}
