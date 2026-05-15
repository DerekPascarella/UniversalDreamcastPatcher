using System.Threading.Tasks;
using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

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
    {
        var owner = TopLevel.GetTopLevel(hostControl) as Window;
        var box = MessageBoxManager.GetMessageBoxStandard("Information", BodyText, ButtonEnum.Ok, Icon.None);
        if (owner != null)
            await box.ShowWindowDialogAsync(owner);
        else
            await box.ShowAsync();
    }
}
