using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public sealed record RedumpTrackEntry(int TrackNumber, bool IsDataInDat, long Size, uint Crc32, byte[] Md5);

public sealed class RedumpDiscEntry
{
    public uint T1Crc32 { get; init; }
    public string GameName { get; init; } = string.Empty;
    public IReadOnlyList<RedumpTrackEntry> Tracks { get; init; } = Array.Empty<RedumpTrackEntry>();
}

// Loads the embedded compact Redump DC DAT (one row per game, keyed by Track 1
// CRC32) into memory on first use, then serves O(1) lookups. The blob is
// produced by _tools/RedumpDatCompactor from the official Redump XML DAT.
public static class RedumpDatLookup
{
    private const string ResourceName = "UniversalDreamcastPatcher.Core.Resources.redump_dc.bin.gz";
    private static readonly object _initLock = new();
    private static Dictionary<uint, RedumpDiscEntry>? _byT1Crc;

    public static RedumpDiscEntry? LookupByT1Crc32(uint t1Crc32)
    {
        EnsureLoaded();
        return _byT1Crc!.TryGetValue(t1Crc32, out var entry) ? entry : null;
    }

    public static int EntryCount
    {
        get { EnsureLoaded(); return _byT1Crc!.Count; }
    }

    private static void EnsureLoaded()
    {
        if (_byT1Crc != null) return;
        lock (_initLock)
        {
            if (_byT1Crc != null) return;
            _byT1Crc = Load();
        }
    }

    private static Dictionary<uint, RedumpDiscEntry> Load()
    {
        var asm = typeof(RedumpDatLookup).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource missing: {ResourceName}");
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        ms.Position = 0;
        using var br = new BinaryReader(ms);

        // Header.
        var magic = br.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != 'U' || magic[1] != 'D' || magic[2] != 'P' || magic[3] != 'R')
            throw new InvalidDataException("Redump DAT blob magic mismatch.");
        uint version = br.ReadUInt32();
        if (version != 1)
            throw new InvalidDataException($"Unsupported Redump DAT blob version: {version}");
        uint gameCount = br.ReadUInt32();

        var result = new Dictionary<uint, RedumpDiscEntry>((int)gameCount);
        for (uint g = 0; g < gameCount; g++)
        {
            uint t1Crc = br.ReadUInt32();
            ushort nameLen = br.ReadUInt16();
            string name = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
            byte trackCount = br.ReadByte();
            var tracks = new RedumpTrackEntry[trackCount];
            for (int t = 0; t < trackCount; t++)
            {
                byte trackNum = br.ReadByte();
                byte isData = br.ReadByte();
                long size = br.ReadInt64();
                uint crc = br.ReadUInt32();
                byte[] md5 = br.ReadBytes(16);
                tracks[t] = new RedumpTrackEntry(trackNum, isData != 0, size, crc, md5);
            }
            // Last write wins on T1 CRC collision. Collisions across Redump
            // entries are vanishingly rare since T1 contains IP.BIN which
            // varies per disc (title, product code, region byte).
            result[t1Crc] = new RedumpDiscEntry
            {
                T1Crc32 = t1Crc,
                GameName = name,
                Tracks = tracks,
            };
        }
        return result;
    }
}
