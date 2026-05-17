using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.Core;
using UniversalDreamcastPatcher.Core.Patching;
using MsBoxIcon = MsBox.Avalonia.Enums.Icon;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Converter;

public partial class ManageExternalDatsWindow : Window
{
    private readonly ObservableCollection<ExternalDatRow> _rows = new();

    private ListBox? _datList;
    private Button? _addButton;
    private Button? _removeButton;
    private Button? _clearButton;
    private Button? _moveUpButton;
    private Button? _moveDownButton;
    private Button? _cancelButton;
    private Button? _saveButton;

    private bool _userSaved;

    public ManageExternalDatsWindow()
    {
        InitializeComponent();

        _datList = this.FindControl<ListBox>("DatList");
        _addButton = this.FindControl<Button>("AddDatButton");
        _removeButton = this.FindControl<Button>("RemoveButton");
        _clearButton = this.FindControl<Button>("ClearButton");
        _moveUpButton = this.FindControl<Button>("MoveUpButton");
        _moveDownButton = this.FindControl<Button>("MoveDownButton");
        _cancelButton = this.FindControl<Button>("CancelButton");
        _saveButton = this.FindControl<Button>("SaveButton");

        if (_datList != null)
        {
            _datList.ItemsSource = _rows;
            _datList.SelectionChanged += (_, _) => UpdateGates();
        }
        if (_addButton != null) _addButton.Click += async (_, _) => await AddDat();
        if (_removeButton != null) _removeButton.Click += (_, _) => RemoveSelected();
        if (_clearButton != null) _clearButton.Click += (_, _) => ClearAll();
        if (_moveUpButton != null) _moveUpButton.Click += (_, _) => MoveSelected(-1);
        if (_moveDownButton != null) _moveDownButton.Click += (_, _) => MoveSelected(+1);
        if (_cancelButton != null) _cancelButton.Click += (_, _) => Close();
        if (_saveButton != null) _saveButton.Click += (_, _) => { _userSaved = true; Close(); };

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        _rows.CollectionChanged += (_, _) =>
        {
            RecomputeIndexes();
            UpdateGates();
        };

        Closing += (_, _) => { if (_userSaved) PersistAndUpdateRegistry(); };

        LoadCurrentState();
        UpdateGates();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // ---- Load / persist --------------------------------------------------

    private void LoadCurrentState()
    {
        // Take the registry's current view as truth. Re-validate any path that
        // was loaded as Missing in case the user dropped the file back in.
        var current = ExternalDatRegistry.Files;
        foreach (var f in current)
        {
            ExternalDatFile resolved = f;
            if (f.IsMissing && File.Exists(f.FilePath))
            {
                var reloaded = ExternalDatFile.TryLoad(f.FilePath, out _);
                if (reloaded != null) resolved = reloaded;
            }
            _rows.Add(new ExternalDatRow(resolved));
        }
    }

    private void PersistAndUpdateRegistry()
    {
        var files = _rows.Select(r => r.File).ToList();
        ExternalDatRegistry.SetFiles(files);

        var settings = AppSettings.Load();
        settings.ExternalDatPaths = files.Select(f => f.FilePath).ToList();
        settings.Save();
    }

    private void RecomputeIndexes()
    {
        for (int i = 0; i < _rows.Count; i++) _rows[i].Index = i + 1;
    }

    // ---- Add / Remove / Clear / Move -------------------------------------

    private async Task AddDat()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add external DAT",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Logiqx DAT files") { Patterns = new[] { "*.dat" } },
                new("All files")        { Patterns = new[] { "*.*" } },
            },
        });
        if (files.Count == 0) return;

        var existingPaths = new HashSet<string>(_rows.Select(r => Path.GetFullPath(r.FilePath)), StringComparer.OrdinalIgnoreCase);

        foreach (var picked in files)
        {
            var path = Path.GetFullPath(picked.Path.LocalPath);
            if (existingPaths.Contains(path)) continue;

            var loaded = ExternalDatFile.TryLoad(path, out string? error);
            if (loaded == null)
            {
                await ShowError(
                    $"The selected DAT file could not be loaded.\n\n" +
                    $"{Path.GetFileName(path)}\n{error ?? "Unknown parser error."}");
                continue;
            }

            existingPaths.Add(path);
            _rows.Add(new ExternalDatRow(loaded));
        }
    }

    private void RemoveSelected()
    {
        if (_datList == null) return;
        var toRemove = _datList.SelectedItems?.Cast<ExternalDatRow>().ToList() ?? new();
        if (toRemove.Count == 0) return;

        int firstRemovedIdx = toRemove.Min(r => _rows.IndexOf(r));

        foreach (var row in toRemove) _rows.Remove(row);

        if (_rows.Count > 0)
            _datList.SelectedIndex = Math.Min(firstRemovedIdx, _rows.Count - 1);
    }

    private void ClearAll()
    {
        _rows.Clear();
    }

    private void MoveSelected(int delta)
    {
        if (_datList == null) return;
        var selected = _datList.SelectedItems?.Cast<ExternalDatRow>().ToList() ?? new();
        if (selected.Count != 1) return;

        var row = selected[0];
        int idx = _rows.IndexOf(row);
        int target = idx + delta;
        if (target < 0 || target >= _rows.Count) return;

        _rows.Move(idx, target);
        _datList.SelectedIndex = target;
    }

    // ---- Gating ----------------------------------------------------------

    private void UpdateGates()
    {
        int selectedCount = _datList?.SelectedItems?.Count ?? 0;

        if (_removeButton != null) _removeButton.IsEnabled = selectedCount > 0;
        if (_clearButton != null) _clearButton.IsEnabled = _rows.Count > 0;

        bool exactlyOne = selectedCount == 1;
        int singleIdx = exactlyOne ? _rows.IndexOf((ExternalDatRow)_datList!.SelectedItem!) : -1;

        if (_moveUpButton != null) _moveUpButton.IsEnabled = exactlyOne && singleIdx > 0;
        if (_moveDownButton != null) _moveDownButton.IsEnabled = exactlyOne && singleIdx >= 0 && singleIdx < _rows.Count - 1;
    }

    private async Task ShowError(string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Error", message, ButtonEnum.Ok, MsBoxIcon.None);
        await box.ShowWindowDialogAsync(this);
    }
}
