using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.App.Views;
using UniversalDreamcastPatcher.Core;
using MsBoxIcon = MsBox.Avalonia.Enums.Icon;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App;

public partial class MainWindow : Window
{
    private Image? _headerLogo;
    private Bitmap? _logoOff;
    private Bitmap? _logoOn;
    private CancellationTokenSource? _flashCts;
    private TextBlock? _versionLine;
    private TabControl? _mainTabs;
    private TabItem? _applyTab;
    private TabItem? _buildTab;
    private AppSettings _settings = new();
    private bool _forceClose;
    private bool _confirmInFlight;

    public MainWindow()
    {
        InitializeComponent();
        _headerLogo = this.FindControl<Image>("HeaderLogo");
        _versionLine = this.FindControl<TextBlock>("VersionLine");
        _mainTabs = this.FindControl<TabControl>("MainTabs");
        _applyTab = this.FindControl<TabItem>("ApplyTab");
        _buildTab = this.FindControl<TabItem>("BuildTab");

        if (_versionLine != null)
            _versionLine.Text = $"v{Constants.Version} - Derek Pascarella (ateam)";

        LoadLogos();

        UpdateManager.CleanupStaleStagingData();
        _settings = AppSettings.Load();
        ApplySavedWindowPosition();

        Closing += MainWindow_Closing;

        _ = CheckForUpdateAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ApplySavedWindowPosition()
    {
        if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)_settings.WindowLeft, (int)_settings.WindowTop);
        }
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        // If a job is in progress and the user hasn't already confirmed,
        // intercept the close and ask them to confirm. e.Cancel must be set
        // before the first await so the framework honors it.
        if (_flashCts != null && !_forceClose)
        {
            e.Cancel = true;

            // Guard against reentry (e.g. Cmd-Q while the prompt is already up).
            if (_confirmInFlight) return;
            _confirmInFlight = true;

            try
            {
                var confirm = MessageBoxManager.GetMessageBoxStandard(
                    "Universal Dreamcast Patcher",
                    "A patch operation is currently in progress. Are you sure you want to quit?\n\n" +
                    "Closing now will abort the operation and may leave partial output files on disk.",
                    ButtonEnum.YesNo, MsBoxIcon.None);

                var result = await confirm.ShowWindowDialogAsync(this);

                if (result == ButtonResult.Yes)
                {
                    _forceClose = true;
                    Close();
                }
            }
            finally
            {
                _confirmInFlight = false;
            }
            return;
        }

        // Reload before saving so a setting changed by a dialog this session
        // (e.g. SkippedUpdateVersion) isn't overwritten.
        var fresh = AppSettings.Load();
        fresh.WindowLeft = Position.X;
        fresh.WindowTop = Position.Y;
        fresh.Save();
    }

    private void LoadLogos()
    {
        using (var s = AssetLoader.Open(new Uri("avares://UniversalDreamcastPatcher/Assets/udp_logo.png")))
            _logoOff = new Bitmap(s);

        using (var s = AssetLoader.Open(new Uri("avares://UniversalDreamcastPatcher/Assets/udp_logo_led.png")))
            _logoOn = new Bitmap(s);
    }

    // LED flash sequence: 250, 250, 150, 150, 150 ms, then stays on for the rest of the job.
    public void StartBusyAnimation()
    {
        StopBusyAnimation();
        SetInactiveTabEnabled(false);
        if (_versionLine != null) _versionLine.IsHitTestVisible = false;

        var cts = new CancellationTokenSource();
        _flashCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                int[] delays = { 250, 250, 150, 150, 150 };
                bool showOn = true;

                foreach (var ms in delays)
                {
                    await Task.Delay(ms, token).ConfigureAwait(false);
                    var next = showOn ? _logoOn : _logoOff;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_headerLogo != null) _headerLogo.Source = next;
                    });
                    showOn = !showOn;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_headerLogo != null) _headerLogo.Source = _logoOn;
                });
            }
            catch (TaskCanceledException) { }
        });
    }

    public void StopBusyAnimation()
    {
        _flashCts?.Cancel();
        _flashCts?.Dispose();
        _flashCts = null;

        if (_headerLogo != null) _headerLogo.Source = _logoOff;

        SetInactiveTabEnabled(true);
        if (_versionLine != null) _versionLine.IsHitTestVisible = true;
    }

    // While one tab is running a job, disable the other tab so the user
    // can't switch and start a second one. The active tab stays enabled.
    private void SetInactiveTabEnabled(bool enabled)
    {
        if (_mainTabs == null || _applyTab == null || _buildTab == null) return;

        if (_mainTabs.SelectedIndex == 0)
            _buildTab.IsEnabled = enabled;
        else
            _applyTab.IsEnabled = enabled;
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            // This was awaited from the UI thread, so the continuation runs on it.
            // No Dispatcher hop needed before showing dialogs.
            var result = await UpdateManager.CheckForUpdateAsync();

            if (result.ManualUpdateRequired && !UpdateAvailableDialog.ShouldSkipVersion(result.LatestTag))
            {
                var manualDialog = new ManualUpdateDialog(result.LatestTag, result.LatestVersion, result.ManualReason);
                await manualDialog.ShowDialog(this);
            }
            else if (result.UpdateAvailable && !UpdateAvailableDialog.ShouldSkipVersion(result.LatestTag))
            {
                var dialog = new UpdateAvailableDialog(result.LatestTag, result.LatestVersion);
                await dialog.ShowDialog(this);

                if (dialog.UserWantsUpdate)
                {
                    var wizard = new UpdateWizardWindow(result.LatestTag, result.LatestVersion);
                    await wizard.ShowDialog(this);
                }
            }
        }
        catch
        {
            // Don't surface update-check failures at startup.
        }
    }

    private async void VersionLine_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var about = new AboutWindow();
        await about.ShowDialog(this);
    }
}
