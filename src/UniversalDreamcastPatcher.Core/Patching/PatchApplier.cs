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

public sealed class PatchApplyOptions
{
    public string SourceDiscImagePath { get; set; } = string.Empty;
    public string DcpPatchPath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
}

public sealed class PatchApplyResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProducedGdiFolder { get; set; } = string.Empty;
    public int FilesPatched { get; set; }
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

        var patchName = Path.GetFileNameWithoutExtension(options.DcpPatchPath);
        var finalGdiFolder = Path.Combine(options.OutputFolder, $"{patchName} [GDI]");

        var workspace = Path.Combine(Path.GetTempPath(), "_UDP_" + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(workspace, "source");
        var patchDir = Path.Combine(workspace, "patch");
        var bootsectorDir = Path.Combine(workspace, "bootsector");

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

            progress?.Report("Extracting source GDI...");
            var readResult = GdiReader.ExtractAsync(normalized.GdiPath, sourceDir, progress, ct).GetAwaiter().GetResult();
            if (!readResult.Success)
            {
                result.ErrorMessage =
                    "The source disc image could not be read.\n\n" +
                    "Make sure the .gdi and all of its track files (track01.bin, track02.raw, etc.) are present in the same folder and are not damaged.\n\n" +
                    $"Details: {readResult.ErrorMessage}";
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
                    "The patch file could not be opened.\n\n" +
                    "It may be corrupted or incomplete. Please try downloading it again.\n\n" +
                    $"Details: {ex.Message}";
                return result;
            }

            // If the DCP bundles an IP.BIN, use it instead of the source disc's.
            var patchBootsector = Path.Combine(patchDir, "bootsector", "IP.BIN");
            if (File.Exists(patchBootsector))
            {
                File.Copy(patchBootsector, Path.Combine(bootsectorDir, "IP.BIN"), overwrite: true);
            }

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
            int added = 0;
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

            foreach (var path in Directory.EnumerateFiles(patchDir, "*", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".xdelta", StringComparison.OrdinalIgnoreCase)) continue;
                var rel = Path.GetRelativePath(patchDir, path).Replace('\\', '/');
                if (rel.StartsWith("bootsector/", StringComparison.OrdinalIgnoreCase)) continue;
                added++;
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

            progress?.Report("Finalizing file metadata...");
            Determinism.ApplyEpochToTree(sourceDir);

            progress?.Report("Building patched GDI...");

            if (Directory.Exists(finalGdiFolder))
                Directory.Delete(finalGdiFolder, recursive: true);
            Directory.CreateDirectory(finalGdiFolder);

            var sourceImageFolder = Path.GetDirectoryName(normalized.GdiPath) ?? string.Empty;
            var gdiLines = File.ReadAllLines(normalized.GdiPath);
            var parsed = ParseGdi(gdiLines, sourceImageFolder);

            foreach (var lowTrack in parsed.LowDensityTracks)
            {
                File.Copy(lowTrack, Path.Combine(finalGdiFolder, Path.GetFileName(lowTrack)), overwrite: true);
            }

            // Copy CDDA (HD audio) verbatim, and collect their paths for the builder.
            var cdda = new List<string>();
            foreach (var cddaTrack in parsed.CddaTracks)
            {
                var destCdda = Path.Combine(finalGdiFolder, Path.GetFileName(cddaTrack));
                File.Copy(cddaTrack, destCdda, overwrite: true);
                cdda.Add(destCdda);
            }

            // TruncateData stays off. Enabling it can break playback on real hardware.
            // RawMode follows the source's data-track sector size: 2352 = BIN, 2048 = ISO.
            var builder = new GDromBuilder(chosenIpBin, cdda)
            {
                RawMode = parsed.Track3SectorSize == 2352,
                TruncateData = false,
                BuildDate = Determinism.FixedEpoch,
            };
            builder.ImportFolder(sourceDir);
            var builtTracks = builder.BuildGDROM(finalGdiFolder, ct);

            builder.WriteGdiFile(gdiLines, builtTracks, Path.Combine(finalGdiFolder, "disc.gdi"));

            result.Success = true;
            result.ProducedGdiFolder = finalGdiFolder;
            result.FilesPatched = patched;
            result.FilesAdded = added;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Patching was cancelled.";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"An unexpected error occurred.\n\nDetails: {ex.Message}";
            return result;
        }
        finally
        {
            try { if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true); }
            catch { /* best effort */ }
        }
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
        public List<string> LowDensityTracks { get; } = new();
        public List<string> CddaTracks { get; } = new();
        // Sector size of the source's HD data track 3 (2048 = ISO, 2352 = raw BIN).
        // Used to match the rebuild output's data-track format to the source's.
        public uint Track3SectorSize { get; set; } = 2352;
    }

    private static ParsedGdi ParseGdi(string[] lines, string gdiDirectory)
    {
        var result = new ParsedGdi();
        if (lines.Length < 2) return result;

        int trackCount = int.TryParse(lines[0].Trim(), out var tc) ? tc : 0;
        for (int i = 1; i <= trackCount && i < lines.Length; i++)
        {
            var parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) continue;

            int trackNumber = int.Parse(parts[0]);
            int trackType = int.Parse(parts[2]);
            uint sectorSize = uint.TryParse(parts[3], out var ss) ? ss : 2352;
            string filename = parts[4];
            string fullPath = Path.Combine(gdiDirectory, filename);

            if (trackNumber <= 2)
            {
                if (File.Exists(fullPath)) result.LowDensityTracks.Add(fullPath);
            }
            else if (trackNumber == 3)
            {
                // Capture the source's data-track sector size so the rebuild matches it (2048 ISO vs 2352 raw).
                result.Track3SectorSize = sectorSize;
            }
            else
            {
                bool isAudio = trackType == 0;
                if (isAudio && File.Exists(fullPath))
                {
                    result.CddaTracks.Add(fullPath);
                }
                // Extra HD data tracks (track 5+) aren't tracked here. Their content is
                // already in sourceDir, and GDromBuilder rebuilds the layout from it.
            }
        }

        return result;
    }
}
