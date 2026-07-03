using System;
using System.Collections.Generic;
using System.Text;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core;

// One track line of a .gdi file:
//   <track#> <lba> <type> <sector size> <filename> <offset>
// Filenames containing spaces are quoted (chdman-style GDIs do this), and some
// tools pad the numeric columns with extra whitespace, so parsing is done with
// a quote-aware tokenizer rather than a plain space split.
public readonly record struct GdiTrackLine(int TrackNumber, int Lba, int Type, int SectorSize, string FileName)
{
    public static bool TryParse(string line, out GdiTrackLine track)
    {
        track = default;

        var tokens = Tokenize(line);
        if (tokens.Count < 5) return false;
        if (!int.TryParse(tokens[0], out int trackNumber)) return false;
        if (!int.TryParse(tokens[1], out int lba)) return false;
        if (!int.TryParse(tokens[2], out int type)) return false;
        if (!int.TryParse(tokens[3], out int sectorSize)) return false;
        if (string.IsNullOrEmpty(tokens[4])) return false;

        track = new GdiTrackLine(trackNumber, lba, type, sectorSize, tokens[4]);
        return true;
    }

    // Splits on whitespace, keeping quoted segments (with their spaces) as a
    // single token and stripping the quotes.
    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0) tokens.Add(current.ToString());

        return tokens;
    }
}
