using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class BuildPatchView : UserControl
{
    private const int MaxGameNameLength = 128;

    private TextBox? _originalGdiPath;
    private TextBox? _modifiedGdiPath;
    private TextBox? _outputDcpPath;
    private TextBox? _customGameName;
    private Button? _browseOriginal;
    private Button? _browseModified;
    private Button? _browseOutput;
    private Button? _buildButton;
    private CheckBox? _useCustomIpBin;
    private RadioButton? _ipBinFromModified;
    private RadioButton? _ipBinFromOriginal;
    private CheckBox? _applyIpBinPatches;
    private CheckBox? _ipBinRegionFree;
    private CheckBox? _ipBinVga;
    private CheckBox? _useCustomGameName;
    private StackPanel? _busyPanel;
    private TextBlock? _progressLabel;

    private bool _isFiltering;
    private CancellationTokenSource? _cts;

    public BuildPatchView()
    {
        InitializeComponent();

        _originalGdiPath = this.FindControl<TextBox>("OriginalGdiPath");
        _modifiedGdiPath = this.FindControl<TextBox>("ModifiedGdiPath");
        _outputDcpPath = this.FindControl<TextBox>("OutputDcpPath");
        _customGameName = this.FindControl<TextBox>("CustomGameName");
        _browseOriginal = this.FindControl<Button>("BrowseOriginalGdi");
        _browseModified = this.FindControl<Button>("BrowseModifiedGdi");
        _browseOutput = this.FindControl<Button>("BrowseBuildOutputFile");
        _buildButton = this.FindControl<Button>("BuildPatchButton");
        _useCustomIpBin = this.FindControl<CheckBox>("UseCustomIpBin");
        _ipBinFromModified = this.FindControl<RadioButton>("IpBinFromModified");
        _ipBinFromOriginal = this.FindControl<RadioButton>("IpBinFromOriginal");
        _applyIpBinPatches = this.FindControl<CheckBox>("ApplyIpBinPatches");
        _ipBinRegionFree = this.FindControl<CheckBox>("IpBinRegionFree");
        _ipBinVga = this.FindControl<CheckBox>("IpBinVga");
        _useCustomGameName = this.FindControl<CheckBox>("UseCustomGameName");
        _busyPanel = this.FindControl<StackPanel>("BuildBusyPanel");
        _progressLabel = this.FindControl<TextBlock>("BuildProgressLabel");

        if (_customGameName != null)
            _customGameName.TextChanged += (_, _) => FilterAsciiUpper(_customGameName, MaxGameNameLength);

        if (_browseOriginal != null) _browseOriginal.Click += async (_, _) => await BrowseGdi(_originalGdiPath, "Select original GDI");
        if (_browseModified != null) _browseModified.Click += async (_, _) => await BrowseGdi(_modifiedGdiPath, "Select modified GDI");
        if (_browseOutput != null) _browseOutput.Click += async (_, _) => await BrowseOutputDcp();
        if (_buildButton != null) _buildButton.Click += async (_, _) => await BuildPatch();

        if (_originalGdiPath != null) _originalGdiPath.TextChanged += (_, _) => UpdateGate();
        if (_modifiedGdiPath != null) _modifiedGdiPath.TextChanged += (_, _) => UpdateGate();
        if (_outputDcpPath != null) _outputDcpPath.TextChanged += (_, _) => UpdateGate();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateGate()
    {
        if (_buildButton == null) return;
        bool ok = !string.IsNullOrWhiteSpace(_originalGdiPath?.Text)
               && !string.IsNullOrWhiteSpace(_modifiedGdiPath?.Text)
               && !string.IsNullOrWhiteSpace(_outputDcpPath?.Text);
        _buildButton.IsEnabled = ok;
    }

    private async Task BrowseGdi(TextBox? target, string title)
    {
        if (target == null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Dreamcast disc images") { Patterns = new[] { "*.gdi", "*.cue", "*.chd" } },
                new("GDI files")   { Patterns = new[] { "*.gdi" } },
                new("CUE/BIN (Redump GD-ROM)") { Patterns = new[] { "*.cue" } },
                new("CHD files")   { Patterns = new[] { "*.chd" } },
                new("All files")   { Patterns = new[] { "*.*" } },
            },
        });
        if (files.Count > 0) target.Text = files[0].Path.LocalPath;
    }

    private async Task BrowseOutputDcp()
    {
        if (_outputDcpPath == null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var picked = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save patch as...",
            DefaultExtension = "dcp",
            ShowOverwritePrompt = true,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("DCP patch") { Patterns = new[] { "*.dcp" } },
            },
        });

        if (picked == null) return;
        _outputDcpPath.Text = EnsureSingleDcpExtension(picked.Path.LocalPath);
    }

    // Normalize to exactly one ".dcp" suffix.
    // File pickers auto-append on some platforms but not others.
    private static string EnsureSingleDcpExtension(string path)
    {
        while (path.EndsWith(".dcp", StringComparison.OrdinalIgnoreCase))
            path = path[..^4];
        return path + ".dcp";
    }

    private async Task BuildPatch()
    {
        // Custom IP.BIN from the original disc, with no patches, is a no-op.
        bool includeCustomIpBin = _useCustomIpBin?.IsChecked == true;
        var ipBinFrom = _ipBinFromOriginal?.IsChecked == true ? IpBinSource.OriginalGdi : IpBinSource.ModifiedGdi;
        bool applyPatches = _applyIpBinPatches?.IsChecked == true;
        bool wantRegionFree = applyPatches && _ipBinRegionFree?.IsChecked == true;
        bool wantVga = applyPatches && _ipBinVga?.IsChecked == true;
        bool wantCustomName = _useCustomGameName?.IsChecked == true;

        if (includeCustomIpBin
            && ipBinFrom == IpBinSource.OriginalGdi
            && !wantRegionFree && !wantVga && !wantCustomName)
        {
            includeCustomIpBin = false;
        }

        var options = new PatchBuildOptions
        {
            OriginalGdiPath = _originalGdiPath?.Text ?? string.Empty,
            ModifiedGdiPath = _modifiedGdiPath?.Text ?? string.Empty,
            OutputDcpPath = _outputDcpPath?.Text ?? string.Empty,
            IncludeCustomIpBin = includeCustomIpBin,
            IpBinFrom = ipBinFrom,
            IpBinRegionFree = wantRegionFree,
            IpBinVga = wantVga,
            UseCustomGameName = wantCustomName,
            CustomGameName = _customGameName?.Text ?? string.Empty,
        };

        if (_buildButton != null) _buildButton.IsVisible = false;
        if (_busyPanel != null) _busyPanel.IsVisible = true;
        SetInputsEnabled(false);

        (TopLevel.GetTopLevel(this) as MainWindow)?.StartBusyAnimation();

        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(msg =>
        {
            if (_progressLabel != null) _progressLabel.Text = msg;
        });

        PatchBuildResult result;
        try
        {
            result = await PatchBuilder.BuildAsync(options, progress, _cts.Token);
        }
        catch (Exception ex)
        {
            result = new PatchBuildResult { Success = false, ErrorMessage = ex.Message };
        }

        if (_buildButton != null) _buildButton.IsVisible = true;
        if (_busyPanel != null) _busyPanel.IsVisible = false;
        if (_progressLabel != null) _progressLabel.Text = string.Empty;
        SetInputsEnabled(true);

        (TopLevel.GetTopLevel(this) as MainWindow)?.StopBusyAnimation();

        _cts.Dispose();
        _cts = null;

        var owner = TopLevel.GetTopLevel(this) as Window;

        string message;
        if (result.Success)
        {
            var sizeKB = new FileInfo(result.ProducedDcpPath).Length / 1024;
            message = $"Patch built successfully.\n\n" +
                      $"Files patched: {result.FilesDiffed}\n" +
                      $"Files added (new): {result.FilesAddedVerbatim}\n" +
                      $"DCP size: {sizeKB:N0} KB\n\n" +
                      $"Output:\n{result.ProducedDcpPath}";
        }
        else
        {
            message = result.ErrorMessage ?? "Building the patch failed for an unknown reason.";
        }

        var box = MessageBoxManager.GetMessageBoxStandard(
            result.Success ? "Information" : "Error",
            message, ButtonEnum.Ok, Icon.None);
        if (owner != null)
            await box.ShowWindowDialogAsync(owner);
        else
            await box.ShowAsync();

        ResetInputs();
    }

    private void ResetInputs()
    {
        if (_originalGdiPath != null) _originalGdiPath.Text = string.Empty;
        if (_modifiedGdiPath != null) _modifiedGdiPath.Text = string.Empty;
        if (_outputDcpPath != null) _outputDcpPath.Text = string.Empty;

        // Reset the IP.BIN options. Child controls grey out automatically via
        // IsEnabled bindings on the parent checkboxes.
        if (_useCustomIpBin != null) _useCustomIpBin.IsChecked = false;
        if (_ipBinFromModified != null) _ipBinFromModified.IsChecked = true;
        if (_ipBinFromOriginal != null) _ipBinFromOriginal.IsChecked = false;
        if (_applyIpBinPatches != null) _applyIpBinPatches.IsChecked = false;
        if (_ipBinRegionFree != null) _ipBinRegionFree.IsChecked = false;
        if (_ipBinVga != null) _ipBinVga.IsChecked = false;
        if (_useCustomGameName != null) _useCustomGameName.IsChecked = false;
        if (_customGameName != null) _customGameName.Text = string.Empty;
    }

    private void SetInputsEnabled(bool enabled)
    {
        if (_browseOriginal != null) _browseOriginal.IsEnabled = enabled;
        if (_browseModified != null) _browseModified.IsEnabled = enabled;
        if (_browseOutput != null) _browseOutput.IsEnabled = enabled;
    }

    private void FilterAsciiUpper(TextBox box, int maxLength)
    {
        if (_isFiltering) return;
        var current = box.Text ?? string.Empty;

        var filtered = new StringBuilder(current.Length);
        foreach (var ch in current)
        {
            if (ch >= 0x20 && ch <= 0x7E)
                filtered.Append(char.ToUpperInvariant(ch));
        }

        if (filtered.Length > maxLength)
            filtered.Length = maxLength;

        var result = filtered.ToString();
        if (result == current) return;

        _isFiltering = true;
        var caret = box.CaretIndex;
        box.Text = result;
        box.CaretIndex = System.Math.Min(caret, result.Length);
        _isFiltering = false;
    }
}
