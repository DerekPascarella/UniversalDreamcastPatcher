using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// One-off tool: reads the Redump DC Logiqx XML DAT and emits a compact
// gzip-compressed binary blob for embedding into UDP.Core.
//
// Output blob format (all little-endian):
//   magic "UDPR" (4 bytes)
//   version u32 = 1
//   game_count u32
//   per game:
//     t1_crc32 u32          (lookup key)
//     name_length u16
//     name_utf8 bytes
//     track_count u8
//     per track:
//       track_number u8
//       is_data u8           (1 = MODE1/2352, 0 = AUDIO)
//       size i64
//       crc32 u32
//       md5 (16 bytes)
//
// Usage: RedumpDatCompactor <input.dat> <output.bin.gz>

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: RedumpDatCompactor <input.dat> <output.bin.gz>");
    return 2;
}

string inPath = args[0];
string outPath = args[1];

var doc = XDocument.Load(inPath);
var games = doc.Descendants("game").ToList();

Console.WriteLine($"Loaded {games.Count} games from DAT.");

using var ms = new MemoryStream();
using var bw = new BinaryWriter(ms);

bw.Write(new byte[] { (byte)'U', (byte)'D', (byte)'P', (byte)'R' });
bw.Write((uint)1);

int countPos = (int)ms.Position;
bw.Write((uint)0);

int written = 0;
int skippedNoT1 = 0;
int skippedNoTracks = 0;
foreach (var game in games)
{
    var name = (string?)game.Attribute("name") ?? "<unknown>";
    var roms = game.Elements("rom").ToList();

    uint? t1Crc = null;
    var tracks = new List<(int Num, bool IsData, long Size, uint Crc, byte[] Md5)>();
    foreach (var rom in roms)
    {
        string romName = (string?)rom.Attribute("name") ?? "";
        var m = Regex.Match(romName, @"\(Track\s+(\d+)\)\.bin$", RegexOptions.IgnoreCase);
        if (!m.Success) continue;
        int tn = int.Parse(m.Groups[1].Value);
        long size = long.Parse((string?)rom.Attribute("size") ?? "0");
        uint crc = uint.Parse((string?)rom.Attribute("crc") ?? "0", System.Globalization.NumberStyles.HexNumber);
        string md5Hex = (string?)rom.Attribute("md5") ?? "";
        if (md5Hex.Length != 32) continue;
        byte[] md5 = Convert.FromHexString(md5Hex);
        // Heuristic: data tracks are .bin in 2352-sector form, but DAT doesn't
        // distinguish. Track 1 is always data; track count >= 3 means tracks 3+
        // could be either. We don't actually need is_data here since the GDI
        // tells us the type at lookup time, so just default to 0 and let the
        // caller decide. We'll keep the field for future-proofing.
        tracks.Add((tn, tn == 1, size, crc, md5));
        if (tn == 1) t1Crc = crc;
    }

    if (t1Crc == null) { skippedNoT1++; continue; }
    if (tracks.Count == 0) { skippedNoTracks++; continue; }

    tracks.Sort((a, b) => a.Num.CompareTo(b.Num));

    bw.Write(t1Crc.Value);
    var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
    bw.Write((ushort)nameBytes.Length);
    bw.Write(nameBytes);
    bw.Write((byte)tracks.Count);
    foreach (var t in tracks)
    {
        bw.Write((byte)t.Num);
        bw.Write((byte)(t.IsData ? 1 : 0));
        bw.Write(t.Size);
        bw.Write(t.Crc);
        bw.Write(t.Md5);
    }
    written++;
}

long endPos = ms.Position;
ms.Position = countPos;
bw.Write((uint)written);
ms.Position = endPos;

Console.WriteLine($"Encoded {written} games (skipped {skippedNoT1} no-T1, {skippedNoTracks} no-tracks).");
Console.WriteLine($"Raw blob size: {ms.Length:N0} bytes");

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
using var outFs = File.Create(outPath);
using var gz = new GZipStream(outFs, CompressionLevel.Optimal, leaveOpen: false);
ms.Position = 0;
ms.CopyTo(gz);
gz.Flush();

var compressedSize = new FileInfo(outPath).Length;
Console.WriteLine($"Wrote: {outPath}");
Console.WriteLine($"Gzipped size: {compressedSize:N0} bytes ({(double)compressedSize * 100 / ms.Length:F1}% of raw)");
return 0;
