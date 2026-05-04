using System;
using System.IO;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core.Patching;

// Pinning filesystem timestamps and GDromBuilder.BuildDate to a fixed epoch
// makes rebuilt GDIs byte-for-byte reproducible.
public static class Determinism
{
    public static readonly DateTime FixedEpoch =
        new DateTime(1999, 9, 9, 12, 12, 12, DateTimeKind.Utc);

    public static void ApplyEpochToTree(string rootDir)
    {
        if (!Directory.Exists(rootDir))
            throw new DirectoryNotFoundException(rootDir);

        foreach (var dir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.AllDirectories))
        {
            Directory.SetCreationTimeUtc(dir, FixedEpoch);
            Directory.SetLastAccessTimeUtc(dir, FixedEpoch);
            Directory.SetLastWriteTimeUtc(dir, FixedEpoch);
        }

        foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
        {
            File.SetCreationTimeUtc(file, FixedEpoch);
            File.SetLastAccessTimeUtc(file, FixedEpoch);
            File.SetLastWriteTimeUtc(file, FixedEpoch);
        }

        Directory.SetCreationTimeUtc(rootDir, FixedEpoch);
        Directory.SetLastAccessTimeUtc(rootDir, FixedEpoch);
        Directory.SetLastWriteTimeUtc(rootDir, FixedEpoch);
    }
}
