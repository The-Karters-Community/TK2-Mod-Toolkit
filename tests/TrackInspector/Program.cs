using TK2.Customization;
using Kind = TK2.Customization.TrackInspectorRules.Kind;

int checks = 0;
void Check(bool condition, string description)
{
    checks++;
    if (!condition) throw new Exception(description);
}
int walls = (1 << 5) | (1 << 31), flat = 1 << 7, always = (1 << 7) | (1 << 8);
Check(TrackInspectorRules.InMask(walls, 31), "High-bit Unity layer must remain valid");
Check(!TrackInspectorRules.InMask(walls, -1), "Negative layer rejected");
Check(!TrackInspectorRules.InMask(walls, 32), "Layer 32 cannot wrap to zero");
Check(TrackInspectorRules.Classify(5, walls, flat, always, false, false) == Kind.Wall, "Physical wall mask");
Check(TrackInspectorRules.Classify(7, walls, flat, always, false, false) == Kind.AlwaysRespawn, "Always respawn takes priority over overlapping conditional mask");
Check(TrackInspectorRules.Classify(7, walls, flat, 0, false, false) == Kind.ConditionalRespawn, "Conditional mask remains distinct");
Check(TrackInspectorRules.Classify(5, walls, flat, always, true, true) == Kind.KillLinked, "Actual command linkage takes priority");
Check(TrackInspectorRules.Classify(3, walls, flat, always, false, true) == Kind.OtherTrigger, "Generic trigger is not lethal");
Check(TrackInspectorRules.Classify(3, walls, flat, always, false, false) == Kind.None, "Unrelated scenery excluded");
int[] square = TrackInspectorRules.MeshEdges(new[] { 0, 1, 2, 2, 1, 3 }, 4, 100);
Check(square.Length == 10, "Shared triangle edge is drawn once");
var unique = new HashSet<string>();
for (int i = 0; i < square.Length; i += 2)
{
    Check(square[i] != square[i + 1], "No degenerate edges");
    Check(unique.Add($"{Math.Min(square[i], square[i + 1])}:{Math.Max(square[i], square[i + 1])}"), "No duplicate reversed edges");
}
Check(TrackInspectorRules.MeshEdges(new[] { 0, 1, 2, 2, 1, 3 }, 4, 2).Length == 4, "Per-mesh budget is strict even mid-triangle");
Check(TrackInspectorRules.MeshEdges(new[] { -1, 1, 2, 0, 1, 99 }, 4, 10).Length == 0, "Malformed triangles cannot reference outside vertex buffer");
Check(TrackInspectorRules.MeshEdges(new[] { 0, 0, 0 }, 1, 10).Length == 0, "Degenerate triangle discarded");
Check(TrackInspectorRules.MeshEdges(new[] { 0, 1 }, 2, 10).Length == 0, "Incomplete triangle ignored");
Check(TrackInspectorRules.MeshEdges(new[] { 0, 1, 2 }, 3, 0).Length == 0, "Zero budget allocates no edges");
Check(TrackInspectorRules.MeshEdges(new[] { 0, 1, 2 }, 0, 10).Length == 0, "Zero vertex buffer allocates no edges");
Console.WriteLine($"Track Inspector rules: {checks} assertions passed.");
