namespace TK2.Reconstructed;

// Native immunity source decisions at 0x18056c150 and 0x18056ee90.
public static class HealthLogic
{
    public static uint ChangeSource(uint current, uint source, bool active) => active ? current | source : current & ~source;
    public static bool IsImmune(uint sources, bool transformedGhost) => sources != 0 || transformedGhost;
    public static bool IsDeathImmune(uint sources) => (sources & 16) != 0;
    public static bool CollidersActive(uint sources) => sources == 0 || IsDeathImmune(sources);
}
