using System;
using System.IO;
using System.Text;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

// Byte-offset writes into an IP.BIN: region-free, VGA, and custom game name.
public static class IpBinPatcher
{
    private const int IpBinSize = 0x8000;

    private const int RegionFlagOffset = 48;
    private const int VgaFlagOffset = 61;
    private const int GameNameOffset = 128;
    private const int GameNameLength = 128;
    private const int RegionStringJPAll = 14084;
    private const int RegionStringUSA = 14116;
    private const int RegionStringEUR = 14148;
    private const int RegionStringLength = 28;

    private static readonly byte[] RegionFlagJue = Encoding.ASCII.GetBytes("JUE");
    private static readonly byte[] VgaFlagOn = new byte[] { (byte)'1' };

    public static void ApplyRegionFree(string ipBinPath) =>
        InPlace(ipBinPath, stream =>
        {
            WriteAt(stream, RegionFlagOffset, RegionFlagJue);
            WriteAt(stream, RegionStringJPAll, PadRight("For JAPAN,TAIWAN,PHILIPINES.", RegionStringLength));
            WriteAt(stream, RegionStringUSA, PadRight("For USA and CANADA.", RegionStringLength));
            WriteAt(stream, RegionStringEUR, PadRight("For EUROPE.", RegionStringLength));
        });

    public static void ApplyVga(string ipBinPath) =>
        InPlace(ipBinPath, stream => WriteAt(stream, VgaFlagOffset, VgaFlagOn));

    public static void ApplyCustomName(string ipBinPath, string gameName)
    {
        if (gameName == null) throw new ArgumentNullException(nameof(gameName));
        var padded = PadRight(gameName, GameNameLength);
        InPlace(ipBinPath, stream => WriteAt(stream, GameNameOffset, padded));
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

    private static byte[] PadRight(string s, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(s);
        var result = new byte[length];
        Array.Fill(result, (byte)' ');
        Array.Copy(bytes, 0, result, 0, Math.Min(bytes.Length, length));
        return result;
    }
}
