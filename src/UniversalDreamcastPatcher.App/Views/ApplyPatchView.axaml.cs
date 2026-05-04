using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class ApplyPatchView : UserControl
{
    private TextBox? _sourceImagePath;
    private TextBox? _patchFilePath;
    private TextBox? _outputFolderPath;
    private Button? _applyButton;
    private Button? _browseSource;
    private Button? _browsePatch;
    private Button? _browseOutput;
    private StackPanel? _busyPanel;
    private TextBlock? _progressLabel;

    private CancellationTokenSource? _cts;

    public ApplyPatchView()
    {
        InitializeComponent();

        _sourceImagePath = this.FindControl<TextBox>("SourceImagePath");
        _patchFilePath = this.FindControl<TextBox>("PatchFilePath");
        _outputFolderPath = this.FindControl<TextBox>("OutputFolderPath");
        _applyButton = this.FindControl<Button>("ApplyPatchButton");
        _browseSource = this.FindControl<Button>("BrowseSourceImage");
        _browsePatch = this.FindControl<Button>("BrowsePatchFile");
        _browseOutput = this.FindControl<Button>("BrowseOutputFolder");
        _busyPanel = this.FindControl<StackPanel>("ApplyBusyPanel");
        _progressLabel = this.FindControl<TextBlock>("ApplyProgressLabel");

        if (_browseSource != null) _browseSource.Click += async (_, _) => await BrowseSource();
        if (_browsePatch != null) _browsePatch.Click += async (_, _) => await BrowsePatch();
        if (_browseOutput != null) _browseOutput.Click += async (_, _) => await BrowseOutput();
        if (_applyButton != null) _applyButton.Click += async (_, _) => await Apply();

        if (_sourceImagePath != null) _sourceImagePath.TextChanged += (_, _) => UpdateGate();
        if (_patchFilePath != null) _patchFilePath.TextChanged += (_, _) => UpdateGate();
        if (_outputFolderPath != null) _outputFolderPath.TextChanged += (_, _) => UpdateGate();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateGate()
    {
        if (_applyButton == null) return;
        bool ok = !string.IsNullOrWhiteSpace(_sourceImagePath?.Text)
               && !string.IsNullOrWhiteSpace(_patchFilePath?.Text)
               && !string.IsNullOrWhiteSpace(_outputFolderPath?.Text);
        _applyButton.IsEnabled = ok;
    }

    private async System.Threading.Tasks.Task BrowseSource()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select source disc image",
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
        if (files.Count > 0 && _sourceImagePath != null)
            _sourceImagePath.Text = files[0].Path.LocalPath;
    }

    private async System.Threading.Tasks.Task BrowsePatch()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DCP patch file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("DCP files") { Patterns = new[] { "*.dcp" } },
                new("All files") { Patterns = new[] { "*.*" } },
            },
        });
        if (files.Count > 0 && _patchFilePath != null)
            _patchFilePath.Text = files[0].Path.LocalPath;
    }

    private async System.Threading.Tasks.Task BrowseOutput()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select output folder",
            AllowMultiple = false,
        });
        if (folders.Count > 0 && _outputFolderPath != null)
            _outputFolderPath.Text = folders[0].Path.LocalPath;
    }

    private async System.Threading.Tasks.Task Apply()
    {
        if (_sourceImagePath?.Text is not string src
         || _patchFilePath?.Text is not string dcp
         || _outputFolderPath?.Text is not string outDir)
            return;

        // Swap button for progress UI, kick the LED flash.
        if (_applyButton != null) _applyButton.IsVisible = false;
        if (_busyPanel != null) _busyPanel.IsVisible = true;
        SetInputsEnabled(false);

        (TopLevel.GetTopLevel(this) as MainWindow)?.StartBusyAnimation();

        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(msg =>
        {
            if (_progressLabel != null) _progressLabel.Text = msg;
        });

        PatchApplyResult result;
        try
        {
            result = await PatchApplier.ApplyAsync(
                new PatchApplyOptions
                {
                    SourceDiscImagePath = src,
                    DcpPatchPath = dcp,
                    OutputFolder = outDir,
                },
                progress,
                _cts.Token);
        }
        catch (Exception ex)
        {
            result = new PatchApplyResult { Success = false, ErrorMessage = ex.Message };
        }

        // Restore UI.
        if (_applyButton != null) _applyButton.IsVisible = true;
        if (_busyPanel != null) _busyPanel.IsVisible = false;
        if (_progressLabel != null) _progressLabel.Text = string.Empty;
        SetInputsEnabled(true);

        (TopLevel.GetTopLevel(this) as MainWindow)?.StopBusyAnimation();

        _cts.Dispose();
        _cts = null;

        var owner = TopLevel.GetTopLevel(this) as Window;

        string message = result.Success
            ? $"Patch applied successfully.\n\n" +
              $"Files patched: {result.FilesPatched}\n" +
              $"Files added: {result.FilesAdded}\n\n" +
              $"GDI folder:\n{result.ProducedGdiFolder}"
            : result.ErrorMessage ?? "Patching failed for an unknown reason.";

        var box = MessageBoxManager.GetMessageBoxStandard("Universal Dreamcast Patcher", message, ButtonEnum.Ok, Icon.None);
        if (owner != null)
            await box.ShowWindowDialogAsync(owner);
        else
            await box.ShowAsync();

        ResetInputs();
    }

    private void ResetInputs()
    {
        if (_sourceImagePath != null) _sourceImagePath.Text = string.Empty;
        if (_patchFilePath != null) _patchFilePath.Text = string.Empty;
        if (_outputFolderPath != null) _outputFolderPath.Text = string.Empty;
    }

    private void SetInputsEnabled(bool enabled)
    {
        if (_browseSource != null) _browseSource.IsEnabled = enabled;
        if (_browsePatch != null) _browsePatch.IsEnabled = enabled;
        if (_browseOutput != null) _browseOutput.IsEnabled = enabled;
    }
}
