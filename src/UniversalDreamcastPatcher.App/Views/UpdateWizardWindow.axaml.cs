using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.Core;
using MsBoxIcon = MsBox.Avalonia.Enums.Icon;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class UpdateWizardWindow : Window
{
    private readonly string _tag;
    private CancellationTokenSource? _cts;
    private bool _downloadComplete;
    private bool _installing;
    private bool _lockHeld;

    public UpdateWizardWindow()
    {
        InitializeComponent();
        _tag = "";
    }

    public UpdateWizardWindow(string tag, string version)
    {
        InitializeComponent();
        _tag = tag;
        StatusText.Text = $"Downloading update {version}...";

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape && !_installing)
                CancelAndClose();
        };

        Opened += (s, e) => StartDownload();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_installing)
        {
            e.Cancel = true;
            return;
        }

        _cts?.Cancel();
        if (!_downloadComplete)
            UpdateManager.CleanupStagingDirectory();
        if (_lockHeld)
            UpdateManager.EndUpdate();

        base.OnClosing(e);
    }

    private void CancelDownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_installing) return;
        CancelAndClose();
    }

    private void CancelAndClose()
    {
        _cts?.Cancel();
        UpdateManager.CleanupStagingDirectory();
        Close();
    }

    private async void StartDownload()
    {
        if (!UpdateManager.TryBeginUpdate())
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                "Information",
                "Another update is already in progress.\n\nPlease wait for it to finish before starting a new one.",
                ButtonEnum.Ok, MsBoxIcon.None);
            await msgBox.ShowWindowDialogAsync(this);
            Close();
            return;
        }
        _lockHeld = true;

        _cts = new CancellationTokenSource();
        var progress = new Progress<DownloadProgress>(p =>
        {
            if (p.TotalBytes > 0)
            {
                var pct = (double)p.BytesRead / p.TotalBytes * 100;
                DownloadProgress.Value = pct;
                SizeText.Text = $"{FormatBytes(p.BytesRead)} / {FormatBytes(p.TotalBytes)}";
            }
            else
            {
                DownloadProgress.IsIndeterminate = true;
                SizeText.Text = $"{FormatBytes(p.BytesRead)} downloaded";
            }
            SpeedText.Text = $"Download speed: {FormatSpeed(p.SpeedBytesPerSecond)}";
        });

        try
        {
            await UpdateManager.DownloadUpdateAsync(_tag, progress, _cts.Token);

            StatusText.Text = "Extracting update...";
            DownloadProgress.IsIndeterminate = true;
            SpeedText.Text = "";
            SizeText.Text = "";
            await UpdateManager.ExtractUpdateAsync(_tag, _cts.Token);

            StatusText.Text = "Preparing update...";
            await UpdateManager.PrepareUpdateAsync();

            _downloadComplete = true;
            StatusText.Text = "Update ready to install.\n\nThe application will close and relaunch automatically.";
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = 100;
            SpeedText.Text = "";
            SizeText.Text = "";
            CancelDownloadButton.Content = "Cancel";
            InstallButton.IsVisible = true;
        }
        catch (OperationCanceledException)
        {
            // user cancelled
        }
        catch (Exception ex)
        {
            UpdateManager.CleanupStagingDirectory();
            var msgBox = MessageBoxManager.GetMessageBoxStandard("Error",
                FriendlyError(ex), ButtonEnum.Ok, MsBoxIcon.None);
            await msgBox.ShowWindowDialogAsync(this);
            Close();
        }
    }

    private void InstallButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_installing) return;
        _installing = true;
        InstallButton.IsEnabled = false;
        CancelDownloadButton.IsEnabled = false;

        // LaunchUpdaterAndExit calls Environment.Exit, which skips Closing events.
        // Save the window position now so it isn't lost.
        SaveParentWindowPosition();

        UpdateManager.LaunchUpdaterAndExit();
    }

    private void SaveParentWindowPosition()
    {
        try
        {
            if (this.Owner is Window parent)
            {
                var settings = AppSettings.Load();
                settings.WindowLeft = parent.Position.X;
                settings.WindowTop = parent.Position.Y;
                settings.Save();
            }
        }
        catch { }
    }

    private static string FriendlyError(Exception ex) => ex switch
    {
        // TimeoutException and UpdateException already contain user-facing text.
        TimeoutException => ex.Message,
        UpdateException => ex.Message,

        HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.NotFound =>
            "The update file could not be downloaded.\n\n" +
            "It may have been removed from GitHub. Please try again later, or download the latest version manually from the project's website.",

        HttpRequestException =>
            "The update could not be downloaded.\n\n" +
            "Please check your internet connection and try again.\n\n" +
            $"Details: {ex.Message}",

        InvalidDataException =>
            "The downloaded update file appears to be damaged.\n\n" +
            "Please try again. If the problem persists, the release may need to be re-uploaded.\n\n" +
            $"Details: {ex.Message}",

        UnauthorizedAccessException =>
            "The update could not be installed.\n\n" +
            "The application does not have permission to write to its own folder. Please make sure no other program is using the folder, or try running as administrator.\n\n" +
            $"Details: {ex.Message}",

        IOException =>
            "The update could not be installed.\n\n" +
            "A file error occurred while writing the new version. Please make sure you have enough free disk space and that no antivirus or other software is locking the application's folder.\n\n" +
            $"Details: {ex.Message}",

        _ =>
            "An unexpected error occurred while updating.\n\n" +
            $"Details: {ex.Message}",
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.#} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.##} GB";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond:0} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:0.#} KB/s";
        return $"{bytesPerSecond / 1024.0 / 1024.0:0.#} MB/s";
    }
}
