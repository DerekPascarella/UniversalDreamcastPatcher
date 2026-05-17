using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public partial class BatchConverterPanel : UserControl
{
    private static readonly string[] RecognizedExtensions = { ".gdi", ".cue", ".chd" };

    private readonly ObservableCollection<BatchQueueItem> _queue = new();
    private readonly HashSet<string> _queuedPaths = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _batchCts;
    private bool _isRunning;

    private ListBox? _queueList;
    private Button? _addFilesButton;
    private Button? _addFolderButton;
    private Button? _removeButton;
    private Button? _clearButton;
    private ComboBox? _targetFormatDropdown;
    private Button? _formatInfoButton;
    private TextBox? _outputFolderPath;
    private Button? _browseOutputFolder;
    private Button? _convertAllButton;
    private Button? _cancelBatchButton;
    private StackPanel? _busyPanel;
    private TextBlock? _progressLabel;

    public BatchConverterPanel()
    {
        InitializeComponent();

        _queueList = this.FindControl<ListBox>("QueueList");
        _addFilesButton = this.FindControl<Button>("AddFilesButton");
        _addFolderButton = this.FindControl<Button>("AddFolderButton");
        _removeButton = this.FindControl<Button>("RemoveButton");
        _clearButton = this.FindControl<Button>("ClearButton");
        _targetFormatDropdown = this.FindControl<ComboBox>("TargetFormatDropdown");
        _formatInfoButton = this.FindControl<Button>("FormatInfoButton");
        _outputFolderPath = this.FindControl<TextBox>("OutputFolderPath");
        _browseOutputFolder = this.FindControl<Button>("BrowseOutputFolder");
        _convertAllButton = this.FindControl<Button>("ConvertAllButton");
        _cancelBatchButton = this.FindControl<Button>("CancelBatchButton");
        _busyPanel = this.FindControl<StackPanel>("BatchBusyPanel");
        _progressLabel = this.FindControl<TextBlock>("BatchProgressLabel");

        if (_queueList != null)
        {
            _queueList.ItemsSource = _queue;
            _queueList.SelectionChanged += (_, _) => UpdateGates();
        }

        _queue.CollectionChanged += (_, _) =>
        {
            RecomputeIndexes();
            RefreshTargetDropdown();
        };

        RefreshTargetDropdown();

        if (_targetFormatDropdown != null)
        {
            _targetFormatDropdown.SelectionChanged += (_, _) => { RecomputeTargetLabels(); UpdateGates(); };
        }

        if (_addFilesButton != null) _addFilesButton.Click += async (_, _) => await AddFiles();
        if (_addFolderButton != null) _addFolderButton.Click += async (_, _) => await AddFolder();
        if (_removeButton != null) _removeButton.Click += (_, _) => RemoveSelected();
        if (_clearButton != null) _clearButton.Click += (_, _) => ClearAll();
        if (_browseOutputFolder != null) _browseOutputFolder.Click += async (_, _) => await BrowseOutput();
        if (_convertAllButton != null) _convertAllButton.Click += async (_, _) => await RunBatch();
        if (_cancelBatchButton != null) _cancelBatchButton.Click += (_, _) => _batchCts?.Cancel();
        if (_formatInfoButton != null) _formatInfoButton.Click += async (_, _) => await FormatInfoDialog.ShowAsync(this);

        if (_outputFolderPath != null) _outputFolderPath.TextChanged += (_, _) => UpdateGates();

        RecomputeTargetLabels();
        UpdateGates();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // ---- Queue management ------------------------------------------------

    private async Task AddFiles()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add disc images",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Dreamcast disc images") { Patterns = new[] { "*.gdi", "*.cue", "*.chd" } },
                new("GDI files")   { Patterns = new[] { "*.gdi" } },
                new("CUE/BIN (Redump GD-ROM)") { Patterns = new[] { "*.cue" } },
                new("CHD files")   { Patterns = new[] { "*.chd" } },
                new("All files")   { Patterns = new[] { "*.*" } },
            },
        });
        if (files.Count == 0) return;

        var paths = files.Select(f => f.Path.LocalPath).ToList();
        var detected = await Task.Run(() => DetectAll(paths));
        foreach (var (path, fmt) in detected) EnqueueDetected(path, fmt);
        UpdateGates();
    }

    private async Task AddFolder()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Add every disc image in a folder",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;

        var root = folders[0].Path.LocalPath;
        var found = await Task.Run(() => DetectAll(ScanFolder(root)));
        foreach (var (path, fmt) in found) EnqueueDetected(path, fmt);
        UpdateGates();
    }

    private static List<string> ScanFolder(string root)
    {
        var hits = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(path);
                if (RecognizedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    hits.Add(path);
            }
        }
        catch { /* unreadable folders: skip silently */ }
        hits.Sort(StringComparer.OrdinalIgnoreCase);
        return hits;
    }

    // Runs on a background thread. CHD detection opens the file and reads the
    // header, so doing it on the UI thread freezes the window for big folders.
    private static List<(string Path, DetectedSourceFormat Detected)> DetectAll(List<string> paths)
    {
        var results = new List<(string, DetectedSourceFormat)>(paths.Count);
        foreach (var p in paths) results.Add((p, SourceFormatDetector.Detect(p)));
        return results;
    }

    private void EnqueueDetected(string path, DetectedSourceFormat detected)
    {
        var full = Path.GetFullPath(path);
        if (!_queuedPaths.Add(full)) return;

        var item = new BatchQueueItem(full, detected);
        item.RecomputeTargetAction(CurrentTargetFormat());
        _queue.Add(item);
    }

    private void RemoveSelected()
    {
        if (_queueList == null) return;
        var toRemove = _queueList.SelectedItems?.Cast<BatchQueueItem>().ToList() ?? new();
        if (toRemove.Count == 0) return;

        int firstRemovedIdx = toRemove.Min(item => _queue.IndexOf(item));

        foreach (var item in toRemove)
        {
            _queue.Remove(item);
            _queuedPaths.Remove(item.SourcePath);
        }

        if (_queue.Count > 0)
            _queueList.SelectedIndex = Math.Min(firstRemovedIdx, _queue.Count - 1);

        UpdateGates();
    }

    private void ClearAll()
    {
        _queue.Clear();
        _queuedPaths.Clear();
        UpdateGates();
    }

    private void RecomputeTargetLabels()
    {
        var target = CurrentTargetFormat();
        foreach (var item in _queue) item.RecomputeTargetAction(target);
    }

    private void RecomputeIndexes()
    {
        for (int i = 0; i < _queue.Count; i++)
            _queue[i].Index = i + 1;
    }

    private void RefreshTargetDropdown()
    {
        if (_targetFormatDropdown == null) return;

        bool anyRunnable = _queue.Any(i => i.Status != BatchItemStatus.Unrecognized);

        if (!anyRunnable)
        {
            _targetFormatDropdown.ItemsSource = null;
            _targetFormatDropdown.IsEnabled = false;
            if (_formatInfoButton != null) _formatInfoButton.IsEnabled = false;
            _targetFormatDropdown.PlaceholderText = _queue.Count == 0
                ? "Add at least one disc image first"
                : "No recognized disc images in the queue";
            return;
        }

        if (_targetFormatDropdown.ItemsSource == null)
        {
            _targetFormatDropdown.ItemsSource = FormatOptionCatalog.ForConverterBatch();
            _targetFormatDropdown.SelectedIndex = 0;
        }
        _targetFormatDropdown.IsEnabled = true;
        if (_formatInfoButton != null) _formatInfoButton.IsEnabled = true;
    }

    private OutputDiscImageFormat CurrentTargetFormat()
        => (_targetFormatDropdown?.SelectedItem as FormatOption)?.Format
            ?? OutputDiscImageFormat.Gdi;

    // ---- Output folder ---------------------------------------------------

    private async Task BrowseOutput()
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

    // ---- Gating ----------------------------------------------------------

    private void UpdateGates()
    {
        bool anyRunnable = _queue.Any(i => i.Status != BatchItemStatus.Unrecognized);
        bool outputSet = !string.IsNullOrWhiteSpace(_outputFolderPath?.Text);
        bool targetSet = _targetFormatDropdown?.SelectedItem is FormatOption;

        if (_convertAllButton != null) _convertAllButton.IsEnabled = !_isRunning && anyRunnable && outputSet && targetSet;
        if (_removeButton != null) _removeButton.IsEnabled = !_isRunning && (_queueList?.SelectedItems?.Count ?? 0) > 0;
        if (_clearButton != null) _clearButton.IsEnabled = !_isRunning && _queue.Count > 0;
    }

    private void SetEditingEnabled(bool enabled)
    {
        if (_addFilesButton != null) _addFilesButton.IsEnabled = enabled;
        if (_addFolderButton != null) _addFolderButton.IsEnabled = enabled;
        if (_targetFormatDropdown != null) _targetFormatDropdown.IsEnabled = enabled;
        if (_formatInfoButton != null) _formatInfoButton.IsEnabled = enabled;
        if (_browseOutputFolder != null) _browseOutputFolder.IsEnabled = enabled;
    }

    // ---- Run loop --------------------------------------------------------

    private async Task RunBatch()
    {
        if (_outputFolderPath?.Text is not string outDir) return;
        var target = CurrentTargetFormat();

        if (!Directory.Exists(outDir))
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Information",
                "The selected output folder does not exist.\n\n" +
                "Please choose a different folder and try again.",
                ButtonEnum.Ok, Icon.None);
            if (owner != null) await box.ShowWindowDialogAsync(owner);
            else await box.ShowAsync();
            return;
        }

        if (!await MissingDatPrompt.ConfirmProceedAsync(this)) return;

        var runnable = _queue.Where(i => i.Status != BatchItemStatus.Unrecognized).ToList();
        if (runnable.Count == 0) return;

        foreach (var item in runnable)
        {
            item.Status = BatchItemStatus.Queued;
            item.StatusDetail = null;
        }

        _isRunning = true;
        _batchCts = new CancellationTokenSource();
        BeginBusyUi();
        SetEditingEnabled(false);
        UpdateGates();

        (TopLevel.GetTopLevel(this) as MainWindow)?.StartBusyAnimation();

        int succeeded = 0, copied = 0, failed = 0, cancelled = 0;

        for (int i = 0; i < runnable.Count; i++)
        {
            var item = runnable[i];

            if (_batchCts.IsCancellationRequested)
            {
                for (int j = i; j < runnable.Count; j++)
                    runnable[j].Status = BatchItemStatus.Cancelled;
                cancelled = runnable.Count - i;
                break;
            }

            item.Status = BatchItemStatus.Running;
            UpdateProgress(item, i + 1, runnable.Count, target);

            var perFileProgress = new Progress<string>(msg =>
            {
                item.StatusDetail = msg;
            });

            DiscImageConvertResult result;
            try
            {
                result = await DiscImageConverter.ConvertAsync(
                    new DiscImageConvertOptions
                    {
                        SourceDiscImagePath = item.SourcePath,
                        OutputFolder = outDir,
                        TargetFormat = target,
                    },
                    perFileProgress,
                    _batchCts.Token);
            }
            catch (OperationCanceledException)
            {
                item.Status = BatchItemStatus.Cancelled;
                cancelled++;
                for (int j = i + 1; j < runnable.Count; j++)
                {
                    runnable[j].Status = BatchItemStatus.Cancelled;
                    cancelled++;
                }
                break;
            }
            catch (Exception ex)
            {
                result = new DiscImageConvertResult { Success = false, ErrorMessage = ex.Message };
            }

            if (!result.Success && _batchCts.IsCancellationRequested)
            {
                item.Status = BatchItemStatus.Cancelled;
                item.StatusDetail = null;
                cancelled++;
                for (int j = i + 1; j < runnable.Count; j++)
                {
                    runnable[j].Status = BatchItemStatus.Cancelled;
                    cancelled++;
                }
                break;
            }

            if (result.Success)
            {
                bool isCopy = IsSameFormat(item.DetectedFormat, target);
                item.Status = isCopy ? BatchItemStatus.Copied : BatchItemStatus.Succeeded;
                item.StatusDetail = result.ProducedOutputFolder;
                if (isCopy) copied++; else succeeded++;
            }
            else
            {
                item.Status = BatchItemStatus.Failed;
                item.StatusDetail = result.ErrorMessage;
                failed++;
            }

        }

        (TopLevel.GetTopLevel(this) as MainWindow)?.StopBusyAnimation();

        _batchCts?.Dispose();
        _batchCts = null;
        _isRunning = false;
        EndBusyUi();
        SetEditingEnabled(true);
        UpdateGates();

        await ShowSummaryDialog(outDir, succeeded, copied, failed, cancelled);

        if (failed > 0)
            await ShowErrorsWindow();
    }

    private async Task ShowErrorsWindow()
    {
        var failedItems = _queue
            .Where(i => i.Status == BatchItemStatus.Failed)
            .Select(i => (i.SourcePath, i.StatusDetail ?? "An unknown error occurred."))
            .ToList();
        if (failedItems.Count == 0) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        var win = new ConversionErrorsWindow(failedItems);
        if (owner != null) await win.ShowDialog(owner);
        else win.Show();
    }

    private void BeginBusyUi()
    {
        if (_convertAllButton != null) _convertAllButton.IsVisible = false;
        if (_busyPanel != null) _busyPanel.IsVisible = true;
        if (_progressLabel != null) _progressLabel.Text = "";
    }

    private void EndBusyUi()
    {
        if (_convertAllButton != null) _convertAllButton.IsVisible = true;
        if (_busyPanel != null) _busyPanel.IsVisible = false;
        if (_progressLabel != null) _progressLabel.Text = "";
    }

    private void UpdateProgress(BatchQueueItem item, int oneBasedIndex, int total, OutputDiscImageFormat target)
    {
        if (_progressLabel != null) _progressLabel.Text = ProgressLabelFor(item, oneBasedIndex, total, target);
    }

    private static string ProgressLabelFor(BatchQueueItem item, int oneBasedIndex, int total, OutputDiscImageFormat target)
    {
        return IsSameFormat(item.DetectedFormat, target)
            ? $"Copying disc {oneBasedIndex} of {total}"
            : $"Converting disc {oneBasedIndex} of {total} to {FormatName(target)}";
    }

    private static string FormatName(OutputDiscImageFormat f) => f switch
    {
        OutputDiscImageFormat.Gdi => "GDI",
        OutputDiscImageFormat.CueBin => "CUE/BIN",
        OutputDiscImageFormat.ChdGdRom => "CHD",
        _ => "",
    };

    private static bool IsSameFormat(DetectedSourceFormat source, OutputDiscImageFormat target) =>
        (source == DetectedSourceFormat.Gdi && target == OutputDiscImageFormat.Gdi)
        || (source == DetectedSourceFormat.CueBin && target == OutputDiscImageFormat.CueBin)
        || ((source == DetectedSourceFormat.ChdContainingGdi || source == DetectedSourceFormat.ChdContainingCueBin)
            && target == OutputDiscImageFormat.ChdGdRom);

    private async Task ShowSummaryDialog(string outDir, int succeeded, int copied, int failed, int cancelled)
    {
        var lines = new List<string> { "Batch conversion complete.", "" };
        if (succeeded > 0) lines.Add($"Converted: {succeeded}");
        if (copied > 0) lines.Add($"Copied:    {copied}");
        if (failed > 0) lines.Add($"Failed:    {failed}");
        if (cancelled > 0) lines.Add($"Cancelled: {cancelled}");

        lines.Add("");
        lines.Add("Output folder:");
        lines.Add(outDir);

        var owner = TopLevel.GetTopLevel(this) as Window;
        var box = MessageBoxManager.GetMessageBoxStandard("Information", string.Join("\n", lines), ButtonEnum.Ok, Icon.None);
        if (owner != null)
            await box.ShowWindowDialogAsync(owner);
        else
            await box.ShowAsync();
    }
}
