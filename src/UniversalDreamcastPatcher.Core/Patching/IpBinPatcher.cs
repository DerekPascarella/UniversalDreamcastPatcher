using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

// Reads and writes IP.BIN fields inside whatever file contains them.
// Locates the 256-byte meta header via the SEGA SEGAKATANA signature,
// and the 92-byte region strings via a 20-byte preamble signature.
// Both spans fit inside one Mode1/2352 sector's user data, so byte
// writes work against raw .bin, GDI tracks, CUE/BIN tracks, and CDI
// files alike without any sector-encoding awareness.
public static class IpBinPatcher
{
    public const int IpBinSize = 0x8000;
    public const int MetaHeaderSize = 0x100;

    // Meta header offsets (relative to the SEGA SEGAKATANA signature).
    private const int HardwareIdOffset = 0x00;
    private const int HardwareIdLength = 16;
    private const int MakerIdOffset = 0x10;
    private const int MakerIdLength = 16;
    private const int DeviceInfoOffset = 0x20;
    private const int DeviceInfoLength = 16;
    private const int AreaSymbolsOffset = 0x30;
    private const int AreaSymbolsLength = 8;
    private const int PeripheralsOffset = 0x38;
    private const int PeripheralsLength = 8;
    private const int ProductNumberOffset = 0x40;
    private const int ProductNumberLength = 10;
    private const int VersionOffset = 0x4A;
    private const int VersionLength = 6;
    private const int ReleaseDateOffset = 0x50;
    private const int ReleaseDateLength = 16;
    private const int BootFilenameOffset = 0x60;
    private const int BootFilenameLength = 16;
    private const int MakerNameOffset = 0x70;
    private const int MakerNameLength = 16;
    private const int SoftwareTitleOffset = 0x80;
    private const int SoftwareTitleLength = 128;

    // Region-strings layout: [JP 28] [sep 4] [US 28] [sep 4] [EU 28] = 92 bytes.
    public const int RegionStringsBlockSize = 92;
    private const int RegionStringLength = 28;
    private const int RegionPreambleSize = 20;

    private const string RegionStringJp = "For JAPAN,TAIWAN,PHILIPINES.";
    private const string RegionStringUs = "For USA and CANADA.";
    private const string RegionStringEu = "For EUROPE.";

    // The four bytes between region strings. Same in every commercial disc.
    private static readonly byte[] RegionStringSeparator = { 0x0E, 0xA0, 0x09, 0x00 };

    // 20 bytes that always sit right before the Japan region string. We
    // signature-scan for this so the search works regardless of whether
    // the file is sector-encoded (GDI/CUE/CDI) or flat (raw .bin).
    private static readonly byte[] RegionPreambleSignature =
    {
        0x00, 0x38, 0x00, 0x70, 0x00, 0xE0, 0x01, 0xC0,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x0E, 0xA0, 0x09, 0x00,
    };

    private static readonly byte[] SegaSignature =
        Encoding.ASCII.GetBytes("SEGA SEGAKATANA SEGA ENTERPRISES");

    // Latin-1 round-trips every byte to a unique char and back, so
    // junk in ASCII fields survives parse + reserialize unchanged.
    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

    // ------ Build Patch flow ------
    // Patch a clean extracted 32,768-byte IP.BIN at fixed offsets. Used
    // by Build Patch, which always extracts to a temp file first.
    private const int LegacyRegionFlagOffset = 48;
    private const int LegacyVgaFlagOffset = 61;
    private const int LegacyGameNameOffset = 128;
    private const int LegacyGameNameLength = 128;
    private const int LegacyRegionStringJpOffset = 14084;
    private const int LegacyRegionStringUsOffset = 14116;
    private const int LegacyRegionStringEuOffset = 14148;

    private static readonly byte[] RegionFlagJue = Encoding.ASCII.GetBytes("JUE");
    private static readonly byte[] VgaFlagOn = { (byte)'1' };

    public static void ApplyRegionFree(string ipBinPath) =>
        InPlace(ipBinPath, stream =>
        {
            WriteAt(stream, LegacyRegionFlagOffset, RegionFlagJue);
            WriteAt(stream, LegacyRegionStringJpOffset, PadRight(RegionStringJp, RegionStringLength));
            WriteAt(stream, LegacyRegionStringUsOffset, PadRight(RegionStringUs, RegionStringLength));
            WriteAt(stream, LegacyRegionStringEuOffset, PadRight(RegionStringEu, RegionStringLength));
        });

