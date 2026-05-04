using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Gdrom;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core;

public sealed class GdiReadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ExtractedRoot { get; set; } = string.Empty;
    public string IpBinPath { get; set; } = string.Empty;
    public int FileCount { get; set; }
}

// Extracts the high density filesystem and IP.BIN out of a .gdi.
public static class GdiReader
{
    public static Task<GdiReadResult> ExtractAsync(
        string gdiPath,
        string outputDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Extract(gdiPath, outputDir, progress, ct), ct);

    private static GdiReadResult Extract(string gdiPath, string outputDir, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new GdiReadResult();

        try
        {
            progress?.Report("Opening GDI...");

            using var reader = GDReader.FromGDIfile(gdiPath);
            if (reader == null)
            {
                result.ErrorMessage =
                    "The .gdi file could not be read.\n\n" +
                    "Please make sure all track files (track01.bin, track02.raw, etc.) listed in the .gdi are present in the same folder.";
                return result;
            }

            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(Path.Combine(outputDir, "bootsector"));

            progress?.Report("Reading IP.BIN...");
            var ipBinPath = Path.Combine(outputDir, "bootsector", "IP.BIN");
            using (var ipSrc = reader.ReadIPBin())
            using (var ipDst = File.Create(ipBinPath))
            {
                ipSrc.CopyTo(ipDst);
            }
            result.IpBinPath = ipBinPath;

            int fileCount = 0;
            ExtractDir(reader, "\\", outputDir, progress, ct, ref fileCount);

            result.FileCount = fileCount;
            result.ExtractedRoot = outputDir;
            result.Success = true;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Extraction was cancelled.";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private static void ExtractDir(DiscUtils.Iso9660.CDReader reader, string isoDir, string hostDir, IProgress<string>? progress, CancellationToken ct, ref int fileCount)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var isoFile in reader.GetFiles(isoDir))
        {
            ct.ThrowIfCancellationRequested();

            var name = LastSegment(isoFile);
            var hostFile = Path.Combine(hostDir, name);

            progress?.Report($"Extracting {isoFile.TrimStart('\\').Replace('\\', '/')}...");

            using var src = reader.OpenFile(isoFile, FileMode.Open);
            using var dst = File.Create(hostFile);
            src.CopyTo(dst);

            fileCount++;
        }

        foreach (var isoSubDir in reader.GetDirectories(isoDir))
        {
            ct.ThrowIfCancellationRequested();

            var name = LastSegment(isoSubDir.TrimEnd('\\'));
            var hostSubDir = Path.Combine(hostDir, name);
            Directory.CreateDirectory(hostSubDir);

            ExtractDir(reader, isoSubDir, hostSubDir, progress, ct, ref fileCount);
        }
    }

    // DiscUtils ISO paths use backslashes, so Path.GetFileName misses them on Linux/macOS.
    private static string LastSegment(string isoPath)
    {
        int idx = isoPath.LastIndexOf('\\');
        return idx < 0 ? isoPath : isoPath[(idx + 1)..];
    }
}
