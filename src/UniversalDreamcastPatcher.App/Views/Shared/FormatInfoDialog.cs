using System.Threading.Tasks;
using Avalonia.Controls;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Shared;

// Format-compatibility blurb shown by the (i) button next to the output-format
// dropdown. Shared between Apply Patch and Converter so the wording matches.
public static class FormatInfoDialog
{
    private const string BodyText =
        "GDI is universally compatible with all emulators and ODEs.\n\n" +
        "CUE/BIN is compatible with most emulators, but only the MODE ODE.\n\n" +
        "CHD is a compressed format compatible with most emulators, but not ODEs.";

    public static async Task ShowAsync(Control hostControl)
        => await DialogBox.ShowAsync(hostControl, "Information", BodyText);
}
