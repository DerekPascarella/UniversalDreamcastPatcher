using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core;

// Settings file. Lives next to the executable on Windows/Linux,
// and in ~/Library/Application Support/UniversalDreamcastPatcher/ on macOS
// (since .app bundles are read-only).
public class AppSettings
{
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public string SkippedUpdateVersion { get; set; } = "";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Returns the directory the .exe was launched from.
    // AppContext.BaseDirectory can't be used here: under PublishSingleFile +
    // IncludeAllContentForSelfExtract it returns the %TEMP% extraction path.
    public static string GetAppDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
                return dir;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string GetSettingsDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", Constants.AppExecutableBase);
        }
        return GetAppDirectory();
    }

    private static string GetSettingsPath() => Path.Combine(GetSettingsDir(), "settings.json");

    public static AppSettings Load()
    {
        string path = GetSettingsPath();
        if (!File.Exists(path)) return new AppSettings();

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            string dir = GetSettingsDir();
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch
        {
            // settings file might be read-only or inaccessible
        }
    }
}
