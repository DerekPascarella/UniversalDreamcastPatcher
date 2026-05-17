using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public sealed record ExternalTrackEntry(int TrackNumber, bool IsData, long Size, uint Crc32, byte[] Md5);

public sealed class ExternalDiscEntry
{
    public uint T1Crc32 { get; init; }
    public string GameName { get; init; } = string.Empty;
    public IReadOnlyList<ExternalTrackEntry> Tracks { get; init; } = Array.Empty<ExternalTrackEntry>();

    public RedumpDiscEntry ToRedump() => new()
    {
        T1Crc32 = T1Crc32,
        GameName = GameName,
        Tracks = Tracks.Select(t => new RedumpTrackEntry(t.TrackNumber, t.IsData, t.Size, t.Crc32, t.Md5)).ToArray(),
    };

    public TosecDiscEntry ToTosec() => new()
    {
        T1Crc32 = T1Crc32,
        GameName = GameName,
        Tracks = Tracks.Select(t => new TosecTrackEntry(t.TrackNumber, t.IsData, t.Size, t.Crc32, t.Md5)).ToArray(),
    };
}

// One external Logiqx XML DAT, parsed into the same shape as the internal
// compact blobs. Constructed via TryLoad. Missing files are represented with
// IsMissing = true and an empty lookup map.
public sealed class ExternalDatFile
{
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string DetectedType { get; }          // "Redump", "TOSEC", "Other"
    public int EntryCount { get; }
    public bool IsMissing { get; }
    private readonly Dictionary<uint, ExternalDiscEntry> _byT1Crc;

    private ExternalDatFile(string filePath, string detectedType, Dictionary<uint, ExternalDiscEntry> byT1Crc, bool isMissing)
    {
        FilePath = filePath;
        DetectedType = detectedType;
        _byT1Crc = byT1Crc;
        EntryCount = byT1Crc.Count;
        IsMissing = isMissing;
    }

    public ExternalDiscEntry? LookupByT1Crc32(uint t1Crc32)
        => _byT1Crc.TryGetValue(t1Crc32, out var e) ? e : null;

    // Returns a marker entry representing a path that didn't exist on disk.
    public static ExternalDatFile MissingPlaceholder(string filePath)
        => new(filePath, "—", new Dictionary<uint, ExternalDiscEntry>(), isMissing: true);

    // Parses a Logiqx XML DAT. Returns null + populates errorMessage on any
    // parse failure (not XML, no <datafile>, no games with a Track 1, etc.).
    public static ExternalDatFile? TryLoad(string filePath, out string? errorMessage)
    {
        errorMessage = null;
        if (!File.Exists(filePath))
        {
            errorMessage = "The DAT file could not be found.";
            return null;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(filePath, LoadOptions.None);
        }
        catch (Exception ex)
        {
            errorMessage = $"The DAT file could not be parsed as XML.\n{ex.Message}";
            return null;
        }

        var root = doc.Root;
        if (root == null || root.Name.LocalName != "datafile")
        {
            errorMessage = "The DAT file is not a Logiqx XML DAT (missing <datafile> root element).";
            return null;
        }

        var detectedType = DetectType(root);
        var dict = new Dictionary<uint, ExternalDiscEntry>();

        foreach (var game in root.Elements("game"))
        {
            string gameName = game.Attribute("name")?.Value ?? string.Empty;
            var tracks = ParseTracks(game.Elements("rom"));
            if (tracks.Count == 0) continue;

            var t1 = tracks.FirstOrDefault(t => t.TrackNumber == 1);
            if (t1 == null) continue;

            dict[t1.Crc32] = new ExternalDiscEntry
            {
                T1Crc32 = t1.Crc32,
                GameName = gameName,
                Tracks = tracks,
            };
        }

        if (dict.Count == 0)
        {
            errorMessage = "The DAT file contains no usable game entries (no <game> with a Track 1 <rom>).";
            return null;
        }

        return new ExternalDatFile(filePath, detectedType, dict, isMissing: false);
    }

    private static string DetectType(XElement root)
    {
        var header = root.Element("header");
        string homepage = header?.Element("homepage")?.Value ?? string.Empty;
        if (homepage.Contains("redump", StringComparison.OrdinalIgnoreCase)) return "Redump";
        if (homepage.Contains("tosec", StringComparison.OrdinalIgnoreCase)) return "TOSEC";

        string headerName = header?.Element("name")?.Value ?? string.Empty;
        if (headerName.Contains("redump", StringComparison.OrdinalIgnoreCase)) return "Redump";
        if (headerName.Contains("tosec", StringComparison.OrdinalIgnoreCase)) return "TOSEC";

        return "Other";
    }

    private static List<ExternalTrackEntry> ParseTracks(IEnumerable<XElement> roms)
    {
        var list = new List<ExternalTrackEntry>();
        int fallbackNum = 0;
        foreach (var rom in roms)
        {
            fallbackNum++;
            string name = rom.Attribute("name")?.Value ?? string.Empty;

            if (!long.TryParse(rom.Attribute("size")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long size))
                continue;
            if (!uint.TryParse(rom.Attribute("crc")?.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint crc))
                continue;

            byte[] md5 = HexDecode(rom.Attribute("md5")?.Value ?? string.Empty);
            int trackNum = ExtractTrackNumber(name) ?? fallbackNum;
            bool isData = InferIsData(name);

            list.Add(new ExternalTrackEntry(trackNum, isData, size, crc, md5));
        }
        list.Sort((a, b) => a.TrackNumber.CompareTo(b.TrackNumber));
        return list;
    }

    private static int? ExtractTrackNumber(string filename)
    {
        var m = Regex.Match(filename, @"\(Track\s+(\d+)\)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int n)) return n;
        return null;
    }

    private static bool InferIsData(string filename)
    {
        string ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext == ".bin" || ext == ".iso";
    }

    private static byte[] HexDecode(string hex)
    {
        if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0) return Array.Empty<byte>();
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                return Array.Empty<byte>();
            bytes[i] = b;
        }
        return bytes;
    }
}
