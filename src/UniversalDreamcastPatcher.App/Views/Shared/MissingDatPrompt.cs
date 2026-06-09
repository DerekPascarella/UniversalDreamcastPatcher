using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.App.Views.Converter;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Shared;

// Pre-conversion guard. When the user has DAT source set to "External" and one
// or more entries in the external pool point to a path that no longer exists,
// this asks them to confirm whether to proceed or abort and edit the list.
public static class MissingDatPrompt
{
    // Returns true to proceed with the operation, false to abort. If aborted,
    // the Manage External DATs window is opened so the user can fix the list.
    public static async Task<bool> ConfirmProceedAsync(Control hostControl)
    {
        if (!ExternalDatRegistry.IsEnabled) return true;

        var missing = ExternalDatRegistry.Files.Where(f => f.IsMissing).ToList();
        if (missing.Count == 0) return true;

        string body =
            "One or more external DAT files are missing from disk:\n\n" +
            string.Join("\n", missing.Select(f => f.FilePath)) +
            "\n\nThese DATs will be skipped during conversion. Continue anyway?";

        var owner = TopLevel.GetTopLevel(hostControl) as Window;
        var result = await DialogBox.ShowAsync(hostControl, "Confirmation", body, ButtonEnum.YesNo);

        if (result == ButtonResult.Yes) return true;

        var manage = new ManageExternalDatsWindow();
        if (owner != null) await manage.ShowDialog(owner);
        else manage.Show();
        return false;
    }
}
