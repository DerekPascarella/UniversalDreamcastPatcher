using System;
using System.IO;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

// File-based encode and decode over the xdelta3 memory API.
// flags=0 uses xdelta3's default behavior.
public static class Xdelta3Codec
{
    private const int ENOSPC = 28;
    private const long MaxOutputSize = 2L * 1024 * 1024 * 1024 - 1024;

    public static void EncodeFile(string originalPath, string modifiedPath, string patchOutPath)
    {
        var source = File.ReadAllBytes(originalPath);
        var input = File.ReadAllBytes(modifiedPath);

        long initialGuess = Math.Max(input.LongLength + 65536, (long)(input.LongLength * 1.5));
        var bytes = Run(isEncode: true, input, source, initialGuess);
        File.WriteAllBytes(patchOutPath, bytes);
    }

    public static void DecodeFile(string originalPath, string patchPath, string outputPath)
    {
        var source = File.ReadAllBytes(originalPath);
        var patch = File.ReadAllBytes(patchPath);

        long initialGuess = Math.Max(source.LongLength * 2 + 65536, 65536);
        var bytes = Run(isEncode: false, patch, source, initialGuess);
        File.WriteAllBytes(outputPath, bytes);
    }

    private static unsafe byte[] Run(bool isEncode, byte[] input, byte[] source, long initialBufferSize)
    {
        long bufSize = Math.Min(initialBufferSize, MaxOutputSize);

        while (true)
        {
            var output = new byte[bufSize];

            int rc;
            nuint written;
            fixed (byte* pInput = input)
            fixed (byte* pSource = source.Length > 0 ? source : new byte[1])
            fixed (byte* pOutput = output)
            {
                nuint srcLen = (nuint)source.LongLength;
                nuint inLen = (nuint)input.LongLength;
                nuint avail = (nuint)output.LongLength;

                if (isEncode)
                {
                    rc = Xdelta3Native.xd3_encode_memory(pInput, inLen, pSource, srcLen,
                        pOutput, out written, avail, flags: 0);
                }
                else
                {
                    rc = Xdelta3Native.xd3_decode_memory(pInput, inLen, pSource, srcLen,
                        pOutput, out written, avail, flags: 0);
                }
            }

            if (rc == 0)
            {
                if ((long)written == output.LongLength) return output;
                var result = new byte[(long)written];
                Buffer.BlockCopy(output, 0, result, 0, (int)written);
                return result;
            }

            if (rc == ENOSPC)
            {
                if (bufSize >= MaxOutputSize)
                    throw new InvalidOperationException(
                        $"xdelta3 output exceeded {MaxOutputSize:N0} bytes while {(isEncode ? "encoding" : "decoding")}.");
                bufSize = Math.Min(bufSize * 2, MaxOutputSize);
                continue;
            }

            throw new InvalidOperationException(
                $"xdelta3 {(isEncode ? "encode" : "decode")} failed with code {rc}.");
        }
    }
}
