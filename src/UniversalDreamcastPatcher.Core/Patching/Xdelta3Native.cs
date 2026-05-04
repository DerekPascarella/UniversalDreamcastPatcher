using System.Runtime.InteropServices;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

// Returns 0 on success, 28 (ENOSPC) if output_buffer is too small, or an xdelta3 error code.
internal static partial class Xdelta3Native
{
    private const string Lib = "xdelta3";

    [LibraryImport(Lib)]
    internal static unsafe partial int xd3_encode_memory(
        byte* input,
        nuint input_size,
        byte* source,
        nuint source_size,
        byte* output_buffer,
        out nuint output_size,
        nuint avail_output,
        int flags);

    [LibraryImport(Lib)]
    internal static unsafe partial int xd3_decode_memory(
        byte* input,
        nuint input_size,
        byte* source,
        nuint source_size,
        byte* output_buffer,
        out nuint output_size,
        nuint avail_output,
        int flags);
}
