using System;
using System.IO;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public enum DetectedSourceFormat
{
    Unknown,
    Gdi,
    CueBin,
    ChdContainingGdi,
    ChdContainingCueBin,
}

// Detects what's behind a source path. For .chd, peeks inside to tell GD-ROM
// from CD-ROM, since that determines which output formats are allowed.
public static class SourceFormatDetector
{
    public static DetectedSourceFormat Detect(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return DetectedSourceFormat.Unknown;

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        switch (ext)
        {
            case ".gdi":
                return DetectedSourceFormat.Gdi;
            case ".cue":
                return DetectedSourceFormat.CueBin;
            case ".chd":
                return DetectChdContents(sourcePath);
            default:
                return DetectedSourceFormat.Unknown;
        }
    }

    private static DetectedSourceFormat DetectChdContents(string chdPath)
    {
        try
        {
            using var reader = new ChdReader(chdPath);
            return reader.IsGdRom
                ? DetectedSourceFormat.ChdContainingGdi
                : DetectedSourceFormat.ChdContainingCueBin;
        }
        catch
        {
            // Unreadable CHD - let the UI decide how to handle it.
            return DetectedSourceFormat.Unknown;
        }
    }
}
