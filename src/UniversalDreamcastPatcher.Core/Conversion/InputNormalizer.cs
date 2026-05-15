using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core;

public sealed class NormalizedInput
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string GdiPath { get; set; } = string.Empty;
    public string StagingDir { get; set; } = string.Empty;
}

// Accepts .gdi, .cue, or .chd and returns a .gdi path ready for GdiReader.
public static class InputNormalizer
{
    public static async Task<NormalizedInput> NormalizeAsync(
        string inputPath,
        string workspaceRoot,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new NormalizedInput();
        if (!File.Exists(inputPath))
        {
            result.ErrorMessage =
                "The selected file could not be found:\n\n" +
                $"{inputPath}";
            return result;
        }

        var ext = Path.GetExtension(inputPath).ToLowerInvariant();

        switch (ext)
        {
            case ".gdi":
                result.Success = true;
                result.GdiPath = inputPath;
                return result;

            case ".cue":
                return await NormalizeCueAsync(inputPath, workspaceRoot, progress, ct);

            case ".chd":
                return await NormalizeChdAsync(inputPath, workspaceRoot, progress, ct);

            default:
                result.ErrorMessage =
                    "This file type is not supported.\n\n" +
                    "Please select a .gdi, .cue, or .chd disc image.";
                return result;
        }
    }

    private static async Task<NormalizedInput> NormalizeCueAsync(
        string cuePath, string workspaceRoot, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new NormalizedInput();

        if (!GdiConverter.IsGdRomCue(cuePath))
        {
            result.ErrorMessage =
                "The selected .cue file is not a GD-ROM dump.\n\n" +
                "Only Dreamcast GD-ROM discs are supported. CD-ROM dumps cannot be used.";
            return result;
        }

        progress?.Report("Preparing CUE/BIN for extraction...");
        var staging = Path.Combine(workspaceRoot, "cue_to_gdi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        var (ok, msg) = await GdiConverter.ConvertToGdi(cuePath, staging, null, ct);
        if (!ok)
        {
            result.ErrorMessage =
                "The CUE/BIN disc image could not be converted to GDI.\n\n" +
                $"Details: {msg}";
            return result;
        }

        result.Success = true;
        result.StagingDir = staging;
        result.GdiPath = Path.Combine(staging, "disc.gdi");
        return result;
    }

    private static async Task<NormalizedInput> NormalizeChdAsync(
        string chdPath, string workspaceRoot, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new NormalizedInput();

        progress?.Report("Reading CHD...");
        bool isGdRom;
        try
        {
            isGdRom = ChdConverter.IsGdRomChd(chdPath);
        }
        catch (Exception ex)
        {
            result.ErrorMessage =
                "The CHD file could not be read.\n\n" +
                "It may be corrupted or use an unsupported format.\n\n" +
                $"Details: {ex.Message}";
            return result;
        }

        var staging = Path.Combine(workspaceRoot, "chd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        if (isGdRom)
        {
            var decompLabel = "Decompressing CHD to GDI";
            var decompProgress = new Progress<int>(p => progress?.Report($"{decompLabel}... {p}%"));
            progress?.Report($"{decompLabel}...");
            var (ok, msg) = await ChdConverter.ConvertToGdi(chdPath, staging, decompProgress, ct);
            if (!ok)
            {
                result.ErrorMessage =
                    "The CHD disc image could not be decompressed.\n\n" +
                    $"Details: {msg}";
                return result;
            }
            result.GdiPath = Path.Combine(staging, "disc.gdi");
        }
        else
        {
            var decompLabel = "Decompressing CHD to CUE/BIN";
            var decompProgress = new Progress<int>(p => progress?.Report($"{decompLabel}... {p}%"));
            progress?.Report($"{decompLabel}...");
            var cueStaging = Path.Combine(staging, "cuebin");
            Directory.CreateDirectory(cueStaging);
            var (ok, msg, cuePath) = await ChdConverter.ConvertToCueBin(chdPath, cueStaging, decompProgress, ct);
            if (!ok)
            {
                result.ErrorMessage =
                    "The CHD disc image could not be decompressed.\n\n" +
                    $"Details: {msg}";
                return result;
            }

            if (!GdiConverter.IsGdRomCue(cuePath))
            {
                result.ErrorMessage =
                    "The selected CHD file is a CD-ROM dump, not a GD-ROM dump.\n\n" +
                    "Only Dreamcast GD-ROM discs are supported.";
                return result;
            }

            progress?.Report("Preparing CUE/BIN for extraction...");
            var gdiStaging = Path.Combine(staging, "gdi");
            Directory.CreateDirectory(gdiStaging);
            var (ok2, msg2) = await GdiConverter.ConvertToGdi(cuePath, gdiStaging, null, ct);
            if (!ok2)
            {
                result.ErrorMessage =
                    "The CUE/BIN disc image could not be converted to GDI.\n\n" +
                    $"Details: {msg2}";
                return result;
            }
            result.GdiPath = Path.Combine(gdiStaging, "disc.gdi");
        }

        result.Success = true;
        result.StagingDir = staging;
        return result;
    }
}
