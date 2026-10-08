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

    // Keep only the outer boundary and visibly sharp creases. Drawing every
    // triangulation edge turns a collision mesh into dense visual noise.
    internal static int[] FeatureEdges(int[] triangles, UnityEngine.Vector3[] vertices, int maxEdges, float creaseDot = 0.78f)
    {
        if (vertices == null || vertices.Length < 3 || triangles == null || maxEdges < 1) return Array.Empty<int>();
        var bounds = new UnityEngine.Bounds(vertices[0], UnityEngine.Vector3.zero);
        foreach (var vertex in vertices) bounds.Encapsulate(vertex);
        double inverseWeldTolerance = 1.0 / Math.Max(1e-5f, bounds.size.magnitude * 1e-6f);
        var positionIds = new Dictionary<(long X, long Y, long Z), int>();
        var welded = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var point = vertices[i];
            var key = ((long)Math.Round(point.x * inverseWeldTolerance),
                (long)Math.Round(point.y * inverseWeldTolerance),
                (long)Math.Round(point.z * inverseWeldTolerance));
            if (!positionIds.TryGetValue(key, out int id))
            { id = positionIds.Count; positionIds.Add(key, id); }
            welded[i] = id;
        }
        var lookup = new Dictionary<ulong, int>();
        var edges = new List<(int A, int B, UnityEngine.Vector3 Normal, int Faces, bool Sharp)>();
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length) continue;
            var cross = UnityEngine.Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (cross.sqrMagnitude < 1e-10f) continue;
            Add(a, b, cross.normalized); Add(b, c, cross.normalized); Add(c, a, cross.normalized);
        }
        var result = new List<int>(Math.Min(maxEdges, edges.Count) * 2);
        foreach (var edge in edges)
        {
            if (edge.Faces != 1 && !edge.Sharp) continue;
            result.Add(edge.A); result.Add(edge.B);
            if (result.Count / 2 >= maxEdges) break;
        }
        return result.ToArray();

        void Add(int a, int b, UnityEngine.Vector3 normal)
        {
            if (welded[a] == welded[b]) return;
            uint low = (uint)Math.Min(welded[a], welded[b]), high = (uint)Math.Max(welded[a], welded[b]);
            ulong key = ((ulong)low << 32) | high;
            if (lookup.TryGetValue(key, out int index))
            {
                var prior = edges[index];
                edges[index] = (prior.A, prior.B, prior.Normal, prior.Faces + 1,
                    prior.Sharp || UnityEngine.Vector3.Dot(prior.Normal, normal) < creaseDot);
                return;
            }
            lookup[key] = edges.Count;
            edges.Add((a, b, normal, 1, false));
        }
    }
}
