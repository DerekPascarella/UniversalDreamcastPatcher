using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

public sealed record TosecTrackEntry(int TrackNumber, bool IsData, long Size, uint Crc32, byte[] Md5);

public sealed class TosecDiscEntry
{
    public uint T1Crc32 { get; init; }
    public string GameName { get; init; } = string.Empty;
    public IReadOnlyList<TosecTrackEntry> Tracks { get; init; } = Array.Empty<TosecTrackEntry>();
}

// Loads the embedded compact TOSEC DC blob (aggregated across all 31 DC DATs:
// JP/US/PAL games, demos, applications, dev builds, homebrew, multimedia,
// samplers, etc.) into memory on first use, then serves O(1) lookups by
// Track 1 CRC32. The blob is produced by _tools/TosecDatCompactor.
public static class TosecDatLookup
{
    private const string ResourceName = "UniversalDreamcastPatcher.Core.Resources.tosec_dc.bin.gz";
    private static readonly object _initLock = new();
    private static Dictionary<uint, TosecDiscEntry>? _byT1Crc;

    public static TosecDiscEntry? LookupByT1Crc32(uint t1Crc32)
    {
        var external = ExternalDatRegistry.LookupByT1Crc32(t1Crc32);
        if (external != null) return external.ToTosec();

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

    private static Dictionary<uint, TosecDiscEntry> Load()
    {
        var asm = typeof(TosecDatLookup).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource missing: {ResourceName}");
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        ms.Position = 0;
        using var br = new BinaryReader(ms);

        var magic = br.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != 'U' || magic[1] != 'D' || magic[2] != 'P' || magic[3] != 'T')
            throw new InvalidDataException("TOSEC DAT blob magic mismatch.");
        uint version = br.ReadUInt32();
        if (version != 1)
            throw new InvalidDataException($"Unsupported TOSEC DAT blob version: {version}");
        uint gameCount = br.ReadUInt32();

        var result = new Dictionary<uint, TosecDiscEntry>((int)gameCount);
        for (uint g = 0; g < gameCount; g++)
        {
            uint t1Crc = br.ReadUInt32();
            ushort nameLen = br.ReadUInt16();
            string name = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
            byte trackCount = br.ReadByte();
            var tracks = new TosecTrackEntry[trackCount];
            for (int t = 0; t < trackCount; t++)
            {
                byte trackNum = br.ReadByte();
                byte isData = br.ReadByte();
                long size = br.ReadInt64();
                uint crc = br.ReadUInt32();
                byte[] md5 = br.ReadBytes(16);
                tracks[t] = new TosecTrackEntry(trackNum, isData != 0, size, crc, md5);
            }
            result[t1Crc] = new TosecDiscEntry
            {
                T1Crc32 = t1Crc,
                GameName = name,
                Tracks = tracks,
            };
        }
        return result;
    }
}
