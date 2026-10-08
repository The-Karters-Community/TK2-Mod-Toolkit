using System;
using System.Collections.Generic;

namespace TK2.Customization;

// Pure rules shared by the overlay and its standalone tests. No game hooks.
internal static class TrackInspectorRules
{
    internal enum Kind { None, Wall, ConditionalRespawn, AlwaysRespawn, KillLinked, OtherTrigger }
    internal static bool InMask(int mask, int layer) => layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;
    internal static Kind Classify(int layer, int walls, int flatRespawn, int alwaysRespawn, bool killLinked, bool otherTrigger)
    {
        if (killLinked) return Kind.KillLinked;
        if (InMask(alwaysRespawn, layer)) return Kind.AlwaysRespawn;
        if (InMask(flatRespawn, layer)) return Kind.ConditionalRespawn;
        if (InMask(walls, layer)) return Kind.Wall;
        return otherTrigger ? Kind.OtherTrigger : Kind.None;
    }

    // Reject malformed indices; return unique undirected edges, never exceeding the budget.
    internal static int[] MeshEdges(int[] triangles, int vertexCount, int maxEdges)
    {
        if (vertexCount < 1 || maxEdges < 1) return Array.Empty<int>();
        var seen = new HashSet<ulong>();
        var edges = new List<int>();
        for (int i = 0; i + 2 < triangles.Length && edges.Count / 2 < maxEdges; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount) continue;
            Add(a, b); Add(b, c); Add(c, a);
        }
        return edges.ToArray();
        void Add(int a, int b)
        {
            if (a == b || edges.Count / 2 >= maxEdges) return;
            uint low = (uint)Math.Min(a, b), high = (uint)Math.Max(a, b);
            if (!seen.Add(((ulong)low << 32) | high)) return;
            edges.Add(a); edges.Add(b);
        }
    }
}
