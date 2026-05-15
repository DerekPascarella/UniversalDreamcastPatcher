using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// One-off tool: walks a directory of TOSEC DC Logiqx-XML DAT files and emits a
// single gzip-compressed binary blob for embedding into UDP.Core. Same wire
// format as the Redump compactor so we can share parsing code.
//
// TOSEC ROM names follow:
//   "GameName ... .gdi"        (the GDI TOC, ~100 bytes)
//   "trackNN.bin"              (Mode 1 data tracks)
//   "trackNN.raw"              (CD-DA audio tracks)
//   "trackNN.iso"              (ISO 2048-byte data tracks, used by some demos)
//
// Output blob (little-endian):
//   "UDPT" + version=1 + game_count
//   per game:
//     t1_crc32 u32, name_len u16, name_utf8, track_count u8,
//     per track: track# u8, is_data u8, size i64, crc32 u32, md5 (16 bytes)
//
// Usage: TosecDatCompactor <dats_dir> <output.bin.gz>

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: TosecDatCompactor <dats_dir> <output.bin.gz>");
    return 2;
}

string datDir = args[0];
string outPath = args[1];

if (!Directory.Exists(datDir))
{
    Console.Error.WriteLine($"DATs dir not found: {datDir}");
    return 1;
}

var datFiles = Directory.GetFiles(datDir, "*.dat", SearchOption.TopDirectoryOnly)
    .OrderBy(p => p)
    .ToList();

if (datFiles.Count == 0)
{
    Console.Error.WriteLine($"No .dat files in {datDir}");
    return 1;
}

Console.WriteLine($"Found {datFiles.Count} TOSEC DAT files.");

var byT1Crc = new Dictionary<uint, (string Name, List<(int Num, bool IsData, long Size, uint Crc, byte[] Md5)> Tracks, bool IsVerified)>();
int totalEntries = 0;
int skippedNoT1 = 0;
int skippedNoTracks = 0;
int collisions = 0;

var trackRe = new Regex(@"^track(\d{1,3})\.(bin|raw|iso)$", RegexOptions.IgnoreCase);

foreach (var datPath in datFiles)
{
    XDocument doc;
    try { doc = XDocument.Load(datPath); }
    catch (Exception ex) { Console.Error.WriteLine($"  Skip {Path.GetFileName(datPath)}: {ex.Message}"); continue; }

    int inThisDat = 0;
    foreach (var game in doc.Descendants("game"))
    {
        var name = (string?)game.Attribute("name") ?? "<unknown>";
        bool isVerified = name.Contains("[!]");

        var tracks = new List<(int Num, bool IsData, long Size, uint Crc, byte[] Md5)>();
        uint? t1Crc = null;

        foreach (var rom in game.Elements("rom"))
        {
            string romName = (string?)rom.Attribute("name") ?? "";
            var m = trackRe.Match(romName);
            if (!m.Success) continue;

            if (!int.TryParse(m.Groups[1].Value, out int tn)) continue;
            string ext = m.Groups[2].Value.ToLowerInvariant();
            bool isData = ext == "bin" || ext == "iso";

            if (!long.TryParse((string?)rom.Attribute("size") ?? "0", out long size)) continue;
            if (!uint.TryParse((string?)rom.Attribute("crc") ?? "", System.Globalization.NumberStyles.HexNumber, null, out uint crc)) continue;
            string md5Hex = (string?)rom.Attribute("md5") ?? "";
            if (md5Hex.Length != 32) continue;
            byte[] md5;
            try { md5 = Convert.FromHexString(md5Hex); } catch { continue; }

            tracks.Add((tn, isData, size, crc, md5));
            if (tn == 1) t1Crc = crc;
        }

        if (t1Crc == null) { skippedNoT1++; continue; }
        if (tracks.Count == 0) { skippedNoTracks++; continue; }

        tracks.Sort((a, b) => a.Num.CompareTo(b.Num));

        if (byT1Crc.TryGetValue(t1Crc.Value, out var existing))
        {
            collisions++;
            // Prefer "[!]" verified entries over hacks/dumps/etc.
            if (existing.IsVerified && !isVerified) continue;
            if (!existing.IsVerified && isVerified)
            {
                byT1Crc[t1Crc.Value] = (name, tracks, isVerified);
            }
            // Same verification status: keep first.
            continue;
        }
        byT1Crc[t1Crc.Value] = (name, tracks, isVerified);
        inThisDat++;
        totalEntries++;
    }
    Console.WriteLine($"  {Path.GetFileName(datPath)}: +{inThisDat} games");
}

Console.WriteLine();
Console.WriteLine($"Total unique games (by T1 CRC32): {byT1Crc.Count}");
Console.WriteLine($"Skipped (no Track 1): {skippedNoT1}");
Console.WriteLine($"Skipped (no tracks):  {skippedNoTracks}");
Console.WriteLine($"T1 CRC collisions resolved: {collisions}");

using var ms = new MemoryStream();
using var bw = new BinaryWriter(ms);
bw.Write(new byte[] { (byte)'U', (byte)'D', (byte)'P', (byte)'T' });
bw.Write((uint)1);
bw.Write((uint)byT1Crc.Count);

foreach (var (t1, entry) in byT1Crc.OrderBy(kv => kv.Key))
{
    bw.Write(t1);
    var nameBytes = System.Text.Encoding.UTF8.GetBytes(entry.Name);
    bw.Write((ushort)nameBytes.Length);
    bw.Write(nameBytes);
    bw.Write((byte)entry.Tracks.Count);
    foreach (var t in entry.Tracks)
    {
        bw.Write((byte)t.Num);
        bw.Write((byte)(t.IsData ? 1 : 0));
        bw.Write(t.Size);
        bw.Write(t.Crc);
        bw.Write(t.Md5);
    }
}

Console.WriteLine($"Raw blob size: {ms.Length:N0} bytes");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
using (var outFs = File.Create(outPath))
using (var gz = new GZipStream(outFs, CompressionLevel.Optimal, leaveOpen: false))
{
    ms.Position = 0;
    ms.CopyTo(gz);
}
var compressed = new FileInfo(outPath).Length;
Console.WriteLine($"Wrote: {outPath}");
Console.WriteLine($"Gzipped size: {compressed:N0} bytes ({(double)compressed * 100 / ms.Length:F1}% of raw)");
return 0;
