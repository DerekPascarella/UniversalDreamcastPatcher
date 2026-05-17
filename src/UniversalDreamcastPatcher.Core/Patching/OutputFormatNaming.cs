using System.IO;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

// Output-disc-image folder and file names. Shared by PatchApplier and
// DiscImageConverter.
public static class OutputFormatNaming
{
    public static string Suffix(OutputDiscImageFormat format) => format switch
    {
        OutputDiscImageFormat.CueBin => "[CUE-BIN]",
        OutputDiscImageFormat.ChdGdRom => "[CHD]",
        _ => "[GDI]",
    };

    // Builds "<parent>/<baseName> <suffix>".
    public static string UserFolderFor(string parentFolder, string baseName, OutputDiscImageFormat format)
        => Path.Combine(parentFolder, $"{baseName} {Suffix(format)}");

    // Same as UserFolderFor, but appends " [2]", " [3]", ... if the folder already exists.
    public static string NextAvailableUserFolderFor(string parentFolder, string baseName, OutputDiscImageFormat format)
    {
        var basePath = UserFolderFor(parentFolder, baseName, format);
        if (!Directory.Exists(basePath)) return basePath;
        for (int n = 2; ; n++)
        {
            var candidate = $"{basePath} [{n}]";
            if (!Directory.Exists(candidate)) return candidate;
        }
    }

    public static string ChdFileName(string baseName) => $"{baseName}.chd";
    public static string CueFileName(string baseName) => $"{baseName}.cue";
}
