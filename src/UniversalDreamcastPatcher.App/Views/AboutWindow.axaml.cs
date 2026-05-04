using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.Core;
using MsBoxIcon = MsBox.Avalonia.Enums.Icon;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        VersionRun.Text = $" v{Constants.Version}";
        KeyDown += AboutWindow_KeyDown;
    }

    private void AboutWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void LinkText_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PlatformUtil.OpenUrl(Constants.AppUrl);
    }

    private async void CheckForUpdatesButton_Click(object? sender, RoutedEventArgs e)
    {
        var btn = (Button)sender!;
        btn.IsEnabled = false;
        btn.Content = "Checking...";

        var parentWindow = this.Owner as Window;

        UpdateCheckResult result;
        try
        {
            result = await UpdateManager.CheckForUpdateAsync();
        }
        catch
        {
            // Keep About open on network failure so the user can retry.
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                "Update Check Failed",
                "Could not check for updates. Please check your internet connection.",
                ButtonEnum.Ok, MsBoxIcon.None);
            await msgBox.ShowWindowDialogAsync(this);
            btn.IsEnabled = true;
            btn.Content = "Check for Updates";
            return;
        }

        // Close About first. The next dialog is parented to the main window.
        Close();
        if (parentWindow == null) return;

        if (result.ManualUpdateRequired)
        {
            var manualDialog = new ManualUpdateDialog(result.LatestTag, result.LatestVersion, result.ManualReason);
            await manualDialog.ShowDialog(parentWindow);
        }
        else if (result.UpdateAvailable)
        {
            var dialog = new UpdateAvailableDialog(result.LatestTag, result.LatestVersion);
            await dialog.ShowDialog(parentWindow);

            if (dialog.UserWantsUpdate)
            {
                var wizard = new UpdateWizardWindow(result.LatestTag, result.LatestVersion);
                await wizard.ShowDialog(parentWindow);
            }
        }
        else
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                "No Update Available",
                "You are running the latest version.",
                ButtonEnum.Ok, MsBoxIcon.None);
            await msgBox.ShowWindowDialogAsync(parentWindow);
        }
    }

}
