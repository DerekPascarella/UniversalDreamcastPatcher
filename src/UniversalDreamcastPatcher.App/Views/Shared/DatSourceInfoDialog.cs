using System.Threading.Tasks;
using Avalonia.Controls;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Shared;

public static class DatSourceInfoDialog
{
    private const string BodyText =
        "Universal Dreamcast Patcher includes TOSEC and Redump DATs for byte-perfect " +
        "conversion between GDI and CUE/BIN of catalogued discs.\n\n" +
        "Select \"External\" to supply your own DAT files. External DATs are consulted " +
        "first. If a disc isn't found in any of them, the built-in DATs are used.";

    public static async Task ShowAsync(Control hostControl)
        => await DialogBox.ShowAsync(hostControl, "Information", BodyText);
}
