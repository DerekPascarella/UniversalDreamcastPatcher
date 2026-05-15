using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Iso9660;
using File = System.IO.File;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public sealed class RedumpAssembleOptions
{
    // Folder with the GDromBuilder output (disc.gdi + tracks).
    public string RebuiltGdiFolder { get; set; } = string.Empty;
    // Source .cue. When set, output mirrors its track structure and T1+T2 are
    // taken from the source verbatim. When null/empty, the assembler runs in
    // GDI-source mode: track structure is derived from the rebuilt disc.gdi,
    // T1+T2 come from the rebuilt folder, and T2 is prepended with a synthesized
    // 150-sector silent pregap so the output matches Redump's T2 convention.
    public string? SourceCuePath { get; set; }
    // Where to write the .cue + tracks.
    public string OutputFolder { get; set; } = string.Empty;
    // Renames output tracks to "<TrackBaseName> (Track NN).bin". Empty = reuse source names.
    public string TrackBaseName { get; set; } = string.Empty;
    // Filename for the output .cue (no folder). Empty = derive from TrackBaseName or source.
    public string OutputCueFilename { get; set; } = string.Empty;
}

public sealed class RedumpAssembleResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProducedCuePath { get; set; } = string.Empty;
}

// Builds a Redump-style CUE/BIN from a rebuilt GDI. Track LBAs in the output
// match the rebuilt directory so files round-trip byte-perfect.
//
// Per-track rules:
//   - SD (T1, T2)    : source verbatim. UDP never rebuilds SD.
//   - HD audio       : rebuilt .raw verbatim.
//   - First HD data  : rebuilt .bin + 150-sector MODE1-zero post-gap.
//                      Fills the data-audio gap declared in disc.gdi.
//   - Later HD data  : 150-sector MODE1-zero pregap + rebuilt .bin.
//                      Fills the audio-data gap before track 5/13/etc.
public static class RedumpAssembler
{
    public static Task<RedumpAssembleResult> AssembleAsync(
        RedumpAssembleOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Assemble(options, progress, ct), ct);

