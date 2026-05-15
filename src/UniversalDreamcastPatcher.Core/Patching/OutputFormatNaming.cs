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

    public static string ChdFileName(string baseName) => $"{baseName}.chd";
    public static string CueFileName(string baseName) => $"{baseName}.cue";
}
