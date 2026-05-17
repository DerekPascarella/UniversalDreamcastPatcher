using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Converter;

public sealed class ExternalDatRow : INotifyPropertyChanged
{
    public ExternalDatFile File { get; }

    public string FilePath => File.FilePath;
    public string FileName => Path.GetFileName(File.FilePath);
    public bool IsMissing => File.IsMissing;

    public string TypeLabel => File.IsMissing ? "—" : File.DetectedType;
    public string EntriesLabel => File.IsMissing ? "Missing" : File.EntryCount.ToString("N0");

    private int _index;
    public int Index
    {
        get => _index;
        set { if (_index != value) { _index = value; Raise(); } }
    }

    public ExternalDatRow(ExternalDatFile file) { File = file; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
