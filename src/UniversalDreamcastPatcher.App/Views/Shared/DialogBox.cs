using System.Threading.Tasks;
using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Shared;

// All standard message boxes go through here so they share one width and the
// same logic for showing over the owning window.
public static class DialogBox
{
    // Narrower than the MsBox 600 default.
    private const double MaxDialogWidth = 460;

    public static async Task<ButtonResult> ShowAsync(
        Control? host,
        string title,
        string message,
        ButtonEnum buttons = ButtonEnum.Ok,
        Icon icon = Icon.None)
    {
        var owner = TopLevel.GetTopLevel(host) as Window;

        var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ContentTitle = title,
            ContentMessage = message,
            ButtonDefinitions = buttons,
            Icon = icon,
            // Center over the main window when there is one, else on screen.
            WindowStartupLocation = owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            MaxWidth = MaxDialogWidth,
        });

        return owner != null
            ? await box.ShowWindowDialogAsync(owner)
            : await box.ShowAsync();
    }
}
