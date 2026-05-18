using System.Collections.Generic;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Shared;

// Builds the rows shown in an output-format dropdown for a given detected
// source format. Three targets: GDI, CUE/BIN, CHD.
public static class FormatOptionCatalog
{
    public static List<FormatOption> ForApplyPatchSource(DetectedSourceFormat source)
    {
        var items = new List<FormatOption>();
        switch (source)
        {
            case DetectedSourceFormat.Gdi:
                items.Add(new("GDI (same as source disc image)", OutputDiscImageFormat.Gdi));
                items.Add(new("CUE/BIN (converted from GDI)", OutputDiscImageFormat.CueBin));
                items.Add(new("CHD (compressed from GDI)", OutputDiscImageFormat.ChdGdRom));
                break;
            case DetectedSourceFormat.CueBin:
                items.Add(new("GDI (converted from CUE/BIN)", OutputDiscImageFormat.Gdi));
                items.Add(new("CUE/BIN (same as source disc image)", OutputDiscImageFormat.CueBin));
                items.Add(new("CHD (compressed from GDI, converted from CUE/BIN)", OutputDiscImageFormat.ChdGdRom));
                break;
            case DetectedSourceFormat.ChdContainingGdi:
            case DetectedSourceFormat.ChdContainingCueBin:
                items.Add(new("GDI (decompressed from CHD)", OutputDiscImageFormat.Gdi));
                items.Add(new("CUE/BIN (decompressed from CHD)", OutputDiscImageFormat.CueBin));
                items.Add(new("CHD (recompressed)", OutputDiscImageFormat.ChdGdRom));
                break;
        }
        return items;
    }

    public static List<FormatOption> ForConverterSource(DetectedSourceFormat source)
    {
        var items = new List<FormatOption>();
        switch (source)
        {
            case DetectedSourceFormat.Gdi:
                items.Add(new("CUE/BIN (converted from GDI)", OutputDiscImageFormat.CueBin));
                items.Add(new("CHD (compressed from GDI)", OutputDiscImageFormat.ChdGdRom));
                break;
            case DetectedSourceFormat.CueBin:
                items.Add(new("GDI (converted from CUE/BIN)", OutputDiscImageFormat.Gdi));
                items.Add(new("CHD (compressed from converted GDI)", OutputDiscImageFormat.ChdGdRom));
                break;
            case DetectedSourceFormat.ChdContainingGdi:
            case DetectedSourceFormat.ChdContainingCueBin:
                items.Add(new("GDI (decompressed from CHD)", OutputDiscImageFormat.Gdi));
                items.Add(new("CUE/BIN (decompressed from CHD)", OutputDiscImageFormat.CueBin));
                break;
        }
        return items;
    }

    // Batch target dropdown: heterogeneous sources, so the rows are the three
    // canonical formats with no source-dependent suffix.
    public static List<FormatOption> ForConverterBatch()
        => new()
        {
            new("GDI", OutputDiscImageFormat.Gdi),
            new("CUE/BIN", OutputDiscImageFormat.CueBin),
            new("CHD", OutputDiscImageFormat.ChdGdRom),
        };
}
