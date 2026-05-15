using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Conversion;

public sealed class DiscImageConvertOptions
{
    public string SourceDiscImagePath { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public OutputDiscImageFormat TargetFormat { get; set; } = OutputDiscImageFormat.Gdi;
}

public sealed class DiscImageConvertResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProducedOutputFolder { get; set; } = string.Empty;
}

// Cross-converts between GDI, CUE/BIN, and CHD without going through
// GDromBuilder. PatchApplier rebuilds because it changes bytes. This class
// preserves the disc data and only re-wraps the container.
//
// Routes:
//   GDI from CUE/BIN: GdiConverter
//   GDI from CHD:     ChdConverter.ConvertToGdi
//   CUE/BIN from X:   pivot to GDI, then DiscImageEmitter (RedumpAssembler)
//   CHD from X:       pivot to GDI, then DiscImageEmitter (ChdWriter on .gdi)
//
// Same-format pairs are not exposed in the UI but are handled defensively as
// a folder copy.
public static class DiscImageConverter
{
    public static Task<DiscImageConvertResult> ConvertAsync(
        DiscImageConvertOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Convert(options, progress, ct), ct);

    private static DiscImageConvertResult Convert(DiscImageConvertOptions options, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new DiscImageConvertResult();

        if (!File.Exists(options.SourceDiscImagePath))
        {
            result.ErrorMessage =
                "The source disc image could not be found.\n\n" +
                "Please select a valid .gdi, .cue, or .chd file and try again.";
            return result;
        }
        if (string.IsNullOrWhiteSpace(options.OutputFolder) || !Directory.Exists(options.OutputFolder))
        {
            result.ErrorMessage =
                "The selected output folder does not exist.\n\n" +
                "Please choose a different folder and try again.";
            return result;
        }

        var detected = SourceFormatDetector.Detect(options.SourceDiscImagePath);
        if (detected == DetectedSourceFormat.Unknown)
        {
            result.ErrorMessage =
                "The selected file is not a recognized Dreamcast disc image.\n\n" +
                "Only .gdi, .cue, and .chd files are supported.";
            return result;
        }

        var baseName = Path.GetFileNameWithoutExtension(options.SourceDiscImagePath);
        var finalUserOutputFolder = OutputFormatNaming.UserFolderFor(
            options.OutputFolder, baseName, options.TargetFormat);

        var workspace = Path.Combine(Path.GetTempPath(), "_UDP_CONVERT_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(workspace);
            ct.ThrowIfCancellationRequested();

            string? routeError = ConvertByRoute(
                options.SourceDiscImagePath, detected,
                options.TargetFormat,
                finalUserOutputFolder, baseName,
                workspace, progress, ct);

            if (routeError != null)
            {
                result.ErrorMessage = routeError;
                return result;
            }

            result.Success = true;
            result.ProducedOutputFolder = finalUserOutputFolder;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Conversion was cancelled.";
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

    // Returns null on success, an error message on failure.
    private static string? ConvertByRoute(
        string sourcePath,
        DetectedSourceFormat source,
        OutputDiscImageFormat target,
        string finalOutputFolder,
        string baseName,
        string workspace,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        // Same-format short circuit (not exposed in UI, defensive only).
        if (IsSameFormat(source, target))
        {
            progress?.Report("Copying source disc image...");
            return CopySourceAsFolder(sourcePath, source, finalOutputFolder);
        }

        // From GDI source: source folder is already the GDI, feed it to the emitter.
        if (source == DetectedSourceFormat.Gdi)
        {
            var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            return EmitFromGdi(sourceDir, target, finalOutputFolder, baseName, progress, ct);
        }

        // From CUE/BIN source.
        if (source == DetectedSourceFormat.CueBin)
        {
            if (target == OutputDiscImageFormat.Gdi)
            {
                progress?.Report("Converting CUE/BIN to GDI...");
                return ConvertCueBinToGdiFolder(sourcePath, finalOutputFolder, ct);
            }
            // CHD target: pivot CUE/BIN -> GDI, then emit.
            var gdiPivot = Path.Combine(workspace, "gdi_pivot");
            Directory.CreateDirectory(gdiPivot);
            progress?.Report("Converting CUE/BIN to GDI...");
            var (ok, msg) = GdiConverter.ConvertToGdi(sourcePath, gdiPivot, null, ct).GetAwaiter().GetResult();
            if (!ok)
                return $"The CUE/BIN disc image could not be converted to GDI.\n\nDetails: {msg}";
            return EmitFromGdi(gdiPivot, target, finalOutputFolder, baseName, progress, ct);
        }

        // From CHD source (GD-ROM CHD). chdman normalizes both inner forms,
        // so this single path covers either of them.
        if (source == DetectedSourceFormat.ChdContainingGdi)
        {
            var decompLabel = "Decompressing CHD to GDI";
            var decompProgress = new Progress<int>(p => progress?.Report($"{decompLabel}... {p}%"));
            if (target == OutputDiscImageFormat.Gdi)
            {
                progress?.Report($"{decompLabel}...");
                Directory.CreateDirectory(finalOutputFolder);
                var (ok, msg) = ChdConverter.ConvertToGdi(sourcePath, finalOutputFolder, decompProgress, ct).GetAwaiter().GetResult();
                if (!ok)
                    return $"The CHD disc image could not be decompressed.\n\nDetails: {msg}";
                return null;
            }
            // CUE/BIN target: decompress to GDI in workspace, then emit.
            var gdiPivot = Path.Combine(workspace, "gdi_pivot");
            Directory.CreateDirectory(gdiPivot);
            progress?.Report($"{decompLabel}...");
            var (ok2, msg2) = ChdConverter.ConvertToGdi(sourcePath, gdiPivot, decompProgress, ct).GetAwaiter().GetResult();
            if (!ok2)
                return $"The CHD disc image could not be decompressed.\n\nDetails: {msg2}";
            return EmitFromGdi(gdiPivot, target, finalOutputFolder, baseName, progress, ct);
        }

        // CHD without the CHGD tag. In practice this only fires for actual
        // CD-ROM CHDs, which UDP rejects. We extract the inner .cue, run
        // IsGdRomCue on it, and emit the standard "not a GD-ROM dump" error.
        if (source == DetectedSourceFormat.ChdContainingCueBin)
        {
            var cuePivot = Path.Combine(workspace, "cue_pivot");
            Directory.CreateDirectory(cuePivot);
            var decompLabel = "Decompressing CHD to CUE/BIN";
            var decompProgress = new Progress<int>(p => progress?.Report($"{decompLabel}... {p}%"));
            progress?.Report($"{decompLabel}...");
            var (ok, msg, cuePath) = ChdConverter.ConvertToCueBin(sourcePath, cuePivot, decompProgress, ct).GetAwaiter().GetResult();
            if (!ok)
                return $"The CHD disc image could not be decompressed.\n\nDetails: {msg}";

            if (!GdiConverter.IsGdRomCue(cuePath))
                return "The selected CHD file is a CD-ROM dump, not a GD-ROM dump.\n\nOnly Dreamcast GD-ROM discs are supported.";

            // CUE passed IsGdRomCue: the CHD is GD-ROM but missing the CHGD
            // tag. Fall back to the GDI-pivot path so the conversion proceeds.
            if (target == OutputDiscImageFormat.Gdi)
            {
                progress?.Report("Converting CUE/BIN to GDI...");
                return ConvertCueBinToGdiFolder(cuePath, finalOutputFolder, ct);
            }
            var gdiPivot = Path.Combine(workspace, "gdi_pivot");
            Directory.CreateDirectory(gdiPivot);
            progress?.Report("Converting CUE/BIN to GDI...");
            var (gOk, gMsg) = GdiConverter.ConvertToGdi(cuePath, gdiPivot, null, ct).GetAwaiter().GetResult();
            if (!gOk)
                return $"The CUE/BIN disc image could not be converted to GDI.\n\nDetails: {gMsg}";
            return EmitFromGdi(gdiPivot, target, finalOutputFolder, baseName, progress, ct);
        }

        return "Unsupported source/target combination.";
    }

    // Drives DiscImageEmitter with the Converter's progress labels (no "patched").
    private static string? EmitFromGdi(
        string gdiFolder,
        OutputDiscImageFormat target,
        string finalOutputFolder,
        string baseName,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (target == OutputDiscImageFormat.CueBin)
            progress?.Report("Converting GDI to CUE/BIN...");

        var emitOptions = new DiscImageEmitOptions
        {
            GdiFolder = gdiFolder,
            TargetFormat = target,
            OutputParentFolder = Path.GetDirectoryName(finalOutputFolder) ?? string.Empty,
            BaseName = baseName,
            SourceCueForRedumpMirror = null,
            CompressGdiToChdLabel = "Compressing GDI to CHD",
        };

        var emitResult = DiscImageEmitter.EmitAsync(emitOptions, progress, ct).GetAwaiter().GetResult();
        return emitResult.Success ? null : emitResult.ErrorMessage;
    }

    private static string? ConvertCueBinToGdiFolder(string cuePath, string finalOutputFolder, CancellationToken ct)
    {
        if (!GdiConverter.IsGdRomCue(cuePath))
            return "The selected .cue file is not a GD-ROM dump.\n\nOnly Dreamcast GD-ROM discs are supported. CD-ROM dumps cannot be used.";
        Directory.CreateDirectory(finalOutputFolder);
        var (ok, msg) = GdiConverter.ConvertToGdi(cuePath, finalOutputFolder, null, ct).GetAwaiter().GetResult();
        return ok ? null : $"The CUE/BIN disc image could not be converted to GDI.\n\nDetails: {msg}";
    }

    private static bool IsSameFormat(DetectedSourceFormat source, OutputDiscImageFormat target) =>
        (source == DetectedSourceFormat.Gdi && target == OutputDiscImageFormat.Gdi) ||
        (source == DetectedSourceFormat.CueBin && target == OutputDiscImageFormat.CueBin) ||
        (source == DetectedSourceFormat.ChdContainingGdi && target == OutputDiscImageFormat.ChdGdRom);

    // Same-format path: defensive only (UI omits this option). Copies the
    // source's containing folder for .gdi/.cue, or the single .chd file.
    private static string? CopySourceAsFolder(string sourcePath, DetectedSourceFormat source, string finalOutputFolder)
    {
        Directory.CreateDirectory(finalOutputFolder);
        if (source == DetectedSourceFormat.Gdi || source == DetectedSourceFormat.CueBin)
        {
            var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            foreach (var f in Directory.EnumerateFiles(sourceDir))
                File.Copy(f, Path.Combine(finalOutputFolder, Path.GetFileName(f)), overwrite: true);
        }
        else
        {
            File.Copy(sourcePath, Path.Combine(finalOutputFolder, Path.GetFileName(sourcePath)), overwrite: true);
        }
        return null;
    }
}
