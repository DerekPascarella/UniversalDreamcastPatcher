using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core
{
    public enum IpBinFileFormat
    {
        RawIpBin,
        Gdi,
        CueBin,
        Cdi,
        ChdGdRom,
    }

    // A single (file, offset) pair. Used for both meta-header and
    // region-string locations.
    public sealed class IpBinFileLocation
    {
        public string FilePath { get; init; } = string.Empty;
        public long Offset { get; init; }
    }

    // One IP.BIN editing target, regardless of whether the underlying
    // file is a raw .bin, GDI, CUE/BIN, CDI, or CHD. Save writes meta
    // header to every detected copy and region strings to every region
    // preamble found. The LDA in commercial discs has many header
    // copies but no paired region preambles, hence the two lists.
    public abstract class IpBinFileSource : IDisposable
    {
        public string SourcePath { get; protected set; } = string.Empty;
        public IpBinFileFormat Format { get; protected set; }

        public List<IpBinFileLocation> MetaHeaderLocations { get; } = new();
        public List<IpBinFileLocation> RegionStringLocations { get; } = new();

        public int CopyCount => MetaHeaderLocations.Count;

        public bool HasIpBin => MetaHeaderLocations.Count > 0;

        public IpBinMetadata LoadFirst()
        {
            if (MetaHeaderLocations.Count == 0)
                throw new InvalidOperationException("No IP.BIN copies were detected in the source.");
            var first = MetaHeaderLocations[0];
            var metaBytes = IpBinPatcher.ReadAt(
                first.FilePath, first.Offset, IpBinPatcher.MetaHeaderSize);
            return IpBinPatcher.ParseMetaHeader(metaBytes);
        }

        public abstract Task SaveAllAsync(
            IpBinMetadata metadata,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default);

        public virtual void Dispose() { }

        // Only CHD subclass uses progress (decompression % per track). Other
        // subclasses scan synchronously and ignore it.
        protected internal abstract Task ScanAsync(string path, IProgress<string>? progress, CancellationToken cancellationToken);

        public static async Task<IpBinFileSource> OpenAsync(
            string sourcePath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path is empty.", nameof(sourcePath));
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source file not found.", sourcePath);

            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            IpBinFileSource src = ext switch
            {
                ".gdi" => new GdiIpBinSource(),
                ".cue" => new CueBinIpBinSource(),
                ".cdi" => new CdiIpBinSource(),
                ".chd" => new ChdIpBinSource(),
                ".bin" => OpenBinAmbiguous(sourcePath),
                _ => throw new InvalidDataException(
                    $"Unsupported source extension: \"{ext}\". Use .gdi, .cue, .cdi, .chd, or a raw .bin IP.BIN."),
            };
            try
            {
                await src.ScanAsync(sourcePath, progress, cancellationToken);
            }
            catch
            {
                src.Dispose();
                throw;
            }
            return src;
        }

        private static IpBinFileSource OpenBinAmbiguous(string path)
        {
            var len = new FileInfo(path).Length;
            if (len == IpBinPatcher.IpBinSize)
                return new RawIpBinSource();
            throw new InvalidDataException(
                $"\"{Path.GetFileName(path)}\" is {len} bytes; a raw IP.BIN must be exactly {IpBinPatcher.IpBinSize} bytes. " +
                "If this is a CUE/BIN track, open the .cue file instead.");
        }

        // Scan one track file for both signatures and append every hit.
        // Two passes (meta header + region preamble) over the same file. The
        // progress callback gets 0-50% during the first pass and 50-100% during
        // the second.
        protected void AccumulateLocationsFromFile(string filePath, IProgress<int>? progress = null)
        {
            var metaProgress = progress == null ? null : new Progress<int>(p => progress.Report(p / 2));
            var regionProgress = progress == null ? null : new Progress<int>(p => progress.Report(50 + p / 2));

            foreach (var off in IpBinPatcher.FindMetaHeaderOffsets(filePath, metaProgress))
                MetaHeaderLocations.Add(new IpBinFileLocation { FilePath = filePath, Offset = off });
            foreach (var off in IpBinPatcher.FindRegionPreambleOffsets(filePath, regionProgress))
                RegionStringLocations.Add(new IpBinFileLocation { FilePath = filePath, Offset = off + 20 });
        }

        protected async Task WriteMetadataAcrossSourceAsync(
            IpBinMetadata metadata, CancellationToken cancellationToken)
        {
            var metaBytes = IpBinPatcher.SerializeMetaHeader(metadata);
            var regionBytes = IpBinPatcher.SerializeRegionStrings(metadata);
            await Task.Run(() =>
            {
                foreach (var loc in MetaHeaderLocations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IpBinPatcher.WriteAt(loc.FilePath, loc.Offset, metaBytes);
                }
                foreach (var loc in RegionStringLocations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IpBinPatcher.WriteAt(loc.FilePath, loc.Offset, regionBytes);
                }
            }, cancellationToken);
        }
    }

    // ---- Raw .bin ---------------------------------------------------------

    // Flat 32,768-byte IP.BIN file. Uses the same signature-scan path
    // as a disc image, so a 32,768-byte file that doesn't start with
    // SEGA SEGAKATANA fails cleanly instead of pretending to be valid.
    public sealed class RawIpBinSource : IpBinFileSource
    {
        protected internal override Task ScanAsync(string path, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            SourcePath = path;
            Format = IpBinFileFormat.RawIpBin;
            var byteProgress = progress == null ? null : new Progress<int>(p => progress.Report($"Loading source... {p}%"));
            AccumulateLocationsFromFile(path, byteProgress);
            return Task.CompletedTask;
        }

        public override async Task SaveAllAsync(
            IpBinMetadata metadata, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            await WriteMetadataAcrossSourceAsync(metadata, cancellationToken);
            progress?.Report(100);
        }
    }

    // ---- GDI --------------------------------------------------------------

    public sealed class GdiIpBinSource : IpBinFileSource
    {
        protected internal override Task ScanAsync(string path, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            SourcePath = path;
            Format = IpBinFileFormat.Gdi;
            var baseDir = Path.GetDirectoryName(path) ?? string.Empty;

            var tracks = ParseGdiTrackFiles(path).ToList();
            for (int i = 0; i < tracks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var trackPath = Path.Combine(baseDir, tracks[i]);
                if (!File.Exists(trackPath)) continue;

                int trackIndex = i;
                int total = tracks.Count;
                var byteProgress = progress == null ? null : new Progress<int>(p =>
                    progress.Report($"Loading source... {(trackIndex * 100 + p) / total}%"));
                AccumulateLocationsFromFile(trackPath, byteProgress);
            }
            return Task.CompletedTask;
        }

        public override async Task SaveAllAsync(
            IpBinMetadata metadata, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            await WriteMetadataAcrossSourceAsync(metadata, cancellationToken);
            progress?.Report(100);
        }

        private static IEnumerable<string> ParseGdiTrackFiles(string gdiPath)
        {
            foreach (var rawLine in File.ReadAllLines(gdiPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                foreach (var part in TokenizeGdiLine(line))
                {
                    var lower = part.ToLowerInvariant();
                    if (lower.EndsWith(".bin") || lower.EndsWith(".iso"))
                        yield return part;
                }
            }
        }

        private static List<string> TokenizeGdiLine(string line)
        {
            var parts = new List<string>();
            var sb = new System.Text.StringBuilder();
            var inQuotes = false;
            foreach (var c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ' ' && !inQuotes)
                {
                    if (sb.Length > 0) { parts.Add(sb.ToString()); sb.Clear(); }
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0) parts.Add(sb.ToString());
            return parts;
        }
    }

    // ---- Redump CUE/BIN ---------------------------------------------------

    public sealed class CueBinIpBinSource : IpBinFileSource
    {
        protected internal override Task ScanAsync(string path, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            SourcePath = path;
            Format = IpBinFileFormat.CueBin;
            var parser = new CueSheetParser();
            parser.Parse(path);
            var dir = Path.GetDirectoryName(path) ?? string.Empty;

            // First pass collects unique data track files so progress can be
            // weighted across them.
            var dataTracks = new List<string>();
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var track in parser.Tracks)
            {
                if (!track.IsData) continue;
                if (string.IsNullOrEmpty(track.BinFilename)) continue;
                if (!seenFiles.Add(track.BinFilename)) continue;
                var trackPath = Path.Combine(dir, track.BinFilename);
                if (File.Exists(trackPath)) dataTracks.Add(trackPath);
            }

            for (int i = 0; i < dataTracks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int trackIndex = i;
                int total = dataTracks.Count;
                var byteProgress = progress == null ? null : new Progress<int>(p =>
                    progress.Report($"Loading source... {(trackIndex * 100 + p) / total}%"));
                AccumulateLocationsFromFile(dataTracks[i], byteProgress);
            }
            return Task.CompletedTask;
        }

        public override async Task SaveAllAsync(
            IpBinMetadata metadata, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            await WriteMetadataAcrossSourceAsync(metadata, cancellationToken);
            progress?.Report(100);
        }
    }

    // ---- CDI --------------------------------------------------------------

    // No CDI footer/track parser. Just signature-scan the raw .cdi file.
    public sealed class CdiIpBinSource : IpBinFileSource
    {
        protected internal override Task ScanAsync(string path, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            SourcePath = path;
            Format = IpBinFileFormat.Cdi;
            var byteProgress = progress == null ? null : new Progress<int>(p => progress.Report($"Loading source... {p}%"));
            AccumulateLocationsFromFile(path, byteProgress);
            return Task.CompletedTask;
        }

        public override async Task SaveAllAsync(
            IpBinMetadata metadata, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            await WriteMetadataAcrossSourceAsync(metadata, cancellationToken);
            progress?.Report(100);
        }
    }

    // ---- CHD --------------------------------------------------------------

    public sealed class ChdIpBinSource : IpBinFileSource
    {
        private string? _tempDirectory;
        private IpBinFileSource? _inner;
        private string? _innerEntryPath;
        private bool _isGdRom;

        protected internal override async Task ScanAsync(string path, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            SourcePath = path;

            using (var chd = new ChdReader(path))
            {
                _isGdRom = chd.IsGdRom;
            }
            if (!_isGdRom)
                throw new InvalidDataException(
                    "This CHD is not a Dreamcast GD-ROM dump. Only GD-ROM CHDs are supported.");
            Format = IpBinFileFormat.ChdGdRom;

            _tempDirectory = CreateTempDirectory();

            var decompProgress = new Progress<int>(p => progress?.Report($"Loading source... {p}%"));

            var (ok, msg) = await ChdConverter.ConvertToGdi(path, _tempDirectory, decompProgress, cancellationToken);
            if (!ok) throw new InvalidDataException(msg);
            _innerEntryPath = Directory.GetFiles(_tempDirectory, "*.gdi").FirstOrDefault()
                ?? throw new InvalidDataException("CHD-to-GDI extraction produced no .gdi file.");
            _inner = new GdiIpBinSource();

            await _inner.ScanAsync(_innerEntryPath, progress, cancellationToken);
            MetaHeaderLocations.AddRange(_inner.MetaHeaderLocations);
            RegionStringLocations.AddRange(_inner.RegionStringLocations);
        }

        public override async Task SaveAllAsync(
            IpBinMetadata metadata, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_inner == null || _innerEntryPath == null || _tempDirectory == null)
                throw new InvalidOperationException("CHD source is not open.");

            await _inner.SaveAllAsync(metadata, null, cancellationToken);

            var sourceDir = Path.GetDirectoryName(SourcePath) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(SourcePath);
            var tempChd = Path.Combine(sourceDir, $".{stem}.ipbin-rebuild.chd");
            if (File.Exists(tempChd)) File.Delete(tempChd);

            var (ok, msg) = await ChdWriter.ConvertToChd(_innerEntryPath, tempChd, progress, cancellationToken);
            if (!ok)
            {
                if (File.Exists(tempChd)) { try { File.Delete(tempChd); } catch { } }
                throw new InvalidDataException(msg ?? "CHD recompression failed.");
            }

            try { File.Replace(tempChd, SourcePath, null); }
            catch (PlatformNotSupportedException)
            {
                // rename() is atomic on POSIX, no Delete+Move gap.
                File.Move(tempChd, SourcePath, overwrite: true);
            }
        }

        public override void Dispose()
        {
            _inner?.Dispose();
            if (_tempDirectory != null && Directory.Exists(_tempDirectory))
            {
                try { Directory.Delete(_tempDirectory, recursive: true); }
                catch { }
            }
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(),
                "udp-ipbin-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
