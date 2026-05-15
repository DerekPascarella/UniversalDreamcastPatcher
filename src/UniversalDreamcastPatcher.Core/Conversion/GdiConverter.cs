using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniversalDreamcastPatcher.Core.Patching;

namespace UniversalDreamcastPatcher.Core
{
    public static class GdiConverter
    {
        private const int SectorSize = 2352;
        private const int HighDensityAreaLba = 45000;

        public static async Task<(bool Success, string Message)> ConvertToGdi(
            string cuePath,
            string outputDirectory,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var cueData = ParseCueFile(cuePath);

                if (cueData.Tracks.Count == 0)
                {
                    return (false, "No tracks found in CUE file");
                }

                // GD-ROM CUEs carry a HIGH-DENSITY AREA REM marker.
                bool isGdRom = cueData.Tracks.Any(t => t.Comments.Contains("HIGH-DENSITY AREA"));
                if (!isGdRom)
                {
                    return (false, "This is not a GD-ROM CUE/BIN. Use redump2cdi for CD-ROM images.");
                }

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                int trackCount = cueData.Tracks.Count;
                var gdiContent = new StringBuilder();
                gdiContent.AppendLine(trackCount.ToString());

                int currentLba = 0;
                int processedTracks = 0;

                // TOSEC DAT fast-path: hash Track 1's .bin (data, no pregap on
                // SD area so identical bytes to TOSEC's track01.bin) and look
                // up the disc. When matched, each per-track CUE-INDEX strip is
                // replaced by byte-exact reconstruction against TOSEC's
                // expected hashes. Unrecognized discs fall through to the
                // CUE-INDEX strip path below.
                TosecDiscEntry? tosecEntry = null;
                var t1Track = cueData.Tracks.FirstOrDefault(t => t.TrackNumber == 1);
                if (t1Track != null)
                {
                    string t1BinPath = Path.Combine(cueData.Directory, t1Track.DataFilename);
                    if (File.Exists(t1BinPath))
                    {
                        uint t1Crc = Crc32.HashToUInt32(File.ReadAllBytes(t1BinPath));
                        tosecEntry = TosecDatLookup.LookupByT1Crc32(t1Crc);
                    }
                }
                bool tosecUsable = tosecEntry != null;

                foreach (var track in cueData.Tracks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string sourceBinPath = Path.Combine(cueData.Directory, track.DataFilename);
                    if (!File.Exists(sourceBinPath))
                    {
                        return (false, $"BIN file not found: {track.DataFilename}");
                    }

                    // trackNN.bin for data, trackNN.raw for audio.
                    string extension = track.IsAudio ? "raw" : "bin";
                    string outputFilename = $"track{track.TrackNumber:D2}.{extension}";
                    string outputPath = Path.Combine(outputDirectory, outputFilename);

                    int sectorCount;

                    // TOSEC byte-exact path. Reads the Redump-form .bin, looks
                    // up TOSEC's expected per-track hashes by track number,
                    // runs the same reconstruction engine as RedumpAssembler
                    // (verbatim, sector-aligned pregap, rolling-CRC scan).
                    // Falls through to the CUE-INDEX strip path if no DAT
                    // entry or any track reconstruction fails.
                    if (tosecUsable)
                    {
                        var tosecTrack = tosecEntry!.Tracks.FirstOrDefault(t => t.TrackNumber == track.TrackNumber);
                        if (tosecTrack != null)
                        {
                            byte[] redumpBytes = File.ReadAllBytes(sourceBinPath);
                            byte[]? reconstructed = RedumpReconstructor.TryReconstruct(
                                redumpBytes, !track.IsAudio, tosecTrack.Size, tosecTrack.Crc32, tosecTrack.Md5);
                            if (reconstructed != null)
                            {
                                await File.WriteAllBytesAsync(outputPath, reconstructed, cancellationToken);
                                sectorCount = (int)(tosecTrack.Size / SectorSize);

                                int tosecTrackType = track.IsAudio ? 0 : 4;
                                gdiContent.AppendLine($"{track.TrackNumber} {currentLba} {tosecTrackType} 2352 {outputFilename} 0");
                                currentLba += sectorCount;

                                if (track.Comments.Contains("HIGH-DENSITY AREA") && currentLba < HighDensityAreaLba)
                                {
                                    currentLba = HighDensityAreaLba;
                                }
                                processedTracks++;
                                progress?.Report((processedTracks * 100) / trackCount);
                                continue;
                            }
                        }
                    }

                    // Single-index track: copy the whole file.
                    // Multi-index track (INDEX 00 + INDEX 01): skip to INDEX 01 to drop the pregap.
                    bool hasOnlyOneIndex = track.Indices.Count == 1;

                    if (hasOnlyOneIndex)
                    {
                        await CopyFileAsync(sourceBinPath, outputPath, cancellationToken);
                        var fileInfo = new FileInfo(sourceBinPath);
                        sectorCount = (int)(fileInfo.Length / SectorSize);
                    }
                    else
                    {
                        // Skip past the pregap to INDEX 01.
                        var index01 = track.Indices.FirstOrDefault(i => i.Number == 1);
                        int framesToSkip = index01?.TotalFrames ?? 0;

                        sectorCount = await CopyFileWithOffsetAsync(sourceBinPath, outputPath, framesToSkip, cancellationToken);
                        currentLba += framesToSkip;
                    }

                    // GDI line: track# LBA type sectorSize filename offset (type: 0 = audio, 4 = data).
                    int trackType = track.IsAudio ? 0 : 4;
                    gdiContent.AppendLine($"{track.TrackNumber} {currentLba} {trackType} 2352 {outputFilename} 0");

                    // Advance LBA after writing the line so it points to the start of this track.
                    currentLba += sectorCount;

                    // HD area starts at LBA 45000. Pad the LBA up if it hasn't reached that yet.
                    if (track.Comments.Contains("HIGH-DENSITY AREA") && currentLba < HighDensityAreaLba)
                    {
                        currentLba = HighDensityAreaLba;
                    }

                    processedTracks++;
                    progress?.Report((processedTracks * 100) / trackCount);
                }

                string gdiPath = Path.Combine(outputDirectory, "disc.gdi");
                await File.WriteAllTextAsync(gdiPath, gdiContent.ToString(), cancellationToken);

                return (true, null);
            }
            catch (OperationCanceledException)
            {
                return (false, "Conversion was cancelled");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Parses the CUE with full index tracking so multi-index tracks
        // (audio with pregap) can be split correctly later.
        private static CueData ParseCueFile(string cuePath)
        {
            var cueData = new CueData
            {
                FilePath = cuePath,
                Directory = Path.GetDirectoryName(cuePath) ?? string.Empty
            };

            var lines = File.ReadAllLines(cuePath);
            string currentDataFile = string.Empty;
            GdiTrack currentTrack = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                var parts = SplitCueLine(line);
                if (parts.Length == 0)
                    continue;

                var command = parts[0].ToUpperInvariant();

                switch (command)
                {
                    case "FILE":
                        if (parts.Length >= 2)
                        {
                            currentDataFile = parts[1];
                        }
                        break;

                    case "TRACK":
                        if (parts.Length >= 3 && int.TryParse(parts[1], out int trackNum))
                        {
                            currentTrack = new GdiTrack
                            {
                                TrackNumber = trackNum,
                                DataType = parts[2],
                                DataFilename = currentDataFile
                            };
                            cueData.Tracks.Add(currentTrack);
                        }
                        break;

                    case "INDEX":
                        if (currentTrack != null && parts.Length >= 3 && int.TryParse(parts[1], out int indexNum))
                        {
                            var index = new TrackIndex
                            {
                                Number = indexNum,
                                TotalFrames = ParseMsfToFrames(parts[2])
                            };
                            currentTrack.Indices.Add(index);
                        }
                        break;

                    case "REM":
                        if (currentTrack != null && parts.Length >= 2)
                        {
                            // Join all parts after REM into a single comment string.
                            var comment = string.Join(" ", parts.Skip(1));
                            currentTrack.Comments.Add(comment);
                        }
                        break;
                }
            }

            return cueData;
        }

