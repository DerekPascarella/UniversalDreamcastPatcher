using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.App.Views.Shared;
using UniversalDreamcastPatcher.Core.Conversion;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Converter;

public partial class SingleConverterPanel : UserControl
{
    private TextBox? _sourceImagePath;
    private TextBox? _outputFolderPath;
    private ComboBox? _outputFormatDropdown;
    private Button? _formatInfoButton;
    private Button? _convertButton;
    private Button? _browseSource;
    private Button? _browseOutput;
    private StackPanel? _busyPanel;
    private TextBlock? _progressLabel;

    private CancellationTokenSource? _cts;

    public SingleConverterPanel()
    {
        InitializeComponent();

        _sourceImagePath = this.FindControl<TextBox>("SourceImagePath");
        _outputFolderPath = this.FindControl<TextBox>("OutputFolderPath");
        _outputFormatDropdown = this.FindControl<ComboBox>("OutputFormatDropdown");
        _formatInfoButton = this.FindControl<Button>("FormatInfoButton");
        _convertButton = this.FindControl<Button>("ConvertButton");
        _browseSource = this.FindControl<Button>("BrowseSourceImage");
        _browseOutput = this.FindControl<Button>("BrowseOutputFolder");
        _busyPanel = this.FindControl<StackPanel>("ConvertBusyPanel");
        _progressLabel = this.FindControl<TextBlock>("ConvertProgressLabel");

        if (_browseSource != null) _browseSource.Click += async (_, _) => await BrowseSource();
        if (_browseOutput != null) _browseOutput.Click += async (_, _) => await BrowseOutput();
        if (_convertButton != null) _convertButton.Click += async (_, _) => await Convert();
        if (_formatInfoButton != null) _formatInfoButton.Click += async (_, _) => await FormatInfoDialog.ShowAsync(this);

        if (_sourceImagePath != null)
        {
            _sourceImagePath.TextChanged += (_, _) => { RefreshOutputFormatDropdown(); UpdateGate(); };
        }
        if (_outputFolderPath != null) _outputFolderPath.TextChanged += (_, _) => UpdateGate();
    }

    private void RefreshOutputFormatDropdown()
    {
        if (_outputFormatDropdown == null) return;

        var path = _sourceImagePath?.Text ?? string.Empty;
        var detected = SourceFormatDetector.Detect(path);

        if (detected == DetectedSourceFormat.Unknown)
        {
            _outputFormatDropdown.ItemsSource = null;
            _outputFormatDropdown.IsEnabled = false;
            if (_formatInfoButton != null) _formatInfoButton.IsEnabled = false;
            _outputFormatDropdown.PlaceholderText = string.IsNullOrWhiteSpace(path)
                ? "Select a source disc image first"
                : "Source disc image is not a recognized format";
            return;
        }

        var items = FormatOptionCatalog.ForConverterSource(detected);
        _outputFormatDropdown.ItemsSource = items;
        _outputFormatDropdown.SelectedIndex = 0;
        _outputFormatDropdown.IsEnabled = true;
        if (_formatInfoButton != null) _formatInfoButton.IsEnabled = true;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateGate()
    {
        if (_convertButton == null) return;
        bool ok = !string.IsNullOrWhiteSpace(_sourceImagePath?.Text)
               && !string.IsNullOrWhiteSpace(_outputFolderPath?.Text)
               && _outputFormatDropdown?.SelectedItem is FormatOption;
        _convertButton.IsEnabled = ok;
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

    private async System.Threading.Tasks.Task Convert()
    {
        if (_sourceImagePath?.Text is not string src
         || _outputFolderPath?.Text is not string outDir)
            return;
        var format = (_outputFormatDropdown?.SelectedItem as FormatOption)?.Format;
        if (format == null) return;

        if (!await MissingDatPrompt.ConfirmProceedAsync(this)) return;

        if (_convertButton != null) _convertButton.IsVisible = false;
        if (_busyPanel != null) _busyPanel.IsVisible = true;
        SetInputsEnabled(false);

        (TopLevel.GetTopLevel(this) as MainWindow)?.StartBusyAnimation();

        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(msg =>
        {
            if (_progressLabel != null) _progressLabel.Text = msg;
        });

        DiscImageConvertResult result;
        try
        {
            result = await DiscImageConverter.ConvertAsync(
                new DiscImageConvertOptions
                {
                    SourceDiscImagePath = src,
                    OutputFolder = outDir,
                    TargetFormat = format.Value,
                },
                progress,
                _cts.Token);
        }
        catch (Exception ex)
        {
            result = new DiscImageConvertResult { Success = false, ErrorMessage = ex.Message };
        }

        if (_convertButton != null) _convertButton.IsVisible = true;
        if (_busyPanel != null) _busyPanel.IsVisible = false;
        if (_progressLabel != null) _progressLabel.Text = string.Empty;
        SetInputsEnabled(true);

        (TopLevel.GetTopLevel(this) as MainWindow)?.StopBusyAnimation();

        _cts.Dispose();
        _cts = null;

        var owner = TopLevel.GetTopLevel(this) as Window;

        string title = result.Success ? "Information" : "Error";
        string message = result.Success
            ? $"Conversion completed successfully.\n\n" +
              $"Output folder:\n{result.ProducedOutputFolder}"
            : result.ErrorMessage ?? "Conversion failed for an unknown reason.";

        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.Ok, Icon.None);
        if (owner != null)
            await box.ShowWindowDialogAsync(owner);
        else
            await box.ShowAsync();

        ResetInputs();
    }

    private void ResetInputs()
    {
        if (_sourceImagePath != null) _sourceImagePath.Text = string.Empty;
        if (_outputFolderPath != null) _outputFolderPath.Text = string.Empty;
    }

    private void SetInputsEnabled(bool enabled)
    {
        if (_browseSource != null) _browseSource.IsEnabled = enabled;
        if (_browseOutput != null) _browseOutput.IsEnabled = enabled;
        if (_outputFormatDropdown != null) _outputFormatDropdown.IsEnabled = enabled;
        if (_formatInfoButton != null) _formatInfoButton.IsEnabled = enabled;
    }
}
