using System;
using System.IO;

namespace TK2.Customization;

internal static class ModelPath
{
    // Only game-local imported data. Reject junctions as well as textual traversal.
    internal static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Select a model imported into BepInEx/models.");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string path = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Model path leaves its imported model folder.");
        for (string? part = path; part != null && part.Length >= fullRoot.Length; part = Path.GetDirectoryName(part))
            if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Model folders must not contain links or junctions.");
        return path;
    }

    internal static void RequireSize(string path, long maximum)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0 || info.Length > maximum)
            throw new InvalidDataException($"Missing, empty, or oversized model asset: {Path.GetFileName(path)}.");
    }
}
