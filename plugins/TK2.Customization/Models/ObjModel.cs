using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace TK2.Customization;

// Wavefront geometry + basic diffuse MTL; no scripts, collision, animation or PBR conversion.
internal static class ObjModel
{
    internal sealed class Part
    {
        internal string Material = "";
        internal readonly List<Vector3> Vertices = new();
        internal readonly List<Vector3> Normals = new();
        internal readonly List<Vector2> UV = new();
        internal readonly List<int> Triangles = new();
        internal bool HasNormals = true;
    }
    internal sealed class Surface { internal Color Color = Color.white; internal string? Texture; }
    internal sealed class Data
    {
        internal readonly List<Part> Parts = new();
        internal readonly Dictionary<string, Surface> Surfaces = new(StringComparer.Ordinal);
    }

    internal static Data Parse(string path, bool mirrorX)
    {
        ModelPath.RequireSize(path, 32 * 1024 * 1024);
        var data = new Data(); var positions = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>();
        var parts = new Dictionary<string, Part>(); string material = ""; int totalVertices = 0;
        string folder = Path.GetDirectoryName(path)!;
        foreach (string source in File.ReadLines(path))
        {
            if (source.Length > 65536) throw new InvalidDataException("OBJ line exceeds 64 KB.");
            string line = source.Split('#')[0].Trim(); if (line.Length == 0) continue;
            string[] item = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            switch (item[0])
            {
                case "v":
                    Require(item, 4); positions.Add(new Vector3(Number(item[1]) * (mirrorX ? -1 : 1), Number(item[2]), Number(item[3])));
                    if (positions.Count > 250000) throw new InvalidDataException("OBJ exceeds 250,000 source vertices."); break;
                case "vn": Require(item, 4); normals.Add(new Vector3(Number(item[1]) * (mirrorX ? -1 : 1), Number(item[2]), Number(item[3]))); break;
                case "vt": Require(item, 3); uvs.Add(new Vector2(Number(item[1]), Number(item[2]))); break;
                case "usemtl": material = line.Length > 7 ? line.Substring(7).Trim() : ""; break;
                case "mtllib":
                    string mtl = ModelPath.Resolve(folder, line.Substring(6).Trim());
                    if (File.Exists(mtl)) ReadMaterials(mtl, data.Surfaces); break;
                case "f":
                    Require(item, 4);
                    if (!parts.TryGetValue(material, out Part? part))
                    {
                        if (parts.Count >= 64) throw new InvalidDataException("OBJ exceeds 64 materials.");
                        part = new Part { Material = material }; parts.Add(material, part); data.Parts.Add(part);
                    }
                    // Duplicate corner vertices so UV seams and distinct OBJ normals remain intact.
                    for (int i = 2; i < item.Length - 1; i++)
                    {
                        foreach (string corner in mirrorX ? new[] { item[1], item[i + 1], item[i] } : new[] { item[1], item[i], item[i + 1] })
                        {
                            if (++totalVertices > 1500000) throw new InvalidDataException("OBJ exceeds 500,000 triangles.");
                            string[] refs = corner.Split('/');
                            part.Triangles.Add(part.Vertices.Count); part.Vertices.Add(positions[Index(refs[0], positions.Count)]);
                            part.UV.Add(refs.Length > 1 && refs[1].Length > 0 ? uvs[Index(refs[1], uvs.Count)] : Vector2.zero);
                            bool hasNormal = refs.Length > 2 && refs[2].Length > 0;
                            part.Normals.Add(hasNormal ? normals[Index(refs[2], normals.Count)] : Vector3.zero); part.HasNormals &= hasNormal;
                        }
                    }
                    break;
            }
            if (normals.Count > 500000 || uvs.Count > 500000) throw new InvalidDataException("OBJ attribute count is too large.");
        }
        if (totalVertices == 0) throw new InvalidDataException("OBJ has no usable polygon faces.");
        return data;
    }