        private static string[] SplitCueLine(string line)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                parts.Add(current.ToString());

            return parts.ToArray();
        }

        // MSF format is MM:SS:FF where FF is frame count (75 frames per second).
        private static int ParseMsfToFrames(string msf)
        {
            var parts = msf.Split(':');
            if (parts.Length != 3)
                return 0;

            if (!int.TryParse(parts[0], out int minutes) ||
                !int.TryParse(parts[1], out int seconds) ||
                !int.TryParse(parts[2], out int frames))
                return 0;

            return (minutes * 60 * 75) + (seconds * 75) + frames;
        }

        private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
        {
            const int bufferSize = 81920; // 80KB buffer
            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

            await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);
        }

        // Skips the requested number of frames from the start of the source
        // (used to drop pregap data from multi-index tracks). Returns the
        // sector count actually written.
        private static async Task<int> CopyFileWithOffsetAsync(string source, string destination, int framesToSkip, CancellationToken cancellationToken)
        {
            const int bufferSize = 81920; // 80KB buffer
            using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

            // Seek to the offset (matching original: stream1.Position = frames * count)
            long bytesToSkip = (long)framesToSkip * SectorSize;
            sourceStream.Seek(bytesToSkip, SeekOrigin.Begin);

            // Calculate sectors to write (matching original: (stream1.Length - stream1.Position) / count)
            long bytesRemaining = sourceStream.Length - sourceStream.Position;
            int sectorsToWrite = (int)(bytesRemaining / SectorSize);

            await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);

            return sectorsToWrite;
        }

        public static bool IsGdRomCue(string cuePath)
        {
            try
            {
                var cueData = ParseCueFile(cuePath);
                return cueData.Tracks.Any(t => t.Comments.Contains("HIGH-DENSITY AREA"));
            }
            catch
            {
                return false;
            }
        }

        // CUE sheet model.
        private class CueData
        {
            public string FilePath { get; set; } = string.Empty;
            public string Directory { get; set; } = string.Empty;
            public List<GdiTrack> Tracks { get; } = new List<GdiTrack>();
        }

        private class GdiTrack
        {
            public int TrackNumber { get; set; }
            public string DataType { get; set; } = string.Empty;
            public string DataFilename { get; set; } = string.Empty;
            public List<TrackIndex> Indices { get; } = new List<TrackIndex>();
            public List<string> Comments { get; } = new List<string>();

            public bool IsAudio => DataType.Equals("AUDIO", StringComparison.OrdinalIgnoreCase);
        }

        private class TrackIndex
        {
            public int Number { get; set; }
            public int TotalFrames { get; set; }
        }
    }
}
