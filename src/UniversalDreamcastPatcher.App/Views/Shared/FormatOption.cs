using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Shared;

// A single dropdown row: user-facing label plus the matching enum value.
public sealed record FormatOption(string Label, OutputDiscImageFormat Format)
{
    public override string ToString() => Label;
}
