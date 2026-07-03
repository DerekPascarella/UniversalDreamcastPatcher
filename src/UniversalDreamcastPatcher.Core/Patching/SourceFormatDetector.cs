using System;
using System.IO;
using UniversalDreamcastPatcher.Core.Conversion;

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
                return GdiConverter.IsGdRomCue(sourcePath)
                    ? DetectedSourceFormat.CueBin
                    : DetectedSourceFormat.Unknown;
            case ".chd":
                return DetectChdContents(sourcePath);
            default:
                return DetectedSourceFormat.Unknown;
        }
    }

    public const string LegacyRedumpGdiError =
        "This disc image is in Redump's old short-lived GDI format.\n\n" +
        "Universal Dreamcast Patcher supports either modern Redump CUE/BIN or TOSEC GDI disc images (both of which can be compressed as CHD).";

    // Redump's old GDI format keeps the pregap inside each track file, so its
    // track 2 LBA equals track 1's sector count exactly. A normal GDI puts
    // track 2 at track 1's sector count plus the 150-sector pregap.
    public static bool IsLegacyRedumpGdi(string gdiPath)
    {
        try
        {
            var lines = File.ReadAllLines(gdiPath);
            if (lines.Length < 3) return false;

            if (!GdiTrackLine.TryParse(lines[1], out var track1)) return false;
            if (!GdiTrackLine.TryParse(lines[2], out var track2)) return false;
            if (track1.TrackNumber != 1 || track2.TrackNumber != 2) return false;
            if (track1.SectorSize <= 0) return false;

            var gdiFolder = Path.GetDirectoryName(gdiPath) ?? string.Empty;
            var track1File = new FileInfo(Path.Combine(gdiFolder, track1.FileName));
            if (!track1File.Exists) return false;

            long track1Sectors = track1File.Length / track1.SectorSize;
            return track2.Lba == track1Sectors;
        }
        catch
        {
            // Unreadable .gdi - let the normal loading paths report it.
            return false;
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