    private static RedumpAssembleResult Assemble(RedumpAssembleOptions options, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new RedumpAssembleResult();

        if (!Directory.Exists(options.RebuiltGdiFolder))
        {
            result.ErrorMessage = $"Rebuilt GDI folder not found: {options.RebuiltGdiFolder}";
            return result;
        }
        bool gdiSourceMode = string.IsNullOrWhiteSpace(options.SourceCuePath);
        if (!gdiSourceMode && !File.Exists(options.SourceCuePath!))
        {
            result.ErrorMessage = $"Source CUE file not found: {options.SourceCuePath}";
            return result;
        }
        if (string.IsNullOrWhiteSpace(options.OutputFolder))
        {
            result.ErrorMessage = "Output folder is required.";
            return result;
        }

        Directory.CreateDirectory(options.OutputFolder);

        var rebuiltDiscGdi = Directory.EnumerateFiles(options.RebuiltGdiFolder, "*.gdi").FirstOrDefault();
        if (rebuiltDiscGdi == null)
        {
            result.ErrorMessage = $"Rebuilt GDI folder has no .gdi file: {options.RebuiltGdiFolder}";
            return result;
        }
        var rebuiltTracks = ParseDiscGdi(rebuiltDiscGdi);

        string sourceCueDir;
        string[] sourceCueLines;
        List<string> sourceTrackFiles;
        List<string> trackTypes;

        if (gdiSourceMode)
        {
            // No CUE input. Mirror the rebuilt GDI's structure instead.
            // disc.gdi track types: 4 = MODE1/2352, 0 = AUDIO.
            sourceCueDir = options.RebuiltGdiFolder;
            sourceCueLines = Array.Empty<string>();
            sourceTrackFiles = rebuiltTracks.Select(t => t.FileName).ToList();
            trackTypes = rebuiltTracks
                .Select(t => t.Type == 4 ? "MODE1/2352" : "AUDIO")
                .ToList();
        }
        else
        {
            sourceCueDir = Path.GetDirectoryName(Path.GetFullPath(options.SourceCuePath!))!;
            sourceCueLines = File.ReadAllLines(options.SourceCuePath!);
            (sourceTrackFiles, trackTypes) = ParseCueTracks(sourceCueLines);
        }

        if (sourceTrackFiles.Count < 3)
        {
            result.ErrorMessage = gdiSourceMode
                ? "Rebuilt GDI has fewer than 3 tracks; not a GD-ROM."
                : "Source CUE has fewer than 3 tracks; not a GD-ROM dump.";
            return result;
        }

        if (sourceTrackFiles.Count != rebuiltTracks.Count)
        {
            result.ErrorMessage = $"Track count mismatch: source has {sourceTrackFiles.Count} tracks, rebuilt GDI has {rebuiltTracks.Count}.";
            return result;
        }

        // HD data tracks (track 3 onward).
        var hdDataIndices = new List<int>();
        for (int i = 2; i < trackTypes.Count; i++)
            if (trackTypes[i] == "MODE1/2352") hdDataIndices.Add(i);

        // Redump DAT fast-path: if the rebuilt GDI's Track 1 CRC32 matches a
        // known Redump entry AND every track can be reconstructed byte-exact
        // against the DAT, write those bytes directly. Unpatched discs land
        // here; patched discs fail reconstruction on the modified track and
        // fall through to the hardcoded-150 path below. Same fall-through if
        // the disc isn't in the DAT at all (homebrew, prototypes, etc).
        RedumpDiscEntry? datEntry = null;
        var rebuiltT1Path = Path.Combine(options.RebuiltGdiFolder, rebuiltTracks[0].FileName);
        if (File.Exists(rebuiltT1Path))
        {
            uint t1Crc = Crc32.HashToUInt32(File.ReadAllBytes(rebuiltT1Path));
            datEntry = RedumpDatLookup.LookupByT1Crc32(t1Crc);
        }

        var outputTrackFiles = new string[sourceTrackFiles.Count];
        bool useCustomNames = !string.IsNullOrWhiteSpace(options.TrackBaseName);
        for (int i = 0; i < sourceTrackFiles.Count; i++)
        {
            outputTrackFiles[i] = useCustomNames
                ? $"{options.TrackBaseName} (Track {(i + 1):D2}).bin"
                : sourceTrackFiles[i];
        }
        var outputCueName = !string.IsNullOrWhiteSpace(options.OutputCueFilename)
            ? options.OutputCueFilename
            : useCustomNames
                ? $"{options.TrackBaseName}.cue"
                : Path.GetFileName(options.SourceCuePath);

        bool datUsable = datEntry != null && datEntry.Tracks.Count == sourceTrackFiles.Count;

        for (int i = 0; i < sourceTrackFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            int trackNum = i + 1;
            string destPath = Path.Combine(options.OutputFolder, outputTrackFiles[i]);

            // Per-track Redump DAT fast-path: try byte-exact reconstruction
            // against the DAT entry for this track. Succeeds for unmodified
            // tracks; falls through to the legacy hardcoded-150 path for
            // tracks whose bytes don't match the DAT (e.g. patched data
            // tracks). Patched-disc outputs thus retain byte-exact form for
            // unmodified tracks and legacy structure for modified tracks.
            if (datUsable)
            {
                var rebuilt = rebuiltTracks[i];
                string srcPath = Path.Combine(options.RebuiltGdiFolder, rebuilt.FileName);
                if (File.Exists(srcPath))
                {
                    byte[] gdiBytes = File.ReadAllBytes(srcPath);
                    var dat = datEntry!.Tracks[i];
                    bool isData = trackTypes[i] == "MODE1/2352";
                    byte[]? reconstructed = RedumpReconstructor.TryReconstruct(gdiBytes, isData, dat.Size, dat.Crc32, dat.Md5);
                    if (reconstructed != null)
                    {
                        File.WriteAllBytes(destPath, reconstructed);
                        continue;
                    }
                    byte[]? hybrid = RedumpReconstructor.BuildHybridLayout(gdiBytes, isData, dat.Size);
                    if (hybrid != null)
                    {
                        File.WriteAllBytes(destPath, hybrid);
                        continue;
                    }
                }
            }

            if (i < 2)
            {
                var sourceTrackPath = Path.Combine(sourceCueDir, sourceTrackFiles[i]);
                if (!File.Exists(sourceTrackPath))
                {
                    result.ErrorMessage = $"Source track {trackNum} BIN not found: {sourceTrackPath}";
                    return result;
                }

                if (gdiSourceMode && i == 0 && rebuiltTracks[0].SectorSize == 2048)
                {
                    // Source GDI uses ISO (2048-byte) data sectors for track 1. Redump
                    // expects 2352-byte raw MODE1 sectors with sync, header, and
                    // EDC/ECC, so transcode each 2048-byte user-data sector here.
                    long isoLen = new FileInfo(sourceTrackPath).Length;
                    if (isoLen % 2048 != 0)
                    {
                        result.ErrorMessage =
                            $"Source track 1 is not a whole number of 2048-byte sectors: {sourceTrackPath} ({isoLen} bytes).";
                        return result;
                    }
                    int sectorCount = (int)(isoLen / 2048);
                    using var input = File.OpenRead(sourceTrackPath);
                    using var output = File.Create(destPath);
                    var userData = new byte[2048];
                    for (int s = 0; s < sectorCount; s++)
                    {
                        int read = input.Read(userData, 0, 2048);
                        if (read != 2048)
                        {
                            result.ErrorMessage = $"Short read on source track 1 at sector {s}.";
                            return result;
                        }
                        var raw = IsoUtilities.ConvertSectorToRawMode1(userData, s);
                        output.Write(raw, 0, 2352);
                    }
                }
                else if (gdiSourceMode && i == 1)
                {
                    // GDI's track02.raw is the audio data with no pregap. Redump's
                    // Track 02 .bin starts with a 150-sector silent pregap so the
                    // CUE's INDEX 01 at 00:02:00 lands on the right offset. Prepend
                    // that pregap here.
                    using var output = File.Create(destPath);
                    var silentPregap = new byte[2352 * 150];
                    output.Write(silentPregap, 0, silentPregap.Length);
                    using var src = File.OpenRead(sourceTrackPath);
                    src.CopyTo(output);
                }
                else
                {
                    File.Copy(sourceTrackPath, destPath, overwrite: true);
                }
            }
            else if (trackTypes[i] == "AUDIO")
            {
                var rebuilt = rebuiltTracks[i];
                var rebuiltRawPath = Path.Combine(options.RebuiltGdiFolder, rebuilt.FileName);
                if (!File.Exists(rebuiltRawPath))
                {
                    result.ErrorMessage = $"Rebuilt audio track {trackNum} not found: {rebuiltRawPath}";
                    return result;
                }
                File.Copy(rebuiltRawPath, destPath, overwrite: true);
            }
            else // MODE1/2352
            {
                var rebuilt = rebuiltTracks[i];
                var rebuiltBinPath = Path.Combine(options.RebuiltGdiFolder, rebuilt.FileName);
                if (!File.Exists(rebuiltBinPath))
                {
                    result.ErrorMessage = $"Rebuilt data track {trackNum} not found: {rebuiltBinPath}";
                    return result;
                }

                bool isFirstHdData = i == hdDataIndices[0];
                if (isFirstHdData)
                {
                    int t3Sectors = (int)(new FileInfo(rebuiltBinPath).Length / 2352);
                    using var output = File.Create(destPath);
                    using (var rb = File.OpenRead(rebuiltBinPath)) rb.CopyTo(output);
                    var emptyUserData = new byte[2048];
                    for (int s = 0; s < 150; s++)
                    {
                        int lba = rebuilt.Lba + t3Sectors + s;
                        output.Write(IsoUtilities.ConvertSectorToRawMode1(emptyUserData, lba));
                    }
                }
                else
                {
                    using var output = File.Create(destPath);
                    var emptyUserData = new byte[2048];
                    for (int s = 0; s < 150; s++)
                    {
                        int lba = rebuilt.Lba - 150 + s;
                        output.Write(IsoUtilities.ConvertSectorToRawMode1(emptyUserData, lba));
                    }
                    using var rb = File.OpenRead(rebuiltBinPath);
                    rb.CopyTo(output);
                }
            }
        }

        var cuePath = Path.Combine(options.OutputFolder, outputCueName);
        File.WriteAllText(cuePath, BuildCueFile(sourceCueLines, outputTrackFiles, trackTypes, hdDataIndices));

        result.Success = true;
        result.ProducedCuePath = cuePath;
        return result;
    }

