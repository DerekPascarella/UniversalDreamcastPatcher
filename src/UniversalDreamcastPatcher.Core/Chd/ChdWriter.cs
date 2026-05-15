using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core
{
    // P/Invoke around libchdw. Pass a .gdi for GD-ROM output or a .cue
    // for CD-ROM output. libchdw decides based on the extension.
    public static class ChdWriter
    {
        private const string LibraryName = "libchdw";

        public static async Task<(bool Success, string Message)> ConvertToChd(
            string inputGdiOrCuePath,
            string outputChdPath,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(inputGdiOrCuePath))
                return (false, $"Input file not found: {inputGdiOrCuePath}");

            var outputDir = Path.GetDirectoryName(outputChdPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            // libchdw refuses to overwrite an existing .chd.
            if (File.Exists(outputChdPath))
                File.Delete(outputChdPath);

            int lastReported = -1;
            ProgressCallback callback = (user, percent) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return 1;

                if (progress != null)
                {
                    int p = (int)percent;
                    if (p != lastReported)
                    {
                        lastReported = p;
                        progress.Report(p);
                    }
                }
                return 0;
            };

            int rc = await Task.Run(() =>
                chdw_create_cd_chd(inputGdiOrCuePath, outputChdPath, callback, IntPtr.Zero),
                cancellationToken);

            GC.KeepAlive(callback);

            if (rc == 0)
                return (true, null);

            string nativeMessage = chdw_last_error_safe();
            if (rc == CHDW_ERR_CANCELLED)
                return (false, "Compression was cancelled");

            // Drop the partial file so a retry isn't blocked by the existing-file check.
            try { if (File.Exists(outputChdPath)) File.Delete(outputChdPath); }
            catch { }

            return (false, string.IsNullOrEmpty(nativeMessage)
                ? $"libchdw returned error {rc}"
                : nativeMessage);
        }

        private static string chdw_last_error_safe()
        {
            try
            {
                IntPtr ptr = chdw_last_error();
                return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
            }
            catch
            {
                return null;
            }
        }

        // Value from libchdw.h.
        private const int CHDW_ERR_CANCELLED = 6;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ProgressCallback(IntPtr user, double percent);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int chdw_create_cd_chd(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string inputPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string outputChdPath,
            ProgressCallback callback,
            IntPtr userData);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr chdw_last_error();
    }
}
