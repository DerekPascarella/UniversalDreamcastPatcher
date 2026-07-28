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
    public const string NonContiguousGdiError =
        "This GDI's track layout cannot be represented as CUE/BIN.\n\n" +
        "Converting this disc image to CHD is still supported.";

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

        // Redump DAT lookup: match the rebuilt GDI's Track 1 CRC32 against the
        // embedded Redump database. When a disc matches, reconstruction is
        // all-or-nothing: either every track reconstructs byte-exact (and the
        // output is a true Redump dump with a CUE derived from the disc's real
        // track positions), or the whole disc falls back to the legacy packing
        // below. Redump and legacy packings put the 150/225-sector gaps inside
        // different track files, so mixing forms within one disc shifts every
        // later track away from its TOC position and the image will not boot.
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
        if (datUsable && TryAssembleFullRedump(options, rebuiltTracks, trackTypes, outputTrackFiles, outputCueName, datEntry!, hdDataIndices, ct))
        {
            result.Success = true;
            result.ProducedCuePath = Path.Combine(options.OutputFolder, outputCueName!);
            return result;
        }

        if (gdiSourceMode && hdDataIndices.Count > 0)
        {
            // The legacy packing below places tracks by concatenating files,
            // so every data track has to sit exactly where the GDI says it
            // does. A shrunk track 3 or a relocated data track lands somewhere
            // else in the output while the filesystem still points at the old
            // location, so bail out here.
            string? guardError = CheckHdLayoutIsContiguous(options.RebuiltGdiFolder, rebuiltTracks, trackTypes, hdDataIndices);
            if (guardError != null)
            {
                result.ErrorMessage = guardError;
                return result;
            }
        }

        for (int i = 0; i < sourceTrackFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            int trackNum = i + 1;
            string destPath = Path.Combine(options.OutputFolder, outputTrackFiles[i]);

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
                        var raw = IsoUtilities.ConvertSectorToRawMode1(userData, rebuiltTracks[0].Lba + s);
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
                bool isoForm = rebuilt.SectorSize == 2048;
                int sourceSectorSize = isoForm ? 2048 : 2352;
                long trackLength = new FileInfo(rebuiltBinPath).Length;
                if (trackLength % sourceSectorSize != 0)
                {
                    result.ErrorMessage =
                        $"Rebuilt data track {trackNum} is not a whole number of {sourceSectorSize}-byte sectors: {rebuiltBinPath} ({trackLength} bytes).";
                    return result;
                }
                int trackSectors = (int)(trackLength / sourceSectorSize);

                using var output = File.Create(destPath);
                var emptyUserData = new byte[2048];

                if (!isFirstHdData)
                {
                    for (int s = 0; s < 150; s++)
                    {
                        int lba = rebuilt.Lba - 150 + s;
                        output.Write(IsoUtilities.ConvertSectorToRawMode1(emptyUserData, lba));
                    }
                }

                using (var rb = File.OpenRead(rebuiltBinPath))
                {
                    if (isoForm)
                    {
                        // The CUE declares MODE1/2352, so 2048-byte source
                        // sectors get rebuilt as raw sectors with their real
                        // absolute addresses.
                        var userData = new byte[2048];
                        for (int s = 0; s < trackSectors; s++)
                        {
                            int read = rb.Read(userData, 0, 2048);
                            if (read != 2048)
                            {
                                result.ErrorMessage = $"Short read on rebuilt data track {trackNum} at sector {s}.";
                                return result;
                            }
                            output.Write(IsoUtilities.ConvertSectorToRawMode1(userData, rebuilt.Lba + s));
                        }
                    }
                    else
                    {
                        rb.CopyTo(output);
                    }
                }

                if (isFirstHdData)
                {
                    for (int s = 0; s < 150; s++)
                    {
                        int lba = rebuilt.Lba + trackSectors + s;
                        output.Write(IsoUtilities.ConvertSectorToRawMode1(emptyUserData, lba));
                    }
                }
            }
        }

        var cuePath = Path.Combine(options.OutputFolder, outputCueName);
        File.WriteAllText(cuePath, BuildCueFile(sourceCueLines, outputTrackFiles, trackTypes, hdDataIndices));

        result.Success = true;
        result.ProducedCuePath = cuePath;
        return result;
    }

    private const int HighDensityAreaLba = 45000;

    // All-or-nothing Redump reconstruction. Returns true only when every track
    // reconstructed byte-exact against the DAT entry and the computed CUE was
    // written. On any failure it removes whatever it wrote and returns false so
    // the caller falls back to the legacy packing for the whole disc. A partial
    // set must never ship because Redump-form and legacy-form files put the gap
    // sectors in different track files.
    private static bool TryAssembleFullRedump(
        RedumpAssembleOptions options,
        List<RebuiltTrack> rebuiltTracks,
        List<string> trackTypes,
        string[] outputTrackFiles,
        string? outputCueName,
        RedumpDiscEntry datEntry,
        List<int> hdDataIndices,
        CancellationToken ct)
    {
        // The computed CUE places each track at its real TOC position, so make
        // sure those positions are derivable before reconstructing anything.
        string? cueText = BuildComputedRedumpCue(rebuiltTracks, trackTypes, outputTrackFiles, datEntry);
        if (cueText == null) return false;

        // HD data tracks first: those carry the patched filesystem, so patched
        // discs bail out here immediately instead of after reconstructing all
        // of the audio tracks.
        var order = hdDataIndices
            .Concat(Enumerable.Range(0, rebuiltTracks.Count).Where(i => !hdDataIndices.Contains(i)));

        var written = new List<string>();
        try
        {
            foreach (int i in order)
            {
                ct.ThrowIfCancellationRequested();

                var srcPath = Path.Combine(options.RebuiltGdiFolder, rebuiltTracks[i].FileName);
                if (!File.Exists(srcPath))
                {
                    DeleteFiles(written);
                    return false;
                }

                // Some GDIs store a track's pregap sectors at the end of the
                // previous track's file. Hand the reconstructor that tail so
                // it can pull the missing lead-in from there. Only applies to
                // HD tracks that have another HD track in front of them.
                byte[]? precedingTail = null;
                if (i >= 3)
                {
                    var prevPath = Path.Combine(options.RebuiltGdiFolder, rebuiltTracks[i - 1].FileName);
                    if (File.Exists(prevPath))
                        precedingTail = ReadFileTail(prevPath, 300L * 2352);
                }

                var dat = datEntry.Tracks[i];
                bool isData = trackTypes[i] == "MODE1/2352";
                byte[]? reconstructed = RedumpReconstructor.TryReconstruct(
                    File.ReadAllBytes(srcPath), isData, dat.Size, dat.Crc32, dat.Md5, precedingTail);
                if (reconstructed == null)
                {
                    DeleteFiles(written);
                    return false;
                }

                var destPath = Path.Combine(options.OutputFolder, outputTrackFiles[i]);
                File.WriteAllBytes(destPath, reconstructed);
                written.Add(destPath);
            }
        }
        catch (OperationCanceledException)
        {
            DeleteFiles(written);
            throw;
        }

        File.WriteAllText(Path.Combine(options.OutputFolder, outputCueName!), cueText);
        return true;
    }

    // CUE for a full byte-exact Redump set. Redump embeds each gap inside the
    // following track's file, so INDEX 01 of every track is derived from the
    // disc's TOC: track LBA (from disc.gdi) minus the track file's cumulative
    // start sector. For a typical GD-ROM this works out to 00:02:00 on audio
    // tracks and 00:03:00 on a final data track. Returns null when any track's
    // numbers are inconsistent, in which case the caller must use the legacy
    // packing instead.
    private static string? BuildComputedRedumpCue(
        List<RebuiltTrack> rebuiltTracks,
        List<string> trackTypes,
        string[] outputTrackFiles,
        RedumpDiscEntry datEntry)
    {
        var sb = new StringBuilder();
        long cumulativeStart = 0;
        for (int i = 0; i < rebuiltTracks.Count; i++)
        {
            if (i == 0) sb.AppendLine("REM SINGLE-DENSITY AREA");
            if (i == 2)
            {
                sb.AppendLine("REM HIGH-DENSITY AREA");
                cumulativeStart = HighDensityAreaLba;
            }

            long size = datEntry.Tracks[i].Size;
            if (size <= 0 || size % 2352 != 0) return null;
            long sectors = size / 2352;

            long pregap = rebuiltTracks[i].Lba - cumulativeStart;
            if (pregap < 0 || pregap >= sectors) return null;

            sb.AppendLine($"FILE \"{outputTrackFiles[i]}\" BINARY");
            sb.AppendLine($"  TRACK {i + 1:D2} {trackTypes[i]}");
            if (pregap > 0)
            {
                sb.AppendLine("    INDEX 00 00:00:00");
                sb.AppendLine($"    INDEX 01 {ToMsf(pregap)}");
            }
            else
            {
                sb.AppendLine("    INDEX 01 00:00:00");
            }

            cumulativeStart += sectors;
        }
        return sb.ToString();
    }

    private static string ToMsf(long sectors) =>
        $"{sectors / (75 * 60):D2}:{sectors / 75 % 60:D2}:{sectors % 75:D2}";

    private static byte[] ReadFileTail(string path, long maxBytes)
    {
        using var fs = File.OpenRead(path);
        long count = Math.Min(maxBytes, fs.Length);
        fs.Seek(-count, SeekOrigin.End);
        var buf = new byte[count];
        fs.ReadExactly(buf, 0, (int)count);
        return buf;
    }

    private static void DeleteFiles(List<string> files)
    {
        foreach (var f in files)
        {
            try { File.Delete(f); } catch { /* best effort */ }
        }
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

    private static string? CheckHdLayoutIsContiguous(
        string gdiFolder, List<RebuiltTrack> rebuiltTracks, List<string> trackTypes, List<int> hdDataIndices)
    {
        int firstHd = hdDataIndices[0];
        if (rebuiltTracks[firstHd].Lba != HighDensityAreaLba)
            return NonContiguousGdiError;

        long emittedLba = HighDensityAreaLba;
        for (int i = firstHd; i < rebuiltTracks.Count; i++)
        {
            var track = rebuiltTracks[i];
            var trackPath = Path.Combine(gdiFolder, track.FileName);
            if (!File.Exists(trackPath)) return null;

            int sectorSize = track.SectorSize > 0 ? track.SectorSize : 2352;
            long fileSectors = new FileInfo(trackPath).Length / sectorSize;

            if (trackTypes[i] == "MODE1/2352")
            {
                bool isFirstHdData = i == firstHd;
                long expectedLba = isFirstHdData ? HighDensityAreaLba : emittedLba + 150;
                if (track.Lba != expectedLba)
                    return NonContiguousGdiError;
                emittedLba = expectedLba + fileSectors + (isFirstHdData ? 150 : 0);
            }
            else
            {
                emittedLba += fileSectors;
            }
        }
        return null;
    }

    // disc.gdi: line 1 = track count, then <track#> <lba> <type> <sector size> <filename> <offset>.
    // GdiTrackLine handles quoted filenames, so GDIs straight out of chdman
    // (which quote track names containing spaces) parse correctly here too.
    private static List<RebuiltTrack> ParseDiscGdi(string path)
    {
        var result = new List<RebuiltTrack>();
        var lines = File.ReadAllLines(path);
        foreach (var line in lines.Skip(1))
        {
            if (GdiTrackLine.TryParse(line, out var track))
            {
                result.Add(new RebuiltTrack(track.TrackNumber, track.Lba, track.Type, track.SectorSize, track.FileName));
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
