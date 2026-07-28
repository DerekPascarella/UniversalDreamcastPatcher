using System;
using System.Collections.Generic;
using System.IO;
using UniversalDreamcastPatcher.Core.Conversion;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public enum DetectedSourceFormat
{
    Unknown,
    Gdi,
    CueBin,
    ChdContainingGdi,
    ChdContainingCueBin,
}

// Detects what's behind a source path. For .chd, peeks inside to tell GD-ROM
// from CD-ROM, since that determines which output formats are allowed.
public static class SourceFormatDetector
{
    public static DetectedSourceFormat Detect(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return DetectedSourceFormat.Unknown;

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        switch (ext)
        {
            case ".gdi":
                return DetectedSourceFormat.Gdi;
            case ".cue":
                return GdiConverter.IsGdRomCue(sourcePath)
                    ? DetectedSourceFormat.CueBin
                    : DetectedSourceFormat.Unknown;
            case ".chd":
                return DetectChdContents(sourcePath);
            default:
                return DetectedSourceFormat.Unknown;
        }
    }

    public const string LegacyRedumpGdiError =
        "This disc image is in Redump's old short-lived GDI format.\n\n" +
        "Universal Dreamcast Patcher supports either modern Redump CUE/BIN or TOSEC GDI disc images (both of which can be compressed as CHD).";

    // Redump's old GDI format keeps the pregap inside each track file, so its
    // track 2 LBA equals track 1's sector count exactly. A normal GDI puts
    // track 2 at track 1's sector count plus the 150-sector pregap.
    public static bool IsLegacyRedumpGdi(string gdiPath)
    {
        try
        {
            var lines = File.ReadAllLines(gdiPath);
            if (lines.Length < 3) return false;

            if (!GdiTrackLine.TryParse(lines[1], out var track1)) return false;
            if (!GdiTrackLine.TryParse(lines[2], out var track2)) return false;
            if (track1.TrackNumber != 1 || track2.TrackNumber != 2) return false;
            if (track1.SectorSize <= 0) return false;

            var gdiFolder = Path.GetDirectoryName(gdiPath) ?? string.Empty;
            var track1File = new FileInfo(Path.Combine(gdiFolder, track1.FileName));
            if (!track1File.Exists) return false;

            long track1Sectors = track1File.Length / track1.SectorSize;
            return track2.Lba == track1Sectors;
        }
        catch
        {
            // Unreadable .gdi - let the normal loading paths report it.
            return false;
        }
    }

    public const string ConversionGdiError =
        "This disc image appears to be an Atomiswave or NAOMI conversion.\n\n" +
        "Patching and CUE/BIN conversion are not supported for these discs. Converting between GDI and CHD is still supported.";

    // Conversion discs ship a decoy filesystem. The PVD declares a tiny volume,
    // the boot binary's directory entry points far outside it, and the real
    // game data has no directory entries at all. Retail discs keep the volume
    // size and the file extents consistent with the tracks, so those two
    // checks are enough to tell them apart.
    public static bool IsConversionGdi(string gdiPath)
    {
        try
        {
            var folder = Path.GetDirectoryName(gdiPath) ?? string.Empty;
            var lines = File.ReadAllLines(gdiPath);
            if (lines.Length < 2) return false;

            var tracks = new List<GdiTrackLine>();
            for (int i = 1; i < lines.Length; i++)
                if (GdiTrackLine.TryParse(lines[i], out var t)) tracks.Add(t);
            if (tracks.Count < 3) return false;

            var regions = new List<(long Start, long End)>();
            var spans = new List<HdDataSpan>();
            foreach (var t in tracks)
            {
                if (t.SectorSize < 2048) return false;
                var file = new FileInfo(Path.Combine(folder, t.FileName));
                if (!file.Exists) return false;
                long sectors = file.Length / t.SectorSize;
                regions.Add((t.Lba, t.Lba + sectors));
                if (t.TrackNumber >= 3 && t.Type == 4)
                    spans.Add(new HdDataSpan(t.Lba, sectors, t.SectorSize, file.FullName));
            }
            if (spans.Count == 0) return false;

            long baseLba = spans[0].Lba;
            long hdDataSectors = 0;
            foreach (var s in spans) hdDataSectors += s.Sectors;

            var pvd = ReadHdSectors(spans, baseLba + 16, 1);
            if (pvd == null || pvd[0] != 0x01 || pvd[1] != (byte)'C' || pvd[2] != (byte)'D' ||
                pvd[3] != (byte)'0' || pvd[4] != (byte)'0' || pvd[5] != (byte)'1')
                return false;

            long volumeSpace = BitConverter.ToUInt32(pvd, 80);
            long volumeEnd = baseLba + volumeSpace;
            long rootExtent = BitConverter.ToUInt32(pvd, 158);
            long rootBytes = BitConverter.ToUInt32(pvd, 166);

            bool anyOutsideVolume = false;
            bool anyUnbacked = false;

            var pending = new Stack<(long Extent, long Bytes)>();
            pending.Push((rootExtent, rootBytes));
            var visited = new HashSet<long>();

            while (pending.Count > 0 && visited.Count <= 5000)
            {
                var (extent, bytes) = pending.Pop();
                if (!visited.Add(extent)) continue;

                var data = ReadHdSectors(spans, extent, (bytes + 2047) / 2048);
                if (data == null)
                {
                    // The directory itself lives outside every data track.
                    anyUnbacked = true;
                    continue;
                }

                long offset = 0;
                while (offset < bytes)
                {
                    int recordLength = data[offset];
                    if (recordLength == 0)
                    {
                        // Directory records never cross a sector boundary.
                        offset = (offset / 2048 + 1) * 2048;
                        continue;
                    }
                    if (recordLength < 34 || offset + recordLength > data.Length) break;

                    long recExtent = BitConverter.ToUInt32(data, (int)offset + 2);
                    long recBytes = BitConverter.ToUInt32(data, (int)offset + 10);
                    byte flags = data[offset + 25];
                    byte nameLength = data[offset + 32];
                    bool selfOrParent = nameLength == 1 &&
                        (data[offset + 33] == 0x00 || data[offset + 33] == 0x01);
                    offset += recordLength;

                    if (selfOrParent) continue;

                    // Zero-length entries sometimes carry garbage extents.
                    if (recBytes == 0) continue;

                    if ((flags & 0x02) != 0)
                    {
                        pending.Push((recExtent, recBytes));
                        continue;
                    }

                    long recSectors = (recBytes + 2047) / 2048;
                    if (recExtent < baseLba || recExtent + recSectors > volumeEnd)
                        anyOutsideVolume = true;

                    // Retail discs let the last file run up to 150 sectors
                    // past the dumped track data, so only a start outside
                    // every track or a larger overrun counts as a violation.
                    if (!SectorBacked(regions, recExtent) ||
                        UnbackedSectors(regions, recExtent, recSectors) > 150)
                        anyUnbacked = true;
                }
            }

            bool decoyVolume = anyOutsideVolume && volumeSpace * 10 < hdDataSectors;
            return decoyVolume || anyUnbacked;
        }
        catch
        {
            // Unreadable disc image. Let the normal loading paths report it.
            return false;
        }
    }

