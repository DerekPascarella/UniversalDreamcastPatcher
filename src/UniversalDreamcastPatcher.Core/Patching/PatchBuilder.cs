using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public enum IpBinSource
{
    ModifiedGdi,
    OriginalGdi,
}

public sealed class PatchBuildOptions
{
    public string OriginalGdiPath { get; set; } = string.Empty;
    public string ModifiedGdiPath { get; set; } = string.Empty;
    public string OutputDcpPath { get; set; } = string.Empty;

    public bool IncludeCustomIpBin { get; set; }
    public IpBinSource IpBinFrom { get; set; } = IpBinSource.ModifiedGdi;

    public bool IpBinRegionFree { get; set; }
    public bool IpBinVga { get; set; }

    public bool UseCustomGameName { get; set; }
    public string CustomGameName { get; set; } = string.Empty;
}

public sealed class PatchBuildResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProducedDcpPath { get; set; } = string.Empty;
    public int FilesDiffed { get; set; }
    public int FilesAddedVerbatim { get; set; }
}

public static class PatchBuilder
{
    public static Task<PatchBuildResult> BuildAsync(
        PatchBuildOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Build(options, progress, ct), ct);

    private static PatchBuildResult Build(PatchBuildOptions options, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new PatchBuildResult();

        if (!File.Exists(options.OriginalGdiPath))
        {
            result.ErrorMessage =
                "The original (unmodified) disc image could not be found.\n\n" +
                "Please select a valid .gdi, .cue, or .chd file and try again.";
            return result;
        }
        if (!File.Exists(options.ModifiedGdiPath))
        {
            result.ErrorMessage =
                "The modified (patched) disc image could not be found.\n\n" +
                "Please select a valid .gdi, .cue, or .chd file and try again.";
            return result;
        }
        var outputParentDir = string.IsNullOrWhiteSpace(options.OutputDcpPath)
            ? string.Empty
            : Path.GetDirectoryName(options.OutputDcpPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(outputParentDir) || !Directory.Exists(outputParentDir))
        {
            result.ErrorMessage =
                "The folder for the chosen .dcp output does not exist.\n\n" +
                "Please pick the output location again.";
            return result;
        }
        if (options.UseCustomGameName && string.IsNullOrWhiteSpace(options.CustomGameName))
        {
            result.ErrorMessage =
                "A custom game name is enabled but no name was entered.\n\n" +
                "Please type a custom game name, or uncheck the custom game name option.";
            return result;
        }

        var finalDcpPath = options.OutputDcpPath;

        var workspace = Path.Combine(Path.GetTempPath(), "_UDP_" + Guid.NewGuid().ToString("N"));
        var origDir = Path.Combine(workspace, "orig");
        var modDir = Path.Combine(workspace, "mod");
        var patchDir = Path.Combine(workspace, "patch");

        try
        {
            Directory.CreateDirectory(workspace);
            Directory.CreateDirectory(origDir);
            Directory.CreateDirectory(modDir);
            Directory.CreateDirectory(patchDir);

            ct.ThrowIfCancellationRequested();

            progress?.Report("Preparing original disc image...");
            var origNorm = InputNormalizer.NormalizeAsync(options.OriginalGdiPath, workspace, progress, ct).GetAwaiter().GetResult();
            if (!origNorm.Success)
            {
                result.ErrorMessage = origNorm.ErrorMessage;
                return result;
            }

            progress?.Report("Preparing modified disc image...");
            var modNorm = InputNormalizer.NormalizeAsync(options.ModifiedGdiPath, workspace, progress, ct).GetAwaiter().GetResult();
            if (!modNorm.Success)
            {
                result.ErrorMessage = modNorm.ErrorMessage;
                return result;
            }

            progress?.Report("Extracting original disc image...");
            var origRead = GdiReader.ExtractAsync(origNorm.GdiPath, origDir, progress, ct).GetAwaiter().GetResult();
            if (!origRead.Success)
            {
                result.ErrorMessage =
                    "The original disc image could not be read.\n\n" +
                    "Make sure the .gdi and all of its track files are present in the same folder and are not damaged.\n\n" +
                    $"Details: {origRead.ErrorMessage}";
                return result;
            }

            progress?.Report("Extracting modified disc image...");
            var modRead = GdiReader.ExtractAsync(modNorm.GdiPath, modDir, progress, ct).GetAwaiter().GetResult();
            if (!modRead.Success)
            {
                result.ErrorMessage =
                    "The modified disc image could not be read.\n\n" +
                    "Make sure the .gdi and all of its track files are present in the same folder and are not damaged.\n\n" +
                    $"Details: {modRead.ErrorMessage}";
                return result;
            }

            TryDeleteDir(Path.Combine(origDir, "bootsector"));
            var modBootsectorIpBin = Path.Combine(modDir, "bootsector", "IP.BIN");

            ct.ThrowIfCancellationRequested();

            progress?.Report("Building patches...");
            int diffed = 0;
            int added = 0;
            foreach (var modFile in Directory.EnumerateFiles(modDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var rel = Path.GetRelativePath(modDir, modFile).Replace('\\', '/');
                if (rel.StartsWith("bootsector/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var origFile = Path.Combine(origDir, rel.Replace('/', Path.DirectorySeparatorChar));
                var patchTarget = Path.Combine(patchDir, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(patchTarget)!);

                if (File.Exists(origFile))
                {
                    if (FilesEqual(origFile, modFile))
                        continue;

                    progress?.Report($"Building patch for {rel}...");
                    try
                    {
                        Xdelta3Codec.EncodeFile(origFile, modFile, patchTarget + ".xdelta");
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage =
                            "A patch could not be built for this file:\n" +
                            $"{rel}\n\n" +
                            $"Details: {ex.Message}";
                        return result;
                    }
                    diffed++;
                }
                else
                {
                    progress?.Report($"Adding {rel}...");
                    File.Copy(modFile, patchTarget, overwrite: false);
                    added++;
                }
            }

            if (options.IncludeCustomIpBin)
            {
                ct.ThrowIfCancellationRequested();

                progress?.Report("Staging IP.BIN...");
                var bootsectorOut = Path.Combine(patchDir, "bootsector");
                Directory.CreateDirectory(bootsectorOut);
                var dstIpBin = Path.Combine(bootsectorOut, "IP.BIN");

                string srcIpBin = options.IpBinFrom == IpBinSource.OriginalGdi
                    ? Path.Combine(origDir, "bootsector", "IP.BIN")
                    : modBootsectorIpBin;

                // origDir/bootsector was deleted earlier. Re-extract if the user chose it as the IP.BIN source.
                if (options.IpBinFrom == IpBinSource.OriginalGdi)
                {
                    var tmpOrigBs = Path.Combine(workspace, "orig_bs");
                    Directory.CreateDirectory(tmpOrigBs);
                    var reRead = GdiReader.ExtractAsync(origNorm.GdiPath, tmpOrigBs, null, ct).GetAwaiter().GetResult();
                    if (!reRead.Success)
                    {
                        result.ErrorMessage =
                            "The original disc image could not be re-read to extract its IP.BIN.\n\n" +
                            $"Details: {reRead.ErrorMessage}";
                        return result;
                    }
                    srcIpBin = Path.Combine(tmpOrigBs, "bootsector", "IP.BIN");
                }

                if (!File.Exists(srcIpBin))
                {
                    result.ErrorMessage =
                        "The Dreamcast boot sector (IP.BIN) could not be found inside the selected disc image.\n\n" +
                        "The disc image may be incomplete or not a valid Dreamcast GD-ROM.";
                    return result;
                }
                File.Copy(srcIpBin, dstIpBin, overwrite: true);

                if (options.IpBinRegionFree) IpBinPatcher.ApplyRegionFree(dstIpBin);
                if (options.IpBinVga) IpBinPatcher.ApplyVga(dstIpBin);
                if (options.UseCustomGameName)
                    IpBinPatcher.ApplyCustomName(dstIpBin, options.CustomGameName);
            }

            ct.ThrowIfCancellationRequested();

            // Files only, no empty directory markers.
            progress?.Report("Packing DCP...");
            if (File.Exists(finalDcpPath))
                File.Delete(finalDcpPath);

            using (var zip = ZipFile.Open(finalDcpPath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(patchDir, "*", SearchOption.AllDirectories)
                                              .OrderBy(p => p, StringComparer.Ordinal))
                {
                    var entryName = Path.GetRelativePath(patchDir, file).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }

            result.Success = true;
            result.ProducedDcpPath = finalDcpPath;
            result.FilesDiffed = diffed;
            result.FilesAddedVerbatim = added;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Patch build was cancelled.";
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

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best effort */ }
    }

    private static bool FilesEqual(string a, string b)
    {
        var fa = new FileInfo(a);
        var fb = new FileInfo(b);
        if (fa.Length != fb.Length) return false;

        using var ma = MD5.Create();
        using var mb = MD5.Create();
        using var sa = fa.OpenRead();
        using var sb = fb.OpenRead();
        return ma.ComputeHash(sa).AsSpan().SequenceEqual(mb.ComputeHash(sb));
    }
}
