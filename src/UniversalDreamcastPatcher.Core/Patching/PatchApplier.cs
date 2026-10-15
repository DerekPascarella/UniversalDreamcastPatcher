using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Gdrom;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public enum OutputDiscImageFormat
{
    Gdi,
    CueBin,
    // GD-ROM-style CHD (libchdw is fed a .gdi).
    ChdGdRom,
}

public sealed class PatchApplyOptions
{
    public string SourceDiscImagePath { get; set; } = string.Empty;
    public string DcpPatchPath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public OutputDiscImageFormat OutputFormat { get; set; } = OutputDiscImageFormat.Gdi;
    /// <summary>
    /// Called on the worker thread for ambiguous DCP track data. Return true to preserve
    /// IP.BIN, false to update its track entries, or throw OperationCanceledException to cancel.
    /// </summary>
    public Func<Task<bool>>? PreserveCustomIpBinAsync { get; set; }
}

public sealed class PatchApplyResult
{
    public bool Success { get; set; }
    public bool Canceled { get; set; }
    public string? ErrorMessage { get; set; }
    // The folder shown to the user. Holds the .gdi or .cue plus track files.
    public string ProducedOutputFolder { get; set; } = string.Empty;
    public int FilesPatched { get; set; }
    public int FilesOverwritten { get; set; }
    public int FilesAdded { get; set; }
}

public static class PatchApplier
{
    public static Task<PatchApplyResult> ApplyAsync(
        PatchApplyOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Apply(options, progress, ct), ct);