    private static (List<string> Files, List<string> Types) ParseCueTracks(string[] cueLines)
    {
        var files = new List<string>();
        var types = new List<string>();
        string? pendingFile = null;
        foreach (var raw in cueLines)
        {
            var line = raw.TrimStart();
            if (line.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
            {
                var first = line.IndexOf('"');
                var last = line.LastIndexOf('"');
                if (first >= 0 && last > first) pendingFile = line.Substring(first + 1, last - first - 1);
            }
            else if (line.StartsWith("TRACK ", StringComparison.OrdinalIgnoreCase) && pendingFile != null)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3) { files.Add(pendingFile); types.Add(parts[2]); pendingFile = null; }
            }
        }
        return (files, types);
    }

    // disc.gdi: line 1 = track count, then <track#> <lba> <type> <sector size> <filename> <offset>.
    private static List<RebuiltTrack> ParseDiscGdi(string path)
    {
        var result = new List<RebuiltTrack>();
        var lines = File.ReadAllLines(path);
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5
                && int.TryParse(parts[0], out int num)
                && int.TryParse(parts[1], out int lba)
                && int.TryParse(parts[2], out int type)
                && int.TryParse(parts[3], out int sectorSize))
            {
                result.Add(new RebuiltTrack(num, lba, type, sectorSize, parts[4]));
            }
        }
        return result;
    }

    private static string BuildCueFile(string[] sourceCueLines, string[] outputTrackFiles, List<string> trackTypes, List<int> hdDataIndices)
    {
        var sb = new StringBuilder();
        int totalTracks = outputTrackFiles.Length;
        // Empty sourceCueLines means GDI-source mode. Emit the REM
        // area markers unconditionally (a GDI doesn't carry that hint).
        bool sourceHasSdRem = sourceCueLines.Length == 0
            || sourceCueLines.Any(l => l.TrimStart().StartsWith("REM SINGLE-DENSITY", StringComparison.OrdinalIgnoreCase));
        bool sourceHasHdRem = sourceCueLines.Length == 0
            || sourceCueLines.Any(l => l.TrimStart().StartsWith("REM HIGH-DENSITY", StringComparison.OrdinalIgnoreCase));
        int firstHdDataIndex = hdDataIndices.Count > 0 ? hdDataIndices[0] : -1;

        for (int i = 0; i < totalTracks; i++)
        {
            int t = i + 1;
            if (i == 0 && sourceHasSdRem) sb.AppendLine("REM SINGLE-DENSITY AREA");
            if (i == 2 && sourceHasHdRem) sb.AppendLine("REM HIGH-DENSITY AREA");
            sb.AppendLine($"FILE \"{outputTrackFiles[i]}\" BINARY");
            string trackType = trackTypes[i];
            sb.AppendLine($"  TRACK {t:D2} {trackType}");

            if (trackType == "AUDIO")
            {
                if (i < 2)
                {
                    // T2 SD audio: source verbatim has the 150-sec pregap inside.
                    sb.AppendLine("    INDEX 00 00:00:00");
                    sb.AppendLine("    INDEX 01 00:02:00");
                }
                else
                {
                    // HD audio: rebuilt .raw, no pregap inside.
                    sb.AppendLine("    INDEX 01 00:00:00");
                }
            }
            else if (trackType == "MODE1/2352")
            {
                if (i == firstHdDataIndex || i < 2)
                {
                    sb.AppendLine("    INDEX 01 00:00:00");
                }
                else
                {
                    // Later HD data: synthesized 150-sector pregap.
                    sb.AppendLine("    INDEX 00 00:00:00");
                    sb.AppendLine("    INDEX 01 00:02:00");
                }
            }
            else
            {
                sb.AppendLine("    INDEX 01 00:00:00");
            }
        }
        return sb.ToString();
    }

    private readonly record struct RebuiltTrack(int Num, int Lba, int Type, int SectorSize, string FileName);
}
