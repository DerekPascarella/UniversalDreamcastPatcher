using System;
using System.IO;
using DiscUtils.Iso9660;
using File = System.IO.File;
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Gdrom
{
    /// <summary>
    /// Allow the reading of a GD-ROM dump by adapting the 3552 byte sectors to 2048 byte sectors
    /// that the CDReader class expects.
    /// </summary>
    public class GDReader : CDReader
    {
        private readonly List<GDDataTrack> _data;
        public GDReader(List<GDDataTrack> data, bool joliet = false) : base(new StreamSectorAdapter(data), joliet, true, data[0].LBA)
        {
            _data = data;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var track in _data)
            {
                track.Data.Dispose();
            }
        }

        /// <summary>
        /// Reads the high density area using the track information from a GDI file.
        /// If you want to read the low density area instead, just create a GDReader with track01.bin as the only track in the track list.
        /// </summary>
        /// <param name="gdiPath">The path to the GDI</param>
        /// <returns>A GDReader for the high density files on the disc</returns>
        public static GDReader FromGDIfile(string gdiPath)
        {
            if (!File.Exists(gdiPath))
            {
                throw new FileNotFoundException("The input GDI was not found or accessible.", gdiPath);
            }
            string[] gdiLines = File.ReadAllText(gdiPath).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return FromGDItext(gdiLines, Path.GetDirectoryName(gdiPath));
        }

        /// <summary>
        /// Reads the high density area using the track information from a CUE sheet.
        /// </summary>
        /// <param name="cuePath">The path to the CUE sheet</param>
        /// <returns>A GDReader for the high density files on the disc</returns>
        public static GDReader FromCueSheet(string cuePath)
        {
            if (!File.Exists(cuePath))
            {
                throw new FileNotFoundException("The input CUE sheet was not found or accessible.", cuePath);
            }
            string[] cueLines = File.ReadAllText(cuePath).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return FromCueText(cueLines, Path.GetDirectoryName(cuePath));
        }

        /// <summary>
        /// Reads the high density area using the track information from a GDI file whose lines you already have in memory.
        /// </summary>
        /// <param name="gdiLines">The lines of text from the GDI file</param>
        /// <param name="sourceDirectory">The path of the directory that this gdi references</param>
        /// <returns>A GDReader for the high density files on the disc</returns>
        /// <exception cref="FileNotFoundException">If the GDI references tracks that do not exist in the source directory</exception>
        public static GDReader FromGDItext(string[] gdiLines, string sourceDirectory)
        {
            if (gdiLines.Length > 3 && int.TryParse(gdiLines[0], out int numTracks) && numTracks >= 3)
            {
                //This code assumes the GDI track list is sorted sequentially and there are only 1 or 2 high density data tracks.
                var track3 = ParseTrackInfo(gdiLines[3], sourceDirectory);
                if (track3 != null)
                {
                    if (!File.Exists(track3.Value.filename))
                    {
                        throw new FileNotFoundException("The GDI references a track file that does not exist!", track3.Value.filename);
                    }
                    List<GDDataTrack> tracks = new List<GDDataTrack>();
                    tracks.Add(new GDDataTrack(new FileStream(track3.Value.filename, FileMode.Open, FileAccess.Read), track3.Value.lba, track3.Value.sectorSize));
                    if (numTracks > 3)
                    {
                        var finalTrack = ParseTrackInfo(gdiLines[numTracks], sourceDirectory);
                        if (!File.Exists(track3.Value.filename))
                        {
                            throw new FileNotFoundException("The GDI references a track file that does not exist!", track3.Value.filename);
                        }
                        if (finalTrack != null)
                        {
                            tracks.Add(new GDDataTrack(new FileStream(finalTrack.Value.filename, FileMode.Open, FileAccess.Read), finalTrack.Value.lba,
                                finalTrack.Value.sectorSize));
                        }
                    }
                    return new GDReader(tracks);
                }
            }
            return null;
        }

        private static (string filename, uint lba, uint sectorSize)? ParseTrackInfo(string trackInfo, string sourceDir)
        {
            string[] pieces = trackInfo.Split(' ');
            if (pieces.Length > 6 || trackInfo.IndexOf('"') > 0)
            {
                pieces = ManuallySplitInfo(trackInfo);
            }
            if (pieces.Length == 6 && uint.TryParse(pieces[1], out uint lba) && uint.TryParse(pieces[3], out uint sectorSize))
            {
                return (filename: Path.Combine(sourceDir, pieces[4]), lba, sectorSize);
            }
            return null;
        }

        private static string[] ManuallySplitInfo(string trackInfo)
        {
            List<string> pieces = new List<string>();
            StringBuilder current = new StringBuilder();
            for (int i = 0; i < trackInfo.Length; i++)
            {
                if (char.IsWhiteSpace(trackInfo[i]))
                {
                    if (current.Length > 0)
                    {
                        pieces.Add(current.ToString());
                        current.Clear();
                    }
                }
                else if (trackInfo[i] == '"' && trackInfo.IndexOf('"', i + 1) > 0)
                {
                    int end = trackInfo.IndexOf('"', i + 1);
                    current.Append(trackInfo.Substring(i + 1, end - i - 1));
                    i = end;
                }
                else
                {
                    current.Append(trackInfo[i]);
                }
            }
            if (current.Length > 0)
            {
                pieces.Add(current.ToString());
            }
            return pieces.ToArray();
        }

        /// <summary>
        /// Reads the high density area using the track information from a CUE sheet whose lines you already have in memory.
        /// </summary>
        /// <param name="cueLines">The lines of text from the cue sheet</param>
        /// <param name="sourceDirectory">The path of the directory that this cue sheet references</param>
        /// <returns>A GDReader for the high density files on the disc</returns>
        /// <exception cref="FileNotFoundException">If the cue sheet references tracks that do not exist in the source directory</exception>
        public static GDReader FromCueText(string[] cueLines, string sourceDirectory)
        {
            int highDensityStart = -1;
            for (int i = 0; i < cueLines.Length; i++)
            {
                if (cueLines[i].StartsWith("REM", StringComparison.Ordinal) && cueLines[i].IndexOf("HIGH-DENSITY", StringComparison.Ordinal) > 0)
                {
                    highDensityStart = i + 1; break;
                }
            }
            if (highDensityStart >= 0)
            {
                List<GDDataTrack> tracks = new List<GDDataTrack>();
                string currentFile = null;
                uint currentLba = 45000;
                for (int i = highDensityStart; i < cueLines.Length; i++)
                {
                    string[] entry = ManuallySplitInfo(cueLines[i]);
                    if (entry.Length == 3)
                    {
                        if (entry[0].Equals("FILE", StringComparison.Ordinal) && entry[2].Equals("BINARY", StringComparison.Ordinal))
                        {
                            currentFile = Path.Combine(sourceDirectory, entry[1]);
                        }
                        else if (entry[0].Equals("TRACK", StringComparison.Ordinal) && currentFile != null)
                        {
                            if (entry[2].IndexOf('/') > 0)
                            {
                                //Data track
                                string sectorSizeText = entry[2].Substring(entry[2].IndexOf('/') + 1);
                                bool mode2 = entry[2].Contains("MODE2");
                                if (uint.TryParse(sectorSizeText, out uint sectorSize))
                                {
                                    var stream = new FileStream(currentFile, FileMode.Open, FileAccess.Read);
                                    tracks.Add(new GDDataTrack(stream, currentLba, sectorSize, (uint)(mode2 ? 0x18 : 0x10)));
                                    currentLba += (uint)(stream.Length / sectorSize);
                                }
                            }
                            else if (entry[2].Equals("AUDIO", StringComparison.Ordinal))
                            {
                                //Audio track
                                FileInfo fileInfo = new FileInfo(currentFile);
                                if (fileInfo.Exists)
                                {
                                    currentLba += (uint)(fileInfo.Length / 2352);
                                }
                            }
                        }
                    }
                }
                if (tracks.Count > 0)
                {
                    return new GDReader(tracks);
                }
            }
            return null;
        }

        public override DateTime GetCreationTimeUtc(string path)
        {
            return CheckFixDateBug(base.GetCreationTimeUtc(path));
        }
        public override DateTime GetLastAccessTimeUtc(string path)
        {
            return CheckFixDateBug(base.GetLastAccessTimeUtc(path));
        }
        public override DateTime GetLastWriteTimeUtc(string path)
        {
            return CheckFixDateBug(base.GetLastWriteTimeUtc(path));
        }
        private int? _discCreationYear;
        private bool _checkedYear;
        /// <summary>
        /// Some official GD-ROM discs were built with a tool that stored the year incorrectly in DirectoryInfo entries.
        /// The year on file entries is an 8-bit number representing the number of years since 1900.
        /// However, these incorrect discs store the year as the low byte of the full year.
        /// For example the year 2001, which is 0x07D1 in Hex, would be stored as 0xD1. 
        /// This would be read back as the year 2109. Since the disc mastering date is stored as a full year, 
        /// we can check this date to see if there's a problem. If the disc was written over 50 years before a file on it,
        /// assume the year is stored incorrectly and treat it as if it were the 2nd byte of the full year.
        /// Additionally, for some discs the year is actually the number of years since 2000, so we need to handle that too.
        /// </summary>
        /// <param name="input">A file timestamp from the disc</param>
        /// <returns>The original date if it is correct, or a fixed date if it is far newer than the disc itself.</returns>
        private DateTime CheckFixDateBug(DateTime input)
        {
            if (_discCreationYear == null && _checkedYear == false)
            {
                _checkedYear = true; //Fetching root date would call this recursively otherwise.
                _discCreationYear = CreationDateAndTime.Year;
            }
            if (input.Year > _discCreationYear + 50)
            {
                return new DateTime(0x700 + ((input.Year - 1900) & 0xFF), input.Month, input.Day, input.Hour, input.Minute, input.Second, input.Millisecond, input.Kind);
            }
            if (input.Year < 1950)
            {
                return new DateTime(2000 + ((input.Year - 1900) & 0xFF), input.Month, input.Day, input.Hour, input.Minute, input.Second, input.Millisecond, input.Kind);
            }
            return input;
        }
    }

    public class GDDataTrack
    {
        internal const int ISO_SECTOR_SIZE = 2048;
        public GDDataTrack(Stream data, uint lba, uint? sectorSize = null, uint? sectorOffset = null)
        {
            Data = data;
            LBA = lba;
            if (sectorSize.HasValue)
            {
                SectorSize = sectorSize.Value;
            }
            else if (data.Length % 2048 == 0 && data.Length % 2352 > 0)
            {
                //Assume 3552 unless the input is a direct multiple of 2048 and not 3552.
                //If neither are true, the logic that tries to read the first sector will fail anyway.
                SectorSize = 2048;
            }
            else
            {
                SectorSize = 2352;
            }
            if (sectorOffset.HasValue)
            {
                SectorOffset = sectorOffset.Value;
            }
            else if (SectorSize == 2352)
            {
                SectorOffset = 0x10;
            }
            else
            {
                SectorOffset = 0;
            }
            if (SectorSize < ISO_SECTOR_SIZE)
            {
                throw new InvalidDataException("What are you doing? An ISO data sector can't be under 2048 bytes.");
            }
            DataLength = data.Length * ISO_SECTOR_SIZE / SectorSize;
        }
        public Stream Data { get; set; }
        public uint LBA { get; set; }
        public long DataStart => LBA * ISO_SECTOR_SIZE;
        public long DataLength { get; set; }
        public long DataEnd => DataStart + DataLength;
        public uint SectorSize { get; set; }
        public uint SectorOffset { get; set; }

        public long Position
        {
            get => DataStart + (((Data.Position - SectorOffset) / SectorSize) * ISO_SECTOR_SIZE) + ((Data.Position - SectorOffset) % SectorSize);
            set => Data.Position = ((value - DataStart) / ISO_SECTOR_SIZE) * SectorSize + ((value - DataStart) % ISO_SECTOR_SIZE) + SectorOffset;
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                offset -= DataStart;
            }
            else if (origin == SeekOrigin.End)
            {
                offset += DataStart;
            }
            long actualOffset = ((offset / ISO_SECTOR_SIZE) * SectorSize) + (offset % ISO_SECTOR_SIZE) + SectorOffset;
            Data.Seek(actualOffset, origin);
            return Position;
        }
    }

    internal class StreamSectorAdapter : Stream
    {
        private readonly List<GDDataTrack> _input;
        private GDDataTrack _currentTrack;
        public StreamSectorAdapter(List<GDDataTrack> input)
        {
            _input = input;
            if (input.Count == 0)
            {
                throw new ArgumentException("You need to provide tracks in order to read them!");
            }
            _currentTrack = _input[0];
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (GDDataTrack track in _input)
            {
                track.Data.Dispose();
            }
        }

        public override void Close()
        {
            foreach (GDDataTrack track in _input)
            {
                track.Data.Close();
            }
        }

        public override void Flush()
        {
            _currentTrack.Data.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int remaining = count;
            int totalBytesRead = 0;

            while (remaining > 0)
            {
                int sectorOffset = _currentTrack != null ? (int)((_currentTrack.Data.Position - _currentTrack.SectorOffset) % _currentTrack.SectorSize) : 0;
                int bytesToRead = Math.Min(GDDataTrack.ISO_SECTOR_SIZE - sectorOffset, remaining);
                int bytesRead = bytesToRead;
                if (_currentTrack != null)
                {
                    bytesRead = _currentTrack.Data.Read(buffer, offset, bytesToRead);
                }
                else
                {
                    Array.Clear(buffer, offset, remaining);
                }
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
                offset += bytesRead;
                remaining -= bytesRead;
                if (_currentTrack?.SectorSize > GDDataTrack.ISO_SECTOR_SIZE)
                {
                    _currentTrack.Data.Seek(_currentTrack.SectorSize - GDDataTrack.ISO_SECTOR_SIZE, SeekOrigin.Current);
                }
                if (_currentTrack != null && _currentTrack.Position == _currentTrack.DataEnd)
                {
                    AdjustTracking(_currentTrack.Position);
                }
                else if (_currentTrack == null)
                {
                    Position += bytesRead;
                }
            }

            return totalBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long nextPosition = Position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    nextPosition = offset;
                    break;
                case SeekOrigin.Current:
                    nextPosition += offset;
                    break;
                case SeekOrigin.End:
                    nextPosition = _input[_input.Count - 1].DataEnd - offset;
                    break;
            }
            if (nextPosition >= _currentTrack?.DataStart && nextPosition < _currentTrack.DataEnd)
            {
                return _currentTrack.Seek(offset, origin);
            }
            AdjustTracking(nextPosition);
            _currentTrack?.Seek(nextPosition, SeekOrigin.Begin);
            return Position;
        }

        private void AdjustTracking(long position)
        {
            // Bug: the original predicate was
            //   if (position < _currentTrack?.DataStart || position >= _currentTrack?.DataEnd)
            // which silently evaluates to false whenever _currentTrack is null
            // (`position < null` returns false in C#). That meant once we stepped
            // off the end of one data track into the inter-track gap, _currentTrack
            // would stay null forever and every subsequent read on a multi-data-track
            // GDI would return zeros, even after a valid Position assignment.
            // We now always search the track list when the position falls outside
            // the current track (or no track is currently selected).
            if (_currentTrack != null
                && position >= _currentTrack.DataStart
                && position < _currentTrack.DataEnd)
            {
                return;
            }

            foreach (GDDataTrack track in _input)
            {
                if (position >= track.DataStart && position < track.DataEnd)
                {
                    _currentTrack = track;
                    return;
                }
            }
            _currentTrack = null;
            _unreachablePosition = position;
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("This stream is read-only. You cannot change the length!");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("This stream is read-only. You cannot write to it!");
        }

        public override bool CanRead => _currentTrack.Data.CanRead;
        public override bool CanSeek => _currentTrack.Data.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _input[_input.Count - 1].DataEnd;

        private long _unreachablePosition;
        public override long Position
        {
            get => _currentTrack?.Position ?? _unreachablePosition;
            set
            {
                AdjustTracking(value);
                if (value >= _currentTrack?.DataStart && value < _currentTrack?.DataEnd)
                {
                    _currentTrack.Position = value;
                }
                else
                {
                    _unreachablePosition = value;
                }
            }
        }
    }
}