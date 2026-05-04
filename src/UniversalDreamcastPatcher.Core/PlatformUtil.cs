using System.Diagnostics;
using System.Runtime.InteropServices;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core;

public static class PlatformUtil
{
    public static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
        }
        catch { }
    }
}
