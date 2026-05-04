using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using UniversalDreamcastPatcher.Core;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class ManualUpdateDialog : Window
{
    public string LatestTag { get; private set; } = "";

    public ManualUpdateDialog()
    {
        InitializeComponent();
    }

    public ManualUpdateDialog(string latestTag, string latestVersion, ManualUpdateReason reason)
    {
        InitializeComponent();
        LatestTag = latestTag;

        if (reason == ManualUpdateReason.UnsupportedPlatform)
            ReasonText.Text = $"A new version of Universal Dreamcast Patcher ({latestVersion}) is available. Auto-update is not supported on this platform.";
        else
            ReasonText.Text = $"A new version of Universal Dreamcast Patcher ({latestVersion}) is available, but this release cannot be auto-updated.";

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SkipButton_Click(object? sender, RoutedEventArgs e)
    {
        UpdateAvailableDialog.SaveSkippedVersion(LatestTag);
        Close();
    }

    private void ReleasesLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PlatformUtil.OpenUrl(Constants.AppUrl + "/releases");
    }
}
