using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public sealed class DiscImageEmitOptions
{
    // Source GDI folder (disc.gdi + tracks). The emitter only reads from it.
    public string GdiFolder { get; set; } = string.Empty;

    // CueBin or ChdGdRom. GDI target is not handled here: a GDI input is
    // already a GDI output, so no emit step is needed.
    public OutputDiscImageFormat TargetFormat { get; set; }

    // User's chosen output parent folder. The emitter creates "<BaseName> <suffix>"
    // inside this folder and writes the result there.
    public string OutputParentFolder { get; set; } = string.Empty;

    // Used as both the output folder's base name (before the format suffix) and
    // the base of the output filenames ("<BaseName>.cue", "<BaseName>.chd",
    // "<BaseName> (Track NN).bin").
    public string BaseName { get; set; } = string.Empty;

    // When set, RedumpAssembler mirrors this CUE's track structure. When
    // null, it runs in GDI-source mode and synthesizes the T2 pregap.
    public string? SourceCueForRedumpMirror { get; set; }

    public string CompressGdiToChdLabel { get; set; } = "Compressing patched GDI to CHD";
}

public sealed class DiscImageEmitResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ProducedOutputFolder { get; set; } = string.Empty;
}

// Produces a CUE/BIN or CHD from an existing GDI folder. Extracted from
// PatchApplier.Apply's emit ladder so that PatchApplier (Apply Patch) and
// DiscImageConverter share one code path.
public static class DiscImageEmitter
{
    public static Task<DiscImageEmitResult> EmitAsync(
        DiscImageEmitOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Emit(options, progress, ct), ct);

    private static DiscImageEmitResult Emit(DiscImageEmitOptions options, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new DiscImageEmitResult();

        if (options.TargetFormat == OutputDiscImageFormat.Gdi)
        {
            result.ErrorMessage = "DiscImageEmitter does not handle a GDI target. The caller must place the GDI at its final location directly.";
            return result;
        }

        var finalUserOutputFolder = OutputFormatNaming.UserFolderFor(
            options.OutputParentFolder, options.BaseName, options.TargetFormat);

        try
        {
            if (options.TargetFormat == OutputDiscImageFormat.CueBin)
            {
                // The progress label set by the caller (typically
                // "Building patched CUE/BIN...") covers RedumpAssembler too.

                if (Directory.Exists(finalUserOutputFolder))
                    Directory.Delete(finalUserOutputFolder, recursive: true);
                Directory.CreateDirectory(finalUserOutputFolder);

                var assembleResult = RedumpAssembler.AssembleAsync(new RedumpAssembleOptions
                {
                    RebuiltGdiFolder = options.GdiFolder,
                    SourceCuePath = options.SourceCueForRedumpMirror,
                    OutputFolder = finalUserOutputFolder,
                    TrackBaseName = options.BaseName,
                    OutputCueFilename = OutputFormatNaming.CueFileName(options.BaseName),
                }, progress, ct).GetAwaiter().GetResult();

                if (!assembleResult.Success)
                {
                    result.ErrorMessage =
                        "The CUE/BIN output could not be assembled.\n\n" +
                        $"Details: {assembleResult.ErrorMessage}";
                    return result;
                }
            }
            else if (options.TargetFormat == OutputDiscImageFormat.ChdGdRom)
            {
                var compressLabel = options.CompressGdiToChdLabel;
                progress?.Report($"{compressLabel}...");

                if (Directory.Exists(finalUserOutputFolder))
                    Directory.Delete(finalUserOutputFolder, recursive: true);
                Directory.CreateDirectory(finalUserOutputFolder);

                // Feed the GDI directly to libchdw.
                var gdiFile = Directory.EnumerateFiles(options.GdiFolder, "*.gdi").FirstOrDefault();
                if (gdiFile == null)
                {
                    result.ErrorMessage = $"No .gdi file found in {options.GdiFolder}.";
                    return result;
                }

                var outChdPath = Path.Combine(finalUserOutputFolder, OutputFormatNaming.ChdFileName(options.BaseName));
                var compressProgress = new Progress<int>(p =>
                    progress?.Report($"{compressLabel}... {p}%"));

                var (chdOk, chdMsg) = ChdWriter.ConvertToChd(gdiFile, outChdPath, compressProgress, ct)
                    .GetAwaiter().GetResult();
                if (!chdOk)
                {
                    result.ErrorMessage =
                        "The CHD output could not be written.\n\n" +
                        $"Details: {chdMsg}";
                    return result;
                }
            }

            result.Success = true;
            result.ProducedOutputFolder = finalUserOutputFolder;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "The operation was cancelled.";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"An unexpected error occurred while writing the output.\n\nDetails: {ex.Message}";
            return result;
        }
    }
}
