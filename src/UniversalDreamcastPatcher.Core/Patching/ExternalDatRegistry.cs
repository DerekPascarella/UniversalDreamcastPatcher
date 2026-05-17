using System;
using System.Collections.Generic;
using System.IO;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

// Process-wide pool of external Logiqx DATs. When DAT source is "External",
// internal lookups consult this pool first and fall back to the embedded
// blobs on miss. Populated from AppSettings on startup and rewritten by the
// Manage External DATs window.
public static class ExternalDatRegistry
{
    private static readonly object _lock = new();
    private static List<ExternalDatFile> _files = new();
    private static bool _enabled;

    public static bool IsEnabled
    {
        get { lock (_lock) return _enabled; }
        set { lock (_lock) _enabled = value; }
    }

    public static IReadOnlyList<ExternalDatFile> Files
    {
        get { lock (_lock) return _files.ToArray(); }
    }

    public static bool HasMissingFiles
    {
        get
        {
            lock (_lock)
            {
                foreach (var f in _files) if (f.IsMissing) return true;
                return false;
            }
        }
    }

    public static void LoadFromSettings(AppSettings settings)
    {
        var files = new List<ExternalDatFile>(settings.ExternalDatPaths.Count);
        foreach (var path in settings.ExternalDatPaths)
        {
            if (!File.Exists(path))
            {
                files.Add(ExternalDatFile.MissingPlaceholder(path));
                continue;
            }
            var loaded = ExternalDatFile.TryLoad(path, out _);
            files.Add(loaded ?? ExternalDatFile.MissingPlaceholder(path));
        }
        lock (_lock)
        {
            _files = files;
            _enabled = string.Equals(settings.DatSource, "External", StringComparison.OrdinalIgnoreCase);
        }
    }

    // Replaces the pool wholesale. Called by the Manage window after the user
    // adds, removes, reorders, or clears entries.
    public static void SetFiles(IEnumerable<ExternalDatFile> files)
    {
        lock (_lock) _files = new List<ExternalDatFile>(files);
    }

    public static ExternalDiscEntry? LookupByT1Crc32(uint t1Crc32)
    {
        lock (_lock)
        {
            if (!_enabled) return null;
            foreach (var f in _files)
            {
                if (f.IsMissing) continue;
                var hit = f.LookupByT1Crc32(t1Crc32);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
