using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Converter;

public enum BatchItemStatus
{
    Queued,
    Running,
    Succeeded,
    Copied,
    Failed,
    Cancelled,
    Unrecognized,
}

public sealed class BatchQueueItem : INotifyPropertyChanged
{
    public string SourcePath { get; }
    public string DisplayName => Path.GetFileName(SourcePath);
    public DetectedSourceFormat DetectedFormat { get; }
    public string DetectedFormatLabel { get; }

    private int _index;
    public int Index
    {
        get => _index;
        set { if (_index != value) { _index = value; Raise(); } }
    }

    private string _targetActionLabel = "";
    public string TargetActionLabel
    {
        get => _targetActionLabel;
        private set { if (_targetActionLabel != value) { _targetActionLabel = value; Raise(); } }
    }

    private BatchItemStatus _status;
    public BatchItemStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                Raise();
                Raise(nameof(StatusLabel));
                Raise(nameof(StatusIcon));
                Raise(nameof(IsFailed));
            }
        }
    }

    public bool IsFailed => _status == BatchItemStatus.Failed;

    private string? _statusDetail;
    public string? StatusDetail
    {
        get => _statusDetail;
        set { if (_statusDetail != value) { _statusDetail = value; Raise(); } }
    }

    public string StatusLabel => _status switch
    {
        BatchItemStatus.Queued => "Queued",
        BatchItemStatus.Running => "Running",
        BatchItemStatus.Succeeded => "Done",
        BatchItemStatus.Copied => "Copied",
        BatchItemStatus.Failed => "Failed",
        BatchItemStatus.Cancelled => "Cancelled",
        BatchItemStatus.Unrecognized => "Skip",
        _ => "",
    };

    public string StatusIcon => _status switch
    {
        BatchItemStatus.Queued => "",
        BatchItemStatus.Running => "⟳",
        BatchItemStatus.Succeeded => "✓",
        BatchItemStatus.Copied => "✓",
        BatchItemStatus.Failed => "✗",
        BatchItemStatus.Cancelled => "⊘",
        BatchItemStatus.Unrecognized => "⊝",
        _ => "",
    };

    public BatchQueueItem(string sourcePath, DetectedSourceFormat detected)
    {
        SourcePath = sourcePath;
        DetectedFormat = detected;
        DetectedFormatLabel = LabelFor(detected);
        _status = detected == DetectedSourceFormat.Unknown
            ? BatchItemStatus.Unrecognized
            : BatchItemStatus.Queued;
    }

    public void RecomputeTargetAction(OutputDiscImageFormat target)
    {
        if (DetectedFormat == DetectedSourceFormat.Unknown)
        {
            TargetActionLabel = "—";
            return;
        }
        if (IsSameFormat(DetectedFormat, target))
        {
            TargetActionLabel = "copy";
            return;
        }
        TargetActionLabel = target switch
        {
            OutputDiscImageFormat.Gdi => "GDI",
            OutputDiscImageFormat.CueBin => "CUE/BIN",
            OutputDiscImageFormat.ChdGdRom => "CHD",
            _ => "",
        };
    }

    private static string LabelFor(DetectedSourceFormat f) => f switch
    {
        DetectedSourceFormat.Gdi => "GDI",
        DetectedSourceFormat.CueBin => "CUE/BIN",
        DetectedSourceFormat.ChdContainingGdi => "CHD",
        DetectedSourceFormat.ChdContainingCueBin => "CHD",
        _ => "—",
    };

    private static bool IsSameFormat(DetectedSourceFormat source, OutputDiscImageFormat target) =>
        (source == DetectedSourceFormat.Gdi && target == OutputDiscImageFormat.Gdi)
        || (source == DetectedSourceFormat.CueBin && target == OutputDiscImageFormat.CueBin)
        || ((source == DetectedSourceFormat.ChdContainingGdi || source == DetectedSourceFormat.ChdContainingCueBin)
            && target == OutputDiscImageFormat.ChdGdRom);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
