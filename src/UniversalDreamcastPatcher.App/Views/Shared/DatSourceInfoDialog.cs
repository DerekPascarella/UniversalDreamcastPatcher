using System.Threading.Tasks;
using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Shared;

public static class DatSourceInfoDialog
{
    private const string BodyText =
        "Universal Dreamcast Patcher includes TOSEC and Redump DATs for\n" +
        "byte-perfect conversion between GDI and CUE/BIN of catalogued discs.\n\n" +
        "Select \"External\" to supply your own DAT files. External DATs\n" +
        "are consulted first. If a disc isn't found in any of them, the\n" +
        "built-in DATs are used.";

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
