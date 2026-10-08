using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK2.Customization;

internal static class MirrorMesh
{
    private const int MaxBufferBytes = 64 * 1024 * 1024;
    internal static Vector3 Point(Vector3 v) => new(-v.x, v.y, v.z);
    internal static Quaternion Rotation(Quaternion q)
    { var r = MirrorGeometry.Rotation(q.x, q.y, q.z, q.w); return new Quaternion(r.X, r.Y, r.Z, r.W); }
    internal static Bounds Bounds(Bounds b) => new(Point(b.center), b.size);

    internal static Mesh Clone(Mesh source)
    {
        if (!source.HasVertexAttribute(VertexAttribute.Position)) throw new NotSupportedException("Mesh has no position channel.");
        if (source.vertexCount > 2_000_000 || source.subMeshCount > 256 || source.blendShapeCount != 0 || source.bindposeCount != 0)
            throw new NotSupportedException($"Unsupported animated or oversized mesh: {source.name}.");
        var ranges = new List<(int Start, int End)>();
        for (int i = 0; i < source.subMeshCount; i++)
        {
            if (source.GetTopology(i) != MeshTopology.Triangles)
                throw new NotSupportedException($"Non-triangle mesh: {source.name}.");
            var sub = source.GetSubMesh(i);
            if (sub.indexStart < 0 || sub.indexCount < 0 || sub.indexCount % 3 != 0) throw new NotSupportedException("Invalid triangle submesh range.");
            int end = checked(sub.indexStart + sub.indexCount);
            foreach (var range in ranges) if (sub.indexStart < range.End && end > range.Start)
                throw new NotSupportedException("Overlapping submesh index ranges cannot be mirrored safely.");
            ranges.Add((sub.indexStart, end));
        }
        if (source.isReadable) return CloneReadable(source);
        if (!SystemInfo.supportsAsyncGPUReadback)
            throw new NotSupportedException($"GPU readback unavailable for non-readable mesh: {source.name}.");
        return CloneGpu(source);
    }

    private static Mesh CloneReadable(Mesh source)
    {
        var mesh = UnityEngine.Object.Instantiate(source);
        try
        {
            mesh.name = source.name + " [TK2 Mirror]";
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = Point(vertices[i]);
            mesh.vertices = vertices;
            var normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = Point(normals[i]);
            mesh.normals = normals;
            var tangents = mesh.tangents;
            for (int i = 0; i < tangents.Length; i++)
            { var t = tangents[i]; t.x = -t.x; t.w = -t.w; tangents[i] = t; }
            mesh.tangents = tangents;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var indices = source.GetIndices(i, false);
                if (indices.Length % 3 != 0) throw new NotSupportedException("Incomplete triangle mesh.");
                for (int p = 0; p < indices.Length; p += 3)
                { int v = indices[p + 1]; indices[p + 1] = indices[p + 2]; indices[p + 2] = v; }
                mesh.SetIndices(indices, MeshTopology.Triangles, i, false, checked((int)source.GetBaseVertex(i)));
            }
            mesh.bounds = Bounds(source.bounds);
            return mesh;
        }
        catch { UnityEngine.Object.Destroy(mesh); throw; }
    }

    private static byte[] Read(GraphicsBuffer buffer)
    {
        int length = checked(buffer.count * buffer.stride);
        if (length < 1 || length > MaxBufferBytes) throw new NotSupportedException("Mesh GPU buffer exceeds the 64 MiB limit.");
        var request = AsyncGPUReadback.Request(buffer, (Il2CppSystem.Action<AsyncGPUReadbackRequest>?)null);
        // Unity exposes no cancellable timeout here. This can stall on a faulty driver;
        // only run during loading, before any scene mutation, and document that limitation.
        request.WaitForCompletion();
        if (!request.done || request.hasError) throw new InvalidOperationException("Mesh GPU readback failed.");
        var data = request.GetData<byte>();
        if (data.Length != length) throw new InvalidOperationException("Mesh GPU readback size changed.");
        var bytes = new byte[length];
        var copy = data.ToArray();
        for (int i = 0; i < length; i++) bytes[i] = copy[i];
        return bytes; // NativeArray is owned by the request; do not dispose its memory.
    }

    private static Mesh CloneGpu(Mesh source)
    {
        var streams = new Dictionary<int, byte[]>();
        for (int s = 0; s < source.vertexBufferCount; s++)
        { var buffer = source.GetVertexBuffer(s); try { streams[s] = Read(buffer); } finally { buffer.Dispose(); } }
        foreach (var attr in new[] { VertexAttribute.Position, VertexAttribute.Normal, VertexAttribute.Tangent })
        {
            if (!source.HasVertexAttribute(attr)) continue;
            int stream = source.GetVertexAttributeStream(attr), stride = source.GetVertexBufferStride(stream);
            int dimension = source.GetVertexAttributeDimension(attr), offset = source.GetVertexAttributeOffset(attr);
            var format = source.GetVertexAttributeFormat(attr);
            if (dimension < (attr == VertexAttribute.Tangent ? 4 : 3)) throw new NotSupportedException("Unsupported geometry channel dimension.");
            if (attr == VertexAttribute.Position && format != VertexAttributeFormat.Float32 && format != VertexAttributeFormat.Float16)
                throw new NotSupportedException("Packed mesh positions are not supported.");
            MirrorGeometry.NegateChannel(streams[stream], source.vertexCount, stride, offset, (int)format);
            if (attr == VertexAttribute.Tangent)
            {
                int size = format == VertexAttributeFormat.Float32 ? 4 : format == VertexAttributeFormat.SNorm8 ? 1 : 2;
                MirrorGeometry.NegateChannel(streams[stream], source.vertexCount, stride, offset + size * 3, (int)format);
            }
        }
        byte[] indices;
        var indexBuffer = source.GetIndexBuffer();
        try { indices = Read(indexBuffer); } finally { indexBuffer.Dispose(); }
        int indexSize = source.indexFormat == IndexFormat.UInt16 ? 2 : 4;
        for (int i = 0; i < source.subMeshCount; i++)
        { var sub = source.GetSubMesh(i); MirrorGeometry.ReverseTriangles(indices, sub.indexStart, sub.indexCount, indexSize); }
        var mesh = new Mesh { name = source.name + " [TK2 Mirror GPU]", indexFormat = source.indexFormat };
        try
        {
            mesh.SetVertexBufferParams(source.vertexCount, source.GetVertexAttributes());
            foreach (var pair in streams)
                mesh.SetVertexBufferData<byte>(new Il2CppStructArray<byte>(pair.Value), 0, 0, pair.Value.Length, pair.Key, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetIndexBufferParams(indices.Length / indexSize, source.indexFormat);
            mesh.SetIndexBufferData<byte>(new Il2CppStructArray<byte>(indices), 0, 0, indices.Length, MeshUpdateFlags.DontRecalculateBounds);
            mesh.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++)
            { var sub = source.GetSubMesh(i); sub.bounds = Bounds(sub.bounds); mesh.SetSubMesh(i, sub, MeshUpdateFlags.DontRecalculateBounds); }
            mesh.bounds = Bounds(source.bounds);
            return mesh;
        }
        catch { UnityEngine.Object.Destroy(mesh); throw; }
    }
}