    private static PatchApplyResult Apply(PatchApplyOptions options, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new PatchApplyResult();

        if (!File.Exists(options.SourceDiscImagePath))
        {
            result.ErrorMessage =
                "The source disc image could not be found.\n\n" +
                "Please select a valid .gdi, .cue, or .chd file and try again.";
            return result;
        }
        if (!File.Exists(options.DcpPatchPath))
        {
            result.ErrorMessage =
                "The patch file could not be found.\n\n" +
                "Please select a valid .dcp file and try again.";
            return result;
        }
        if (string.IsNullOrWhiteSpace(options.OutputFolder) || !Directory.Exists(options.OutputFolder))
        {
            result.ErrorMessage =
                "The selected output folder does not exist.\n\n" +
                "Please choose a different folder and try again.";
            return result;
        }

        // CUE/BIN output is produced via RedumpAssembler. When the source is a
        // GDI (or CHD-of-GDI), the assembler runs in GDI-source mode and
        // synthesizes the T2 pregap, so no source CUE is needed.

        var patchName = Path.GetFileNameWithoutExtension(options.DcpPatchPath);
        var finalUserOutputFolder = OutputFormatNaming.NextAvailableUserFolderFor(options.OutputFolder, patchName, options.OutputFormat);

        // GDI output writes directly into the user's folder. CUE/BIN and CHD
        // both rebuild a GDI in temp first and then repackage it via DiscImageEmitter.
        var finalGdiFolder = options.OutputFormat == OutputDiscImageFormat.Gdi
            ? finalUserOutputFolder
            : Path.Combine(Path.GetTempPath(), "_UDP_GDI_" + Guid.NewGuid().ToString("N"));

        var workspace = Path.Combine(Path.GetTempPath(), "_UDP_" + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(workspace, "source");
        var patchDir = Path.Combine(workspace, "patch");
        var bootsectorDir = Path.Combine(workspace, "bootsector");
        bool outputCreated = false;

        try
        {
            Directory.CreateDirectory(workspace);
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(patchDir);
            Directory.CreateDirectory(bootsectorDir);

            ct.ThrowIfCancellationRequested();

            var normalized = InputNormalizer.NormalizeAsync(options.SourceDiscImagePath, workspace, progress, ct)
                .GetAwaiter().GetResult();
            if (!normalized.Success)
            {
                result.ErrorMessage = normalized.ErrorMessage;
                return result;
            }

            progress?.Report("Extracting source disc image...");
            var readResult = GdiReader.ExtractAsync(normalized.GdiPath, sourceDir, progress, ct).GetAwaiter().GetResult();
            if (!readResult.Success)
            {
                result.ErrorMessage =
                    "The source disc image could not be read.\n" +
                    "Make sure the .gdi and all of its track files (track01.bin, track02.raw, etc.) are present in the same folder and are not damaged.\n" +
                    readResult.ErrorMessage;
                return result;
            }

            // Move IP.BIN out of sourceDir so GDromBuilder sees only game data.
            var sourceBootsector = Path.Combine(sourceDir, "bootsector");
            var sourceIpBin = Path.Combine(sourceBootsector, "IP.BIN");
            if (File.Exists(sourceIpBin))
            {
                File.Move(sourceIpBin, Path.Combine(bootsectorDir, "IP.BIN"));
            }
            if (Directory.Exists(sourceBootsector))
                Directory.Delete(sourceBootsector, recursive: true);

            ct.ThrowIfCancellationRequested();

            progress?.Report("Unpacking DCP...");
            try
            {
                ZipFile.ExtractToDirectory(options.DcpPatchPath, patchDir);
            }
            catch (Exception ex)
            {
                result.ErrorMessage =
                    "The patch file could not be opened.\n" +
                    "It may be corrupted or incomplete. Please try downloading it again.\n" +
                    ex.Message;
                return result;
            }

            // A DCP that bundles an IP.BIN supplies it, overriding the source disc's.
            var patchBootsector = Path.Combine(patchDir, "bootsector", "IP.BIN");
            bool hasPatchBootsector = File.Exists(patchBootsector);
            if (hasPatchBootsector)
            {
                File.Copy(patchBootsector, Path.Combine(bootsectorDir, "IP.BIN"), overwrite: true);
            }

            var (added, overwritten) = CountWholeFiles(patchDir, sourceDir);

            foreach (var entry in Directory.EnumerateFileSystemEntries(patchDir))
            {
                var name = Path.GetFileName(entry);
                if (string.Equals(name, "bootsector", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Directory.Exists(entry))
                {
                    var target = Path.Combine(sourceDir, name);
                    MergeDirectory(entry, target);
                }
                else
                {
                    var target = Path.Combine(sourceDir, name);
                    File.Copy(entry, target, overwrite: true);
                }
            }

            ct.ThrowIfCancellationRequested();

            progress?.Report("Applying patches...");
            int patched = 0;
            foreach (var path in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                if (!path.EndsWith(".xdelta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var originalPath = path[..^".xdelta".Length];
                if (!File.Exists(originalPath))
                {
                    result.ErrorMessage =
                        "The patch references a file that is missing from your source disc image:\n\n" +
                        $"{Path.GetRelativePath(sourceDir, originalPath)}\n\n" +
                        "This usually means the source disc image is a different version than the one this patch was built for.";
                    return result;
                }

                progress?.Report($"Patching {Path.GetRelativePath(sourceDir, originalPath).Replace('\\', '/')}...");

                var newPath = originalPath + ".new";
                try
                {
                    Xdelta3Codec.DecodeFile(originalPath, path, newPath);
                }
                catch (Exception)
                {
                    result.ErrorMessage =
                        "This patch could not be applied to your source disc image.\n\n" +
                        "The file that failed to patch was:\n" +
                        $"{Path.GetRelativePath(sourceDir, originalPath)}\n\n" +
                        "This almost always means the source disc image is a different version or region than the one the patch was built for.\n\n" +
                        "Please verify you are using the correct source disc image and try again.";
                    if (File.Exists(newPath)) File.Delete(newPath);
                    return result;
                }

                File.Delete(originalPath);
                File.Delete(path);
                File.Move(newPath, originalPath);
                patched++;
            }

            var chosenIpBin = Path.Combine(bootsectorDir, "IP.BIN");
            if (!File.Exists(chosenIpBin))
            {
                result.ErrorMessage =
                    "The Dreamcast boot sector (IP.BIN) could not be found inside the source disc image.\n\n" +
                    "The disc image may be incomplete or not a valid Dreamcast GD-ROM.";
                return result;
            }

            ct.ThrowIfCancellationRequested();

            progress?.Report("Ensuring consistent output checksum...");
            Determinism.ApplyEpochToTree(sourceDir);

            // GDI rebuild. The user-visible label says "CUE/BIN" when that's
            // the chosen output. RedumpAssembler repackages the rebuilt GDI
            // into a CUE/BIN afterward.
            var rebuildLabel = options.OutputFormat == OutputDiscImageFormat.CueBin
                ? "Building patched CUE/BIN"
                : "Building patched GDI";
            progress?.Report($"{rebuildLabel}...");

            if (Directory.Exists(finalGdiFolder))
                Directory.Delete(finalGdiFolder, recursive: true);
            Directory.CreateDirectory(finalGdiFolder);
            outputCreated = true;

            var sourceImageFolder = Path.GetDirectoryName(normalized.GdiPath) ?? string.Empty;
            var gdiLines = File.ReadAllLines(normalized.GdiPath);
            var parsed = ParseGdi(gdiLines);

            // Every track file the .gdi references has to be present. GdiReader only
            // opens the data tracks, so a missing audio track would otherwise slip
            // through and silently produce a disc with no CDDA.
            var missingTracks = parsed.LowDensityTracks.Concat(parsed.CddaTracks)
                .Where(t => !File.Exists(Path.Combine(sourceImageFolder, t.FileName)))
                .Select(t => t.FileName)
                .ToList();
            if (missingTracks.Count > 0)
            {
                result.ErrorMessage =
                    "The source disc image is missing track files that its .gdi references:\n\n" +
                    string.Join("\n", missingTracks) +
                    "\n\nMake sure all track files are present in the same folder as the .gdi and try again.";
                return result;
            }

            // Tracks copied from the source are renamed to UDP's trackNN naming so the
            // rebuilt .gdi never needs quoting and the CUE/CHD steps always see the
            // same names no matter what the source called its tracks.
            var lowDensityGdiLines = new List<string>();
            foreach (var lowTrack in parsed.LowDensityTracks)
            {
                var destName = NormalizedTrackFileName(lowTrack);
                File.Copy(Path.Combine(sourceImageFolder, lowTrack.FileName), Path.Combine(finalGdiFolder, destName), overwrite: true);
                lowDensityGdiLines.Add($"{lowTrack.TrackNumber} {lowTrack.Lba} {lowTrack.Type} {lowTrack.SectorSize} {destName} 0");
            }

            // Copy CDDA (HD audio) verbatim, and collect their paths for the builder.
            var cdda = new List<string>();
            foreach (var cddaTrack in parsed.CddaTracks)
            {
                var destCdda = Path.Combine(finalGdiFolder, NormalizedTrackFileName(cddaTrack));
                File.Copy(Path.Combine(sourceImageFolder, cddaTrack.FileName), destCdda, overwrite: true);
                cdda.Add(destCdda);
            }

            // TruncateData stays off. Enabling it can break playback on real hardware.
            // RawMode follows the source's data-track sector size: 2352 = BIN, 2048 = ISO.
            // CUE/BIN output always needs 2352-byte raw data tracks (Redump
            // assembly expects raw MODE1). GDI and CHD outputs follow the source.
            bool needsRawForRedump = options.OutputFormat == OutputDiscImageFormat.CueBin;
            var builder = new GDromBuilder(chosenIpBin, cdda)
            {
                RawMode = needsRawForRedump || parsed.Track3SectorSize == 2352,
                TruncateData = false,
                BuildDate = Determinism.FixedEpoch,
                SourceTracks = parsed.HighDensityTracks,
            };
            if (hasPatchBootsector)
            {
                builder.PreserveCustomIpBin = () =>
                {
                    ct.ThrowIfCancellationRequested();
                    if (options.PreserveCustomIpBinAsync == null)
                        throw new InvalidOperationException("The patch's IP.BIN contains ambiguous track data. Choose whether to preserve IP.BIN or update its track entries before continuing.");
                    bool preserve = options.PreserveCustomIpBinAsync().GetAwaiter().GetResult();
                    ct.ThrowIfCancellationRequested();
                    return preserve;
                };
            }
            builder.ReportProgress = p => progress?.Report($"{rebuildLabel}... {p}%");
            builder.ImportFolder(sourceDir);
            var builtTracks = builder.BuildGDROM(finalGdiFolder, ct);

            builder.WriteGdiFile(lowDensityGdiLines.ToArray(), builtTracks, Path.Combine(finalGdiFolder, "disc.gdi"));

            if (options.OutputFormat != OutputDiscImageFormat.Gdi)
            {
                // For CUE/BIN sources (and CHD-of-CUE/BIN), the source CUE's
                // track structure is mirrored in the output via RedumpAssembler.
                // For GDI / CHD-of-GDI sources, ResolveSourceCueForAssembly
                // returns null, putting the assembler in GDI-source mode.
                var sourceCueForAssembly = ResolveSourceCueForAssembly(options.SourceDiscImagePath, workspace, progress, ct);

                var emitResult = DiscImageEmitter.EmitAsync(new DiscImageEmitOptions
                {
                    GdiFolder = finalGdiFolder,
                    TargetFormat = options.OutputFormat,
                    OutputParentFolder = options.OutputFolder,
                    BaseName = patchName,
                    ResolvedFinalUserFolder = finalUserOutputFolder,
                    SourceCueForRedumpMirror = sourceCueForAssembly,
                    // Apply Patch's user-visible wording: keep "patched" in the
                    // CHD compression labels (matches the rebuild phase label).
                }, progress, ct).GetAwaiter().GetResult();

                if (!emitResult.Success)
                {
                    result.ErrorMessage = $"The patched output could not be written.\n{emitResult.ErrorMessage}";
                    return result;
                }
            }

            result.Success = true;
            result.ProducedOutputFolder = finalUserOutputFolder;
            result.FilesPatched = patched;
            result.FilesOverwritten = overwritten;
            result.FilesAdded = added;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Canceled = true;
            result.ErrorMessage = "Patching was canceled.";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"An unexpected error occurred.\n{ex.Message}";
            return result;
        }
        finally
        {
            try { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
            catch { /* best effort */ }
            if (options.OutputFormat != OutputDiscImageFormat.Gdi || (result.Canceled && outputCreated))
            {
                try { if (Directory.Exists(finalGdiFolder)) Directory.Delete(finalGdiFolder, recursive: true); }
                catch { /* best effort */ }
            }
        }
    }

    // Returns the path to a source CUE for the assembler to mirror, or null if
    // the source is GDI-shaped (in which case the assembler runs in GDI-source
    // mode and derives structure from the rebuilt GDI).
    //
    // .cue source              -> the .cue itself
    // .chd containing CD-ROM   -> extract via ConvertToCueBin first
    // .gdi source              -> null (GDI-source mode)
    // .chd containing GD-ROM   -> null (GDI-source mode)
    private static string? ResolveSourceCueForAssembly(string sourcePath, string workspace, IProgress<string>? progress, CancellationToken ct)
    {
        var detected = SourceFormatDetector.Detect(sourcePath);

        if (detected == DetectedSourceFormat.CueBin)
            return sourcePath;

        if (detected == DetectedSourceFormat.ChdContainingCueBin)
        {
            var chdCueDir = Path.Combine(workspace, "chd_to_cue");
            Directory.CreateDirectory(chdCueDir);
            var (ok, _, cuePath) = ChdConverter.ConvertToCueBin(sourcePath, chdCueDir, null, ct).GetAwaiter().GetResult();
            return ok ? cuePath : null;
        }

        // GDI / ChdContainingGdi: no source CUE, assembler uses GDI-source mode.
        return null;
    }

    private static (int Added, int Overwritten) CountWholeFiles(string patchDir, string sourceDir)
    {
        int added = 0;
        int overwritten = 0;
        foreach (var path in Directory.EnumerateFiles(patchDir, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".xdelta", StringComparison.OrdinalIgnoreCase)) continue;
            var rel = Path.GetRelativePath(patchDir, path);
            if (string.Equals(rel, "bootsector", StringComparison.OrdinalIgnoreCase)
                || rel.Replace('\\', '/').StartsWith("bootsector/", StringComparison.OrdinalIgnoreCase)) continue;

            if (File.Exists(Path.Combine(sourceDir, rel)))
                overwritten++;
            else
                added++;
        }
        return (added, overwritten);
    }

    private static void MergeDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var sub in Directory.EnumerateDirectories(source))
        {
            MergeDirectory(sub, Path.Combine(target, Path.GetFileName(sub)));
        }
    }

    private sealed class ParsedGdi
    {
        public List<GdiTrackLine> LowDensityTracks { get; } = new();
        public List<GdiTrackLine> CddaTracks { get; } = new();
        public List<DiscTrack> HighDensityTracks { get; } = new();
        // Sector size of the source's HD data track 3 (2048 = ISO, 2352 = raw BIN).
        // Used to match the rebuild output's data-track format to the source's.
        public uint Track3SectorSize { get; set; } = 2352;
    }

    private static ParsedGdi ParseGdi(string[] lines)
    {
        var result = new ParsedGdi();
        if (lines.Length < 2) return result;

        int trackCount = int.TryParse(lines[0].Trim(), out var tc) ? tc : 0;
        for (int i = 1; i <= trackCount && i < lines.Length; i++)
        {
            if (!GdiTrackLine.TryParse(lines[i], out var track)) continue;

            if (track.TrackNumber >= 3)
                result.HighDensityTracks.Add(new DiscTrack { LBA = (uint)track.Lba, Type = (byte)track.Type });

            if (track.TrackNumber <= 2)
            {
                result.LowDensityTracks.Add(track);
            }
            else if (track.TrackNumber == 3)
            {
                // Capture the source's data-track sector size so the rebuild matches it (2048 ISO vs 2352 raw).
                result.Track3SectorSize = (uint)track.SectorSize;
            }
            else if (track.Type == 0)
            {
                result.CddaTracks.Add(track);
            }
            // Extra HD data tracks (track 5+) stay in sourceDir, and GDromBuilder
            // rebuilds the layout from them.
        }

        return result;
    }

    // trackNN name for a track copied out of the source image. Keeps the source
    // extension when it has one (.bin/.raw/.iso), otherwise picks by track type.
    private static string NormalizedTrackFileName(GdiTrackLine track)
    {
        var extension = Path.GetExtension(track.FileName);
        if (string.IsNullOrEmpty(extension))
            extension = track.Type == 0 ? ".raw" : ".bin";
        return $"track{track.TrackNumber:D2}{extension.ToLowerInvariant()}";
    }
}
