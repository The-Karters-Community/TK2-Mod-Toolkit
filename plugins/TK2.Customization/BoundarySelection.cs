namespace TK2.Customization;

internal enum BoundaryKind { None, Wall, Respawn }
internal static class BoundarySelection
{
    internal static BoundaryKind Classify(int layer, int wallMask, int flatRespawnMask, int alwaysRespawnMask)
    {
        if (layer < 0 || layer > 31) return BoundaryKind.None;
        int bit = 1 << layer;
        // Native OnMovementHit checks respawn independently of wall-hit effects.
        if (((flatRespawnMask | alwaysRespawnMask) & bit) != 0) return BoundaryKind.Respawn;
        return (wallMask & bit) != 0 ? BoundaryKind.Wall : BoundaryKind.None;
    }
}