    public static void ApplyVga(string ipBinPath) =>
        InPlace(ipBinPath, stream => WriteAt(stream, LegacyVgaFlagOffset, VgaFlagOn));

    public static void ApplyCustomName(string ipBinPath, string gameName)
    {
        if (gameName == null) throw new ArgumentNullException(nameof(gameName));
        var padded = PadRight(gameName, LegacyGameNameLength);
        InPlace(ipBinPath, stream => WriteAt(stream, LegacyGameNameOffset, padded));
    }

    private static void InPlace(string path, Action<FileStream> action)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (fs.Length != IpBinSize)
        {
            throw new InvalidDataException(
                "The IP.BIN file is not a valid Dreamcast boot sector.\n\n" +
                $"It is {fs.Length} bytes; the correct size is {IpBinSize} bytes.");
        }
        action(fs);
    }

    private static void WriteAt(FileStream stream, int offset, byte[] data)
    {
        stream.Position = offset;
        stream.Write(data, 0, data.Length);
    }

    // ------ IP.BIN Editor flow ------

    // Pull a metadata model out of the 256-byte meta header. Doesn't
    // throw on bad data - bogus fields come through as-is and are
    // flagged by IpBinMetadata.Validate() instead.
    public static IpBinMetadata ParseMetaHeader(byte[] metaHeader)
    {
        if (metaHeader == null) throw new ArgumentNullException(nameof(metaHeader));
        if (metaHeader.Length < MetaHeaderSize)
        {
            throw new InvalidDataException(
                $"Meta header block is {metaHeader.Length} bytes; expected at least {MetaHeaderSize}.");
        }

        var metadata = new IpBinMetadata();

        var deviceInfo = ReadText(metaHeader, DeviceInfoOffset, DeviceInfoLength).TrimEnd();
        ParseDeviceInfo(deviceInfo, metadata);

        metadata.RegionJapan = metaHeader[AreaSymbolsOffset + 0] == (byte)'J';
        metadata.RegionUsa = metaHeader[AreaSymbolsOffset + 1] == (byte)'U';
        metadata.RegionEurope = metaHeader[AreaSymbolsOffset + 2] == (byte)'E';

        var peripheralsText = ReadText(metaHeader, PeripheralsOffset, 7);
        metadata.Peripherals = ParsePeripherals(peripheralsText);

        metadata.ProductNumber = TrimPadding(ReadText(metaHeader, ProductNumberOffset, ProductNumberLength));
        metadata.Version = TrimPadding(ReadText(metaHeader, VersionOffset, VersionLength));
        metadata.ReleaseDate = TrimPadding(ReadText(metaHeader, ReleaseDateOffset, 8));
        metadata.BootFilename = TrimPadding(ReadText(metaHeader, BootFilenameOffset, BootFilenameLength));
        metadata.MakerName = TrimPadding(ReadText(metaHeader, MakerNameOffset, MakerNameLength));
        metadata.SoftwareTitle = TrimPadding(ReadText(metaHeader, SoftwareTitleOffset, SoftwareTitleLength));

        return metadata;
    }

    // Build the 256-byte meta header from a metadata model. Hardware
    // ID and Maker ID get forced back to their fixed values (so a
    // malformed source ends up cleaned up on save), and the Device Info
    // CRC is recomputed from Product + Version after they're written.
    public static byte[] SerializeMetaHeader(IpBinMetadata metadata)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));

        var output = new byte[MetaHeaderSize];

        WriteAsciiPadded(output, HardwareIdOffset, IpBinMetadata.HardwareIdConst, HardwareIdLength);
        WriteAsciiPadded(output, MakerIdOffset, IpBinMetadata.MakerIdConst, MakerIdLength);

        var media = metadata.MediaType == IpBinMediaType.CdRom ? "CD-ROM" : "GD-ROM";
        var deviceInfo = $"0000 {media}{ClampDigit(metadata.DiscNumber)}/{ClampDigit(metadata.DiscCount)}";
        WriteAsciiPadded(output, DeviceInfoOffset, deviceInfo, DeviceInfoLength);

        for (var i = 0; i < AreaSymbolsLength; i++) output[AreaSymbolsOffset + i] = (byte)' ';
        if (metadata.RegionJapan) output[AreaSymbolsOffset + 0] = (byte)'J';
        if (metadata.RegionUsa) output[AreaSymbolsOffset + 1] = (byte)'U';
        if (metadata.RegionEurope) output[AreaSymbolsOffset + 2] = (byte)'E';

        var peripheralsHex = ((uint)metadata.Peripherals).ToString("X7");
        WriteAsciiPadded(output, PeripheralsOffset, peripheralsHex, PeripheralsLength);

        WriteAsciiPadded(output, ProductNumberOffset, metadata.ProductNumber, ProductNumberLength);
        WriteAsciiPadded(output, VersionOffset, metadata.Version, VersionLength);
        WriteAsciiPadded(output, ReleaseDateOffset, metadata.ReleaseDate, ReleaseDateLength);
        WriteAsciiPadded(output, BootFilenameOffset, metadata.BootFilename, BootFilenameLength);
        WriteAsciiPadded(output, MakerNameOffset, metadata.MakerName, MakerNameLength);
        WriteAsciiPadded(output, SoftwareTitleOffset, metadata.SoftwareTitle, SoftwareTitleLength);

        var crc = ComputeDeviceInfoCrc(output);
        var crcText = crc.ToString("X4");
        for (var i = 0; i < 4; i++) output[DeviceInfoOffset + i] = (byte)crcText[i];

        return output;
    }

    // Build the 92-byte region-strings block. Each 28-byte slot gets
    // its canonical string when the region is checked, or 28 spaces
    // when it isn't. The two 4-byte separators are always the same.
    public static byte[] SerializeRegionStrings(IpBinMetadata metadata)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        var output = new byte[RegionStringsBlockSize];

        WriteAsciiPadded(output, 0, metadata.RegionJapan ? RegionStringJp : string.Empty, RegionStringLength);
        Array.Copy(RegionStringSeparator, 0, output, RegionStringLength, RegionStringSeparator.Length);
        WriteAsciiPadded(output, RegionStringLength + 4, metadata.RegionUsa ? RegionStringUs : string.Empty, RegionStringLength);
        Array.Copy(RegionStringSeparator, 0, output, 2 * RegionStringLength + 4, RegionStringSeparator.Length);
        WriteAsciiPadded(output, 2 * RegionStringLength + 8, metadata.RegionEurope ? RegionStringEu : string.Empty, RegionStringLength);

        return output;
    }

    // Returns every offset where "SEGA SEGAKATANA SEGA ENTERPRISES"
    // appears in the file. Streams in chunks so a 700 MB data track
    // doesn't get pulled into memory. Progress reports byte position
    // as a 0-100% int.
    public static List<long> FindMetaHeaderOffsets(string path, IProgress<int>? progress = null) =>
        FindSignature(path, SegaSignature, progress);

    // Returns every offset where the 20-byte region-string preamble
    // appears. The Japan string starts at offset + 20.
    public static List<long> FindRegionPreambleOffsets(string path, IProgress<int>? progress = null) =>
        FindSignature(path, RegionPreambleSignature, progress);

    // Read N bytes starting at a given offset.
    public static byte[] ReadAt(string path, long offset, int length)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = fs.Read(buffer, read, length - read);
            if (n <= 0)
            {
                throw new InvalidDataException(
                    $"Truncated read: got {read} of {length} bytes at offset {offset} of \"{path}\".");
            }
            read += n;
        }
        return buffer;
    }

    // Overwrite bytes starting at a given offset.
    public static void WriteAt(string path, long offset, byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        fs.Seek(offset, SeekOrigin.Begin);
        fs.Write(data, 0, data.Length);
    }

    private static List<long> FindSignature(string path, byte[] signature, IProgress<int>? progress = null)
    {
        if (!File.Exists(path)) return new List<long>();

        var offsets = new List<long>();
        const int chunkSize = 1 << 20; // 1 MiB
        var overlap = signature.Length - 1;
        var buffer = new byte[chunkSize + overlap];

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var totalBytes = fs.Length;
        long position = 0;
        var carry = 0;
        int lastReported = -1;

        while (true)
        {
            var read = fs.Read(buffer, carry, chunkSize);
            if (read == 0) break;
            var validLength = carry + read;

            for (var i = 0; i <= validLength - signature.Length; i++)
            {
                var match = true;
                for (var j = 0; j < signature.Length; j++)
                {
                    if (buffer[i + j] != signature[j]) { match = false; break; }
                }
                if (match) offsets.Add(position + i);
            }

            if (validLength >= overlap)
            {
                Array.Copy(buffer, validLength - overlap, buffer, 0, overlap);
                position += validLength - overlap;
                carry = overlap;
            }
            else
            {
                position += validLength;
                carry = 0;
            }

            if (progress != null && totalBytes > 0)
            {
                int pct = (int)(position * 100 / totalBytes);
                if (pct != lastReported)
                {
                    progress.Report(pct);
                    lastReported = pct;
                }
            }
        }

        if (progress != null && lastReported != 100)
            progress.Report(100);

        return offsets;
    }

    private static string ReadText(byte[] block, int offset, int length) =>
        Latin1.GetString(block, offset, length);

    // Strip space and control bytes from both ends. Mid-string control
    // bytes survive so the validator can still flag them.
    private static string TrimPadding(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        int start = 0, end = s.Length;
        while (start < end && IsPaddingChar(s[start])) start++;
        while (end > start && IsPaddingChar(s[end - 1])) end--;
        return s.Substring(start, end - start);
    }

    private static bool IsPaddingChar(char c) => c <= 0x20;

    private static void WriteAsciiPadded(byte[] block, int offset, string value, int length)
    {
        value ??= string.Empty;
        var bytes = Latin1.GetBytes(value);
        for (var i = 0; i < length; i++)
        {
            block[offset + i] = i < bytes.Length ? bytes[i] : (byte)' ';
        }
    }

    private static byte[] PadRight(string s, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        var result = new byte[length];
        Array.Fill(result, (byte)' ');
        Array.Copy(bytes, 0, result, 0, Math.Min(bytes.Length, length));
        return result;
    }

    private static readonly Regex DeviceInfoRule = new(
        @"^[0-9A-F]{4}\s+(GD|CD)-ROM(\d)/(\d)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static void ParseDeviceInfo(string deviceInfo, IpBinMetadata metadata)
    {
        var match = DeviceInfoRule.Match(deviceInfo);
        if (!match.Success) return;
        metadata.MediaType = match.Groups[1].Value.Equals("CD", StringComparison.OrdinalIgnoreCase)
            ? IpBinMediaType.CdRom
            : IpBinMediaType.GdRom;
        var num = int.Parse(match.Groups[2].Value);
        var count = int.Parse(match.Groups[3].Value);
        // 0 isn't a valid disc number or count - leave the defaults (1/1).
        if (num >= 1 && count >= 1)
        {
            metadata.DiscNumber = num;
            metadata.DiscCount = count;
        }
    }

    private static IpBinPeripherals ParsePeripherals(string text)
    {
        var sb = new StringBuilder(7);
        foreach (var c in text)
        {
            if (IsHexDigit(c)) sb.Append(c);
            else if (sb.Length > 0) break;
        }
        if (sb.Length == 0) return IpBinPeripherals.None;
        if (uint.TryParse(sb.ToString(), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var bits))
        {
            return (IpBinPeripherals)bits;
        }
        return IpBinPeripherals.None;
    }

    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');

    private static int ClampDigit(int n) => n < 1 ? 1 : n > 9 ? 9 : n;

    // 16-bit checksum over Product Number + Version (16 bytes at 0x40).
    // Algorithm matches KallistiOS makeip (src/field.c). The 4-digit hex
    // result goes at 0x20-0x23.
    private static ushort ComputeDeviceInfoCrc(byte[] block)
    {
        var n = 0xFFFF;
        for (var i = 0; i < 16; i++)
        {
            n ^= block[ProductNumberOffset + i] << 8;
            for (var c = 0; c < 8; c++)
            {
                n = (n & 0x8000) != 0 ? (n << 1) ^ 4129 : n << 1;
            }
        }
        return (ushort)(n & 0xFFFF);
    }
}