    internal static GameObject Create(Data data, Material prototype, List<UnityEngine.Object> owned)
    {
        var root = new GameObject("TK2 imported OBJ"); root.SetActive(false); owned.Add(root);
        long pixels = 0;
        foreach (Part part in data.Parts)
        {
            var mesh = new Mesh { name = "TK2 imported geometry", indexFormat = IndexFormat.UInt32 }; owned.Add(mesh);
            mesh.vertices = part.Vertices.ToArray(); mesh.uv = part.UV.ToArray(); mesh.triangles = part.Triangles.ToArray();
            if (part.HasNormals) mesh.normals = part.Normals.ToArray(); else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var material = new Material(prototype); owned.Add(material);
            Texture2D? texture = null; Color color = Color.white;
            if (data.Surfaces.TryGetValue(part.Material, out Surface? surface))
            {
                color = surface.Color;
                if (surface.Texture != null)
                {
                    ModelPath.RequireSize(surface.Texture, 16 * 1024 * 1024);
                    byte[] encoded = File.ReadAllBytes(surface.Texture);
                    var size = ImageSize(encoded);
                    pixels += (long)size.Width * size.Height;
                    if (pixels > 32000000) throw new InvalidDataException("OBJ diffuse textures exceed a combined 32 megapixels.");
                    texture = new Texture2D(2, 2); owned.Add(texture);
                    if (!ImageConversion.LoadImage(texture, encoded, true) || texture.width > 8192 || texture.height > 8192)
                        throw new InvalidDataException("Diffuse texture cannot be decoded or exceeds 8192 pixels.");
                }
            }
            SetDiffuse(material, color, texture);
            var child = new GameObject(string.IsNullOrEmpty(part.Material) ? "Mesh" : part.Material);
            child.transform.SetParent(root.transform, false); child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        return root;
    }

    // Validate encoded dimensions before Unity allocates the decoded texture.
    internal static (int Width, int Height) ImageSize(byte[] bytes)
    {
        long width = 0, height = 0;
        if (bytes.Length >= 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71)
        {
            width = ((long)bytes[16] << 24) | ((long)bytes[17] << 16) | ((long)bytes[18] << 8) | bytes[19];
            height = ((long)bytes[20] << 24) | ((long)bytes[21] << 16) | ((long)bytes[22] << 8) | bytes[23];
        }
        else if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216)
        {
            int offset = 2;
            while (offset + 3 < bytes.Length)
            {
                if (bytes[offset++] != 255) break;
                while (offset < bytes.Length && bytes[offset] == 255) offset++;
                if (offset >= bytes.Length) break;
                byte marker = bytes[offset++];
                if (marker is 0xD8 or >= 0xD0 and <= 0xD7 or 0x01) continue;
                if (marker is 0xD9 or 0xDA || offset + 1 >= bytes.Length) break;
                int length = (bytes[offset] << 8) | bytes[offset + 1];
                if (length < 2 || offset + length > bytes.Length) break;
                if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC) && length >= 7)
                { height = (bytes[offset + 3] << 8) | bytes[offset + 4]; width = (bytes[offset + 5] << 8) | bytes[offset + 6]; break; }
                offset += length;
            }
        }
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || width * height > 16000000)
            throw new InvalidDataException("Texture needs valid PNG/JPEG dimensions, at most 8192 per side and 16 megapixels.");
        return ((int)width, (int)height);
    }

    internal static void SetDiffuse(Material material, Color color, Texture? texture)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
    }

    private static void ReadMaterials(string path, Dictionary<string, Surface> surfaces)
    {
        ModelPath.RequireSize(path, 1024 * 1024); Surface? current = null;
        foreach (string source in File.ReadLines(path))
        {
            if (source.Length > 65536) throw new InvalidDataException("MTL line exceeds 64 KB.");
            string line = source.Split('#')[0].Trim(); string[] item = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (item.Length == 0) continue;
            if (item[0] == "newmtl")
            {
                if (surfaces.Count >= 64) throw new InvalidDataException("MTL exceeds 64 materials.");
                current = new Surface(); surfaces[line.Substring(6).Trim()] = current;
            }
            else if (current != null && item[0] == "Kd") { Require(item, 4); current.Color = new Color(Number(item[1]), Number(item[2]), Number(item[3]), 1); }
            else if (current != null && item[0] == "map_Kd")
            {
                string file = line.Substring(6).Trim().Trim('"');
                if (file.StartsWith("-", StringComparison.Ordinal)) continue; // MTL mapping options need an explicit converter.
                string texture = ModelPath.Resolve(Path.GetDirectoryName(path)!, file);
                string ext = Path.GetExtension(texture).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" && File.Exists(texture)) current.Texture = texture;
            }
        }
    }
    private static float Number(string text)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number) || Math.Abs(number) > 1000000)
            throw new InvalidDataException("OBJ contains invalid or unbounded numeric values.");
        return number;
    }
    private static int Index(string text, int count)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) || index == 0)
            throw new InvalidDataException("OBJ corner index is invalid.");
        long resolved = index > 0 ? (long)index - 1 : (long)count + index;
        if (resolved < 0 || resolved >= count) throw new InvalidDataException("OBJ corner references an absent vertex/attribute.");
        return (int)resolved;
    }
    private static void Require(string[] item, int count) { if (item.Length < count) throw new InvalidDataException("Incomplete OBJ/MTL record."); }
}