    private readonly record struct HdDataSpan(long Lba, long Sectors, int SectorSize, string FilePath);

    // Reads 2048 bytes of user data per sector from whichever high density
    // data track holds the LBA. Raw tracks keep user data at offset 16.
    private static byte[]? ReadHdSectors(List<HdDataSpan> spans, long lba, long count)
    {
        if (count <= 0 || count > 65536) return null;
        var result = new byte[count * 2048];
        long done = 0;
        while (done < count)
        {
            long target = lba + done;
            HdDataSpan span = default;
            foreach (var s in spans)
                if (s.Lba <= target && target < s.Lba + s.Sectors) { span = s; break; }
            if (span.SectorSize == 0) return null;

            long take = Math.Min(count - done, span.Lba + span.Sectors - target);
            using var fs = File.OpenRead(span.FilePath);
            if (span.SectorSize == 2048)
            {
                fs.Position = (target - span.Lba) * 2048;
                ReadFully(fs, result, (int)(done * 2048), (int)(take * 2048));
            }
            else
            {
                for (long i = 0; i < take; i++)
                {
                    fs.Position = (target - span.Lba + i) * span.SectorSize + 16;
                    ReadFully(fs, result, (int)((done + i) * 2048), 2048);
                }
            }
            done += take;
        }
        return result;
    }

    // A span already claims to cover this range, so a short read here is an
    // I/O failure. The caller's catch block reports the disc as unreadable.
    private static void ReadFully(FileStream fs, byte[] buffer, int offset, int count)
    {
        int readTotal = 0;
        while (readTotal < count)
        {
            int n = fs.Read(buffer, offset + readTotal, count - readTotal);
            if (n == 0) throw new IOException("Short read while loading high density sector data.");
            readTotal += n;
        }
    }

    private static bool SectorBacked(List<(long Start, long End)> regions, long lba)
    {
        foreach (var (a, b) in regions)
            if (a <= lba && lba < b) return true;
        return false;
    }

    // Counts extent sectors that no track region covers, audio included.
    // Retail discs list CDDA as files whose extents sit inside audio
    // tracks, so those count as valid backing.
    private static long UnbackedSectors(List<(long Start, long End)> regions, long extent, long sectors)
    {
        var need = new List<(long Start, long End)> { (extent, extent + sectors) };
        foreach (var (a, b) in regions)
        {
            var next = new List<(long Start, long End)>();
            foreach (var (x, y) in need)
            {
                if (b <= x || a >= y) { next.Add((x, y)); continue; }
                if (x < a) next.Add((x, a));
                if (y > b) next.Add((b, y));
            }
            need = next;
            if (need.Count == 0) return 0;
        }
        long total = 0;
        foreach (var (x, y) in need) total += y - x;
        return total;
    }

    private static DetectedSourceFormat DetectChdContents(string chdPath)
    {
        try
        {
            using var reader = new ChdReader(chdPath);
            return reader.IsGdRom
                ? DetectedSourceFormat.ChdContainingGdi
                : DetectedSourceFormat.ChdContainingCueBin;
        }
        catch
        {
            // Unreadable CHD - let the UI decide how to handle it.
            return DetectedSourceFormat.Unknown;
        }
    }
}
