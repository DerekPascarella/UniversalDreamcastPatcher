using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using UniversalDreamcastPatcher.Core;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class UpdateAvailableDialog : Window
{
    public string LatestTag { get; private set; } = "";
    public string LatestVersion { get; private set; } = "";
    public bool UserWantsUpdate { get; private set; }

    public UpdateAvailableDialog()
    {
        InitializeComponent();
    }

    public UpdateAvailableDialog(string latestTag, string latestVersion)
    {
        InitializeComponent();
        LatestTag = latestTag;
        LatestVersion = latestVersion;
        VersionText.Text = latestVersion;

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void UpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        UserWantsUpdate = true;
        Close();
    }

    private void RemindButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SkipButton_Click(object? sender, RoutedEventArgs e)
    {
        SaveSkippedVersion(LatestTag);
        Close();
    }

    private void ChangelogLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PlatformUtil.OpenUrl(Constants.ChangelogUrl);
    }

    internal static bool ShouldSkipVersion(string latestTag)
    {
        try
        {
            var settings = AppSettings.Load();
            return !string.IsNullOrWhiteSpace(settings.SkippedUpdateVersion) &&
                   settings.SkippedUpdateVersion == latestTag;
        }
        catch
        {
            return false;
        }
    }

    internal static void SaveSkippedVersion(string tag)
    {
        try
        {
            var settings = AppSettings.Load();
            settings.SkippedUpdateVersion = tag;
            settings.Save();
        }
        catch { }
    }

}
