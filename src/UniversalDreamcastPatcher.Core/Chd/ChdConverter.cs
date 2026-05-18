using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core
{
    public static class ChdConverter
    {
        private const int SectorSize = 2352;
        private const int HighDensityAreaLba = 45000;
        private const int SectorsPerBatch = 256; // ~588KB per batch
        private const int TrackPadding = 4; // chdman aligns each track to 4-frame boundaries

        public static async Task<(bool Success, string Message)> ConvertToGdi(
            string chdPath,
            string outputDirectory,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var chd = new ChdReader(chdPath);

                if (!chd.IsGdRom)
                    return (false, "This CHD is not a GD-ROM image. Use ConvertToCueBin for CD-ROM CHDs.");

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                int trackCount = chd.Tracks.Count;
                var gdiContent = new StringBuilder();
                gdiContent.AppendLine(trackCount.ToString());

                int currentLba = 0;
                long chdSectorOffset = 0;
                int processedTracks = 0;
                bool swapAudio = chd.Header.Version >= 5;

                for (int t = 0; t < chd.Tracks.Count; t++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var track = chd.Tracks[t];

                    // Account for pregap in LBA but skip pregap data in output
                    currentLba += track.Pregap;
                    chdSectorOffset += track.Pregap;

                    // Output filename: trackNN.bin for data, trackNN.raw for audio
                    string extension = track.IsAudio ? "raw" : "bin";
                    string outputFilename = $"track{track.TrackNumber:D2}.{extension}";
                    string outputPath = Path.Combine(outputDirectory, outputFilename);

                    // GDI line: track# LBA type 2352 filename 0
                    int trackType = track.IsAudio ? 0 : 4;
                    gdiContent.AppendLine($"{track.TrackNumber} {currentLba} {trackType} {SectorSize} {outputFilename} 0");

                    // Extract track data frames from CHD.
                    // FRAMES in CHD metadata includes PAD, so subtract PAD to get actual content.
                    int dataFrames = track.Frames - track.Pad;
                    await Task.Run(() => ExtractTrackData(chd, chdSectorOffset, dataFrames, outputPath,
                        swapAudio && track.IsAudio, cancellationToken), cancellationToken);

                    // Advance LBA by the full track span (FRAMES includes PAD, which
                    // fills the gap to the next track on the disc layout).
                    // Advance CHD offset by FRAMES + alignment to 4-frame boundary.
                    currentLba += track.Frames;
                    chdSectorOffset += track.Frames + GetExtraFrames(track.Frames);

                    // Insert high-density area gap after last low-density track
                    if (track.TrackNumber < 3)
                    {
                        bool nextIsHd = (t + 1 < chd.Tracks.Count && chd.Tracks[t + 1].TrackNumber >= 3);
                        if (nextIsHd && currentLba < HighDensityAreaLba)
                            currentLba = HighDensityAreaLba;
                    }

                    processedTracks++;
                    progress?.Report((processedTracks * 100) / trackCount);
                }

                // Write disc.gdi manifest
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

        public static async Task<(bool Success, string Message, string CuePath)> ConvertToCueBin(
            string chdPath,
            string outputDirectory,
            IProgress<int> progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var chd = new ChdReader(chdPath);

                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                int trackCount = chd.Tracks.Count;
                var cueContent = new StringBuilder();
                long chdSectorOffset = 0;
                int processedTracks = 0;
                bool swapAudio = chd.Header.Version >= 5;

                for (int t = 0; t < chd.Tracks.Count; t++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var track = chd.Tracks[t];

                    string binFilename = $"Track {track.TrackNumber:D2}.bin";
                    string binPath = Path.Combine(outputDirectory, binFilename);

                    // Map CHD track type to CUE track type
                    string cueTrackType = track.IsAudio ? "AUDIO" : "MODE1/2352";

                    cueContent.AppendLine($"FILE \"{binFilename}\" BINARY");
                    cueContent.AppendLine($"  TRACK {track.TrackNumber:D2} {cueTrackType}");

                    // Skip pregap frames in CHD, write only data frames to BIN
                    chdSectorOffset += track.Pregap;

                    if (track.Pregap > 0 && track.TrackNumber > 1)
                    {
                        // Add PREGAP directive for non-first tracks
                        cueContent.AppendLine($"    PREGAP {FramesToMsf(track.Pregap)}");
                    }

                    cueContent.AppendLine($"    INDEX 01 00:00:00");

                    // Extract track data frames from CHD.
                    // FRAMES in CHD metadata includes PAD, so subtract PAD to get actual content.
                    int dataFrames = track.Frames - track.Pad;
                    await Task.Run(() => ExtractTrackData(chd, chdSectorOffset, dataFrames, binPath,
                        swapAudio && track.IsAudio, cancellationToken), cancellationToken);

                    // Advance past data frames + alignment padding in CHD stream.
                    // chdman rounds FRAMES (which includes PAD) to a 4-frame boundary.
                    chdSectorOffset += track.Frames + GetExtraFrames(track.Frames);

                    processedTracks++;
                    progress?.Report((processedTracks * 100) / trackCount);
                }

                // Write CUE sheet
                string baseName = Path.GetFileNameWithoutExtension(chdPath);
                string cuePath = Path.Combine(outputDirectory, baseName + ".cue");
                await File.WriteAllTextAsync(cuePath, cueContent.ToString(), cancellationToken);

                return (true, null, cuePath);
            }
            catch (OperationCanceledException)
            {
                return (false, "Conversion was cancelled", null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static bool IsGdRomChd(string chdPath)
        {
            try
            {
                using var chd = new ChdReader(chdPath);
                return chd.IsGdRom;
            }
            catch
            {
                return false;
            }
        }

        // Streams the track data out in fixed-size batches so a multi-GB CHD
        // doesn't have to be held in memory all at once.
        private static void ExtractTrackData(
            ChdReader chd,
            long startSector,
            int frameCount,
            string outputPath,
            bool swapEndianness,
            CancellationToken cancellationToken)
        {
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
                81920, FileOptions.SequentialScan);

            int remaining = frameCount;
            long currentSector = startSector;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int batchSize = Math.Min(remaining, SectorsPerBatch);
                byte[] data = chd.ReadSectors(currentSector, batchSize);

                if (swapEndianness)
                    SwapAudioEndianness(data);

                fs.Write(data, 0, data.Length);

                currentSector += batchSize;
                remaining -= batchSize;
            }
        }

        // CHD v5+ stores audio big-endian, but BIN/RAW files expect little-endian,
        // so each 16-bit sample needs its bytes flipped.
        private static void SwapAudioEndianness(byte[] data)
        {
            for (int i = 0; i < data.Length - 1; i += 2)
            {
                byte tmp = data[i];
                data[i] = data[i + 1];
                data[i + 1] = tmp;
            }
        }

        // chdman pads each track up to a 4-frame boundary. The padding
        // frames live in the data stream and must be stepped over.
        private static int GetExtraFrames(int totalFrames)
        {
            return ((totalFrames + TrackPadding - 1) / TrackPadding) * TrackPadding - totalFrames;
        }

        // CUE MSF format is MM:SS:FF at 75 frames per second.
        private static string FramesToMsf(int frames)
        {
            int minutes = frames / (75 * 60);
            int seconds = (frames / 75) % 60;
            int ff = frames % 75;
            return $"{minutes:D2}:{seconds:D2}:{ff:D2}";
        }
    }
}
