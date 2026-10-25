using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using UnityEngine;
using UnityEngine.Profiling;

namespace TK2.Customization;

// Explicit one-shot main-menu observation; never a recurring object search in a race.
internal static class MemoryCensus
{
    internal static void Capture()
    {
        var timer = Stopwatch.StartNew();
        var textures = new List<TextureRow>();
        var meshes = new List<MeshRow>();
        var cameras = new List<object>();
        var thumbIds = new HashSet<int>();
        int modCount = 0, thumbLinks = 0, objectErrors = 0;
        foreach (var mod in Resources.FindObjectsOfTypeAll<PTK_ModInfo>())
        {
            if (mod == null) continue;
            modCount++;
            if (mod.thumbnailsForObjects == null) continue;
            foreach (var link in mod.thumbnailsForObjects)
            {
                if (link?.spriteThumbnail == null || link.spriteThumbnail.texture == null) continue;
                thumbLinks++; thumbIds.Add(link.spriteThumbnail.texture.GetInstanceID());
            }
        }
        foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            if (t == null) continue;
            try { textures.Add(new TextureRow(t.GetInstanceID(), t.name, t.width, t.height, t.mipmapCount,
                t.format.ToString(), t.isReadable, Profiler.GetRuntimeMemorySizeLong(t), thumbIds.Contains(t.GetInstanceID()))); }
            catch { objectErrors++; }
        }
        foreach (var m in Resources.FindObjectsOfTypeAll<Mesh>())
        {
            if (m == null) continue;
            try { meshes.Add(new MeshRow(m.name, m.vertexCount, m.isReadable, Profiler.GetRuntimeMemorySizeLong(m))); }
            catch { objectErrors++; }
        }
        foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (c == null) continue;
            cameras.Add(new { name = c.name, enabled = c.enabled, active = c.gameObject.activeInHierarchy,
                targetTexture = c.targetTexture == null ? null : c.targetTexture.name, depth = c.depth });
        }
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var report = new { schema = 1, utc = DateTime.UtcNow, runtime = ToolkitVersion.Value,
            stage = "main_menu", mipLimit = QualitySettings.globalTextureMipmapLimit,
            textureCurrentBytes = Texture.currentTextureMemory, textureCount = textures.Count,
            textureObjectReportedBytes = textures.Sum(t => t.reportedBytes),
            readableTextures = textures.Count(t => t.readable), readableTextureReportedBytes = textures.Where(t => t.readable).Sum(t => t.reportedBytes),
            modCount, thumbLinks, uniqueModThumbnailTextures = thumbIds.Count,
            modThumbnailReportedBytes = textures.Where(t => t.modThumbnail).Sum(t => t.reportedBytes),
            meshCount = meshes.Count, meshReportedBytes = meshes.Sum(m => m.reportedBytes),
            activeCameras = Camera.allCamerasCount, cameras, objectErrors,
            workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64,
            unityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), unityReservedBytes = Profiler.GetTotalReservedMemoryLong(),
            il2cppUsedBytes = Profiler.GetMonoUsedSizeLong(), il2cppHeapBytes = Profiler.GetMonoHeapSizeLong(),
            largestTextures = textures.OrderByDescending(t => t.reportedBytes).Take(150).ToArray(),
            modThumbnails = textures.Where(t => t.modThumbnail).OrderByDescending(t => t.reportedBytes).Take(100).ToArray(),
            largestMeshes = meshes.OrderByDescending(m => m.reportedBytes).Take(40).ToArray(),
            captureMilliseconds = timer.Elapsed.TotalMilliseconds,
            limitation = "Object memory is Unity-reported allocation, not exclusively CPU residency or GPU memory. Totals overlap. Object width/height can remain original under mip limits. Snapshot work and wrapper allocations can briefly pause the menu. Thumbnail ownership covers loaded PTK_ModInfo sprite links only; downloader and other caches can contain additional images. No pixels or object references are retained." };
        string dir = Path.Combine(Paths.BepInExRootPath, "diagnostics"); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"tk2-memory-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report));
        Plugin.Instance?.Log.LogInfo($"Memory census: textures={textures.Count}, loaded texture bytes={report.textureCurrentBytes}, mod thumbnail bytes={report.modThumbnailReportedBytes}, active cameras={report.activeCameras}; {path}");
    }
    private sealed record TextureRow(int id, string name, int width, int height, int mips, string format, bool readable, long reportedBytes, bool modThumbnail);
    private sealed record MeshRow(string name, int vertices, bool readable, long reportedBytes);
}
