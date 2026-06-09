using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MsBox.Avalonia.Enums;
using UniversalDreamcastPatcher.App.Views.Shared;
using UniversalDreamcastPatcher.Core;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class IpBinEditorView : UserControl
{
    // ---- Source / state ---------------------------------------------------

    private IpBinFileSource? _source;
    private bool _suppressFieldHandlers;      // Held while populating UI from a metadata object.
    private bool _isFiltering;                // Guard against re-entrant TextChanged during live-filter writes.
    private bool _filtersAttached;            // Tracks current attached/detached state of the live filters.

    private EventHandler<global::Avalonia.Controls.TextChangedEventArgs>? _productNumberFilterHandler;
    private EventHandler<global::Avalonia.Controls.TextChangedEventArgs>? _versionFilterHandler;
    private EventHandler<global::Avalonia.Controls.TextChangedEventArgs>? _releaseDateFilterHandler;
    private EventHandler<global::Avalonia.Controls.TextChangedEventArgs>? _bootFilenameFilterHandler;
    private EventHandler<global::Avalonia.Controls.TextChangedEventArgs>? _makerNameFilterHandler;
    private EventHandler<global::Avalonia.Controls.TextChangedEventArgs>? _gameTitleFilterHandler;

    // ---- Controls (resolved in InitializeComponent) ----------------------

    private TextBox? _sourcePath;
    private TextBlock? _detectionLabel;
    private Button? _browseSource;
    private TabControl? _editorTabs;
    private Button? _saveChangesButton;
    private StackPanel? _saveBusyPanel;
    private ProgressBar? _saveProgressBar;
    private TextBlock? _saveProgressLabel;

    private TextBox? _productNumberInput;
    private TextBox? _versionInput;
    private TextBox? _releaseDateInput;
    private TextBox? _makerNameInput;
    private TextBox? _gameTitleInput;
    private TextBox? _bootFilenameInput;

    private RadioButton? _mediaGdRom;
    private RadioButton? _mediaCdRom;
    private NumericUpDown? _discNumberInput;
    private NumericUpDown? _discCountInput;

    private CheckBox? _regionJ;
    private CheckBox? _regionU;
    private CheckBox? _regionE;
    private TextBlock? _regionWarning;

    // Peripherals (full set, in canonical-table order).
    private CheckBox? _periphWindowsCe;
    private CheckBox? _periphVgaBox;
    private CheckBox? _periphOther;
    private CheckBox? _periphPuruPuru;
    private CheckBox? _periphMike;
    private CheckBox? _periphMemoryCard;
    private CheckBox? _periphStandardPad;
    private CheckBox? _periphCButton;
    private CheckBox? _periphDButton;
    private CheckBox? _periphXButton;
    private CheckBox? _periphYButton;
    private CheckBox? _periphZButton;
    private CheckBox? _periphExpandedDpad;
    private CheckBox? _periphTriggerL;     // Analog L
    private CheckBox? _periphTriggerR;     // Analog R
    private CheckBox? _periphAnalogH;
    private CheckBox? _periphAnalogV;
    private CheckBox? _periphAnalogXH;
    private CheckBox? _periphAnalogXV;
    private CheckBox? _periphLightGun;
    private CheckBox? _periphKeyboard;
    private CheckBox? _periphMouse;

    public IpBinEditorView()
    {
        InitializeComponent();

        _sourcePath = this.FindControl<TextBox>("SourcePath");
        _detectionLabel = this.FindControl<TextBlock>("DetectionLabel");
        _browseSource = this.FindControl<Button>("BrowseSource");
        _editorTabs = this.FindControl<TabControl>("EditorSubTabs");
        _saveChangesButton = this.FindControl<Button>("SaveChangesButton");
        _saveBusyPanel = this.FindControl<StackPanel>("SaveBusyPanel");
        _saveProgressBar = this.FindControl<ProgressBar>("SaveProgressBar");
        _saveProgressLabel = this.FindControl<TextBlock>("SaveProgressLabel");

        _productNumberInput = this.FindControl<TextBox>("ProductNumberInput");
        _versionInput = this.FindControl<TextBox>("VersionInput");
        _releaseDateInput = this.FindControl<TextBox>("ReleaseDateInput");
        _makerNameInput = this.FindControl<TextBox>("MakerNameInput");
        _gameTitleInput = this.FindControl<TextBox>("GameTitleInput");
        _bootFilenameInput = this.FindControl<TextBox>("BootFilenameInput");

        _mediaGdRom = this.FindControl<RadioButton>("MediaGdRom");
        _mediaCdRom = this.FindControl<RadioButton>("MediaCdRom");
        _discNumberInput = this.FindControl<NumericUpDown>("DiscNumberInput");
        _discCountInput = this.FindControl<NumericUpDown>("DiscCountInput");

        _regionJ = this.FindControl<CheckBox>("RegionJ");
        _regionU = this.FindControl<CheckBox>("RegionU");
        _regionE = this.FindControl<CheckBox>("RegionE");
        _regionWarning = this.FindControl<TextBlock>("RegionWarning");

        _periphWindowsCe = this.FindControl<CheckBox>("PeriphWindowsCe");
        _periphVgaBox = this.FindControl<CheckBox>("PeriphVgaBox");
        _periphOther = this.FindControl<CheckBox>("PeriphOther");
        _periphPuruPuru = this.FindControl<CheckBox>("PeriphPuruPuru");
        _periphMike = this.FindControl<CheckBox>("PeriphMike");
        _periphMemoryCard = this.FindControl<CheckBox>("PeriphMemoryCard");
        _periphStandardPad = this.FindControl<CheckBox>("PeriphStandardPad");
        _periphCButton = this.FindControl<CheckBox>("PeriphCButton");
        _periphDButton = this.FindControl<CheckBox>("PeriphDButton");
        _periphXButton = this.FindControl<CheckBox>("PeriphXButton");
        _periphYButton = this.FindControl<CheckBox>("PeriphYButton");
        _periphZButton = this.FindControl<CheckBox>("PeriphZButton");
        _periphExpandedDpad = this.FindControl<CheckBox>("PeriphExpandedDpad");
        _periphTriggerL = this.FindControl<CheckBox>("PeriphTriggerL");
        _periphTriggerR = this.FindControl<CheckBox>("PeriphTriggerR");
        _periphAnalogH = this.FindControl<CheckBox>("PeriphAnalogH");
        _periphAnalogV = this.FindControl<CheckBox>("PeriphAnalogV");
        _periphAnalogXH = this.FindControl<CheckBox>("PeriphAnalogXH");
        _periphAnalogXV = this.FindControl<CheckBox>("PeriphAnalogXV");
        _periphLightGun = this.FindControl<CheckBox>("PeriphLightGun");
        _periphKeyboard = this.FindControl<CheckBox>("PeriphKeyboard");
        _periphMouse = this.FindControl<CheckBox>("PeriphMouse");

        if (_browseSource != null) _browseSource.Click += async (_, _) => await BrowseSourceClicked();
        if (_saveChangesButton != null) _saveChangesButton.Click += async (_, _) => await SaveChangesClicked();

        WireFieldChangeHandlers();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // ---- Browse / Load ----------------------------------------------------

    private async Task BrowseSourceClicked()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select source disc image or IP.BIN",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Dreamcast disc images or raw IP.BIN")
                {
                    Patterns = new[] { "*.gdi", "*.cue", "*.cdi", "*.chd", "*.bin" }
                },
                new("GDI files")        { Patterns = new[] { "*.gdi" } },
                new("CUE/BIN (Redump)") { Patterns = new[] { "*.cue" } },
                new("CDI files")        { Patterns = new[] { "*.cdi" } },
                new("CHD files")        { Patterns = new[] { "*.chd" } },
                new("Raw IP.BIN")       { Patterns = new[] { "*.bin" } },
                new("All files")        { Patterns = new[] { "*.*" } },
            },
        });

        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        if (_sourcePath != null) _sourcePath.Text = path;

        await LoadSourceAsync(path);
    }

    private async Task LoadSourceAsync(string path)
    {
        // Dispose first so a CHD source's temp dir is gone before the
        // next OpenAsync runs.
        _source?.Dispose();
        _source = null;

        SetDetectionLabel("Loading source...");
        SetEditorEnabled(false);

        // Lock Browse during load. CHD extraction takes seconds and a
        // second click would race the first.
        if (_browseSource != null) _browseSource.IsEnabled = false;

        // Progress<string> captures the UI thread's SyncContext, so callbacks
        // from the background Task.Run land back on the UI thread.
        var loadProgress = new Progress<string>(SetDetectionLabel);

        try
        {
            try
            {
                // Off-thread so the UI keeps painting and the
                // "Loading source..." label renders for every format.
                _source = await Task.Run(() => IpBinFileSource.OpenAsync(path, loadProgress));
            }
            catch (Exception ex)
            {
                SetDetectionLabel("Failed to open source.");
                await ShowDialog("Error", $"Could not open the source file.\n\n{ex.Message}");
                return;
            }

            if (!_source.HasIpBin)
            {
                SetDetectionLabel($"Detected: {FormatLabel(_source.Format)} - no IP.BIN found");
                await ShowDialog("Error",
                    "The selected file does not contain a recognizable IP.BIN. " +
                    "It may not be a Dreamcast disc image.");
                _source.Dispose();
                _source = null;
                return;
            }

            IpBinMetadata metadata;
            try
            {
                metadata = _source.LoadFirst();
            }
            catch (Exception ex)
            {
                SetDetectionLabel("Failed to parse IP.BIN.");
                await ShowDialog("Error",
                    $"The first IP.BIN copy in the source could not be read.\n\n{ex.Message}");
                _source.Dispose();
                _source = null;
                return;
            }

            // If the source has no regions checked, fall back to region-
            // free so the loaded state at least boots. The load dialog
            // reports the auto-fix so the user can change it.
            bool regionAutoFixed = !metadata.RegionJapan && !metadata.RegionUsa && !metadata.RegionEurope;
            if (regionAutoFixed)
            {
                metadata.RegionJapan = true;
                metadata.RegionUsa = true;
                metadata.RegionEurope = true;
            }

            PopulateFromMetadata(metadata);
            SetDetectionLabel(BuildDetectionLabel(_source, metadata));
            SetEditorEnabled(true);
            if (_editorTabs != null) _editorTabs.SelectedIndex = 0;

            UpdateValidationSurface();

            var blocking = GatherMetadataFromUi().Validate()
                .Where(i => i.Severity == ValidationSeverity.Block).ToList();

            if (blocking.Count > 0 || regionAutoFixed)
            {
                var sb = new StringBuilder();
                if (regionAutoFixed)
                {
                    sb.AppendLine("The source had no region flags set, so the editor has set all three regions (region-free). Adjust the region checkboxes if you'd prefer a different setting.");
                }
                if (blocking.Count > 0)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine("The following fields don't match specification and are highlighted in red.");
                    sb.AppendLine();
                    foreach (var iss in blocking)
                        sb.AppendLine($"- {FieldDisplayName(iss.Field)}: {iss.Reason}");
                }
                await ShowDialog("Information", sb.ToString().TrimEnd());
            }
        }
        finally
        {
            if (_browseSource != null) _browseSource.IsEnabled = true;
        }
    }

    private static string FormatLabel(IpBinFileFormat fmt) => fmt switch
    {
        IpBinFileFormat.RawIpBin => "Raw IP.BIN",
        IpBinFileFormat.Gdi => "GDI",
        IpBinFileFormat.CueBin => "CUE/BIN",
        IpBinFileFormat.Cdi => "CDI",
        IpBinFileFormat.ChdGdRom => "CHD",
        _ => fmt.ToString(),
    };

    private static string BuildDetectionLabel(IpBinFileSource source, IpBinMetadata metadata)
    {
        var fmt = FormatLabel(source.Format);
        var media = metadata.MediaType == IpBinMediaType.CdRom ? "CD-ROM" : "GD-ROM";
        var copies = source.CopyCount == 1 ? "1 IP.BIN copy" : $"{source.CopyCount} IP.BIN copies";
        return $"Detected: {fmt} - {media} - {copies}";
    }

    private void SetDetectionLabel(string text)
    {
        if (_detectionLabel != null) _detectionLabel.Text = text;
    }

    private void SetEditorEnabled(bool enabled)
    {
        if (_editorTabs != null) _editorTabs.IsEnabled = enabled;
        // Save is gated separately by UpdateValidationSurface().
        if (_saveChangesButton != null) _saveChangesButton.IsEnabled = enabled;
    }

    // ---- Field <-> Metadata mapping --------------------------------------

    private void PopulateFromMetadata(IpBinMetadata m)
    {
        _suppressFieldHandlers = true;
        DetachLiveFilters();
        try
        {
            // Uppercase on load. Non-ASCII bytes stay until the user
            // edits the field, at which point the live filter strips them.
            if (_productNumberInput != null) _productNumberInput.Text = ToUpper(m.ProductNumber);
            if (_versionInput != null) _versionInput.Text = ToUpper(m.Version);
            if (_releaseDateInput != null) _releaseDateInput.Text = m.ReleaseDate ?? string.Empty;
            if (_makerNameInput != null) _makerNameInput.Text = ToUpper(m.MakerName);
            if (_gameTitleInput != null) _gameTitleInput.Text = ToUpper(m.SoftwareTitle);
            if (_bootFilenameInput != null) _bootFilenameInput.Text = ToUpper(m.BootFilename);

            if (_mediaGdRom != null) _mediaGdRom.IsChecked = m.MediaType == IpBinMediaType.GdRom;
            if (_mediaCdRom != null) _mediaCdRom.IsChecked = m.MediaType == IpBinMediaType.CdRom;

            if (_discNumberInput != null) _discNumberInput.Value = Clamp9(m.DiscNumber);
            if (_discCountInput != null) _discCountInput.Value = Clamp9(m.DiscCount);

            if (_regionJ != null) _regionJ.IsChecked = m.RegionJapan;
            if (_regionU != null) _regionU.IsChecked = m.RegionUsa;
            if (_regionE != null) _regionE.IsChecked = m.RegionEurope;

            SetCheck(_periphWindowsCe, m.Peripherals, IpBinPeripherals.WindowsCe);
            SetCheck(_periphVgaBox, m.Peripherals, IpBinPeripherals.VgaBox);
            SetCheck(_periphOther, m.Peripherals, IpBinPeripherals.OtherExpansions);
            SetCheck(_periphPuruPuru, m.Peripherals, IpBinPeripherals.PuruPuru);
            SetCheck(_periphMike, m.Peripherals, IpBinPeripherals.Microphone);
            SetCheck(_periphMemoryCard, m.Peripherals, IpBinPeripherals.MemoryCard);
            SetCheck(_periphStandardPad, m.Peripherals, IpBinPeripherals.StandardPad);
            SetCheck(_periphCButton, m.Peripherals, IpBinPeripherals.CButton);
            SetCheck(_periphDButton, m.Peripherals, IpBinPeripherals.DButton);
            SetCheck(_periphXButton, m.Peripherals, IpBinPeripherals.XButton);
            SetCheck(_periphYButton, m.Peripherals, IpBinPeripherals.YButton);
            SetCheck(_periphZButton, m.Peripherals, IpBinPeripherals.ZButton);
            SetCheck(_periphExpandedDpad, m.Peripherals, IpBinPeripherals.ExpandedDpad);
            SetCheck(_periphTriggerR, m.Peripherals, IpBinPeripherals.AnalogTriggerR);
            SetCheck(_periphTriggerL, m.Peripherals, IpBinPeripherals.AnalogTriggerL);
            SetCheck(_periphAnalogH, m.Peripherals, IpBinPeripherals.AnalogStickHorizontal);
            SetCheck(_periphAnalogV, m.Peripherals, IpBinPeripherals.AnalogStickVertical);
            SetCheck(_periphAnalogXH, m.Peripherals, IpBinPeripherals.ExpandedAnalogHorizontal);
            SetCheck(_periphAnalogXV, m.Peripherals, IpBinPeripherals.ExpandedAnalogVertical);
            SetCheck(_periphLightGun, m.Peripherals, IpBinPeripherals.LightGun);
            SetCheck(_periphKeyboard, m.Peripherals, IpBinPeripherals.Keyboard);
            SetCheck(_periphMouse, m.Peripherals, IpBinPeripherals.Mouse);
        }
        finally
        {
            // Defer to background priority so Avalonia's queued
            // TextChanged events fire while suppress is still set.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AttachLiveFilters();
                _suppressFieldHandlers = false;
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private IpBinMetadata GatherMetadataFromUi()
    {
        var m = new IpBinMetadata
        {
            ProductNumber = _productNumberInput?.Text ?? string.Empty,
            Version = _versionInput?.Text ?? string.Empty,
            ReleaseDate = _releaseDateInput?.Text ?? string.Empty,
            MakerName = _makerNameInput?.Text ?? string.Empty,
            SoftwareTitle = _gameTitleInput?.Text ?? string.Empty,
            BootFilename = _bootFilenameInput?.Text ?? string.Empty,
            MediaType = (_mediaCdRom?.IsChecked ?? false) ? IpBinMediaType.CdRom : IpBinMediaType.GdRom,
            DiscNumber = (int)(_discNumberInput?.Value ?? 1),
            DiscCount = (int)(_discCountInput?.Value ?? 1),
            RegionJapan = _regionJ?.IsChecked ?? false,
            RegionUsa = _regionU?.IsChecked ?? false,
            RegionEurope = _regionE?.IsChecked ?? false,
        };

        IpBinPeripherals bits = IpBinPeripherals.None;
        bits |= GetBit(_periphWindowsCe, IpBinPeripherals.WindowsCe);
        bits |= GetBit(_periphVgaBox, IpBinPeripherals.VgaBox);
        bits |= GetBit(_periphOther, IpBinPeripherals.OtherExpansions);
        bits |= GetBit(_periphPuruPuru, IpBinPeripherals.PuruPuru);
        bits |= GetBit(_periphMike, IpBinPeripherals.Microphone);
        bits |= GetBit(_periphMemoryCard, IpBinPeripherals.MemoryCard);
        bits |= GetBit(_periphStandardPad, IpBinPeripherals.StandardPad);
        bits |= GetBit(_periphCButton, IpBinPeripherals.CButton);
        bits |= GetBit(_periphDButton, IpBinPeripherals.DButton);
        bits |= GetBit(_periphXButton, IpBinPeripherals.XButton);
        bits |= GetBit(_periphYButton, IpBinPeripherals.YButton);
        bits |= GetBit(_periphZButton, IpBinPeripherals.ZButton);
        bits |= GetBit(_periphExpandedDpad, IpBinPeripherals.ExpandedDpad);
        bits |= GetBit(_periphTriggerR, IpBinPeripherals.AnalogTriggerR);
        bits |= GetBit(_periphTriggerL, IpBinPeripherals.AnalogTriggerL);
        bits |= GetBit(_periphAnalogH, IpBinPeripherals.AnalogStickHorizontal);
        bits |= GetBit(_periphAnalogV, IpBinPeripherals.AnalogStickVertical);
        bits |= GetBit(_periphAnalogXH, IpBinPeripherals.ExpandedAnalogHorizontal);
        bits |= GetBit(_periphAnalogXV, IpBinPeripherals.ExpandedAnalogVertical);
        bits |= GetBit(_periphLightGun, IpBinPeripherals.LightGun);
        bits |= GetBit(_periphKeyboard, IpBinPeripherals.Keyboard);
        bits |= GetBit(_periphMouse, IpBinPeripherals.Mouse);
        m.Peripherals = bits;
        return m;
    }

    private static void SetCheck(CheckBox? c, IpBinPeripherals bits, IpBinPeripherals flag)
    {
        if (c == null) return;
        c.IsChecked = (bits & flag) != 0;
    }

    private static IpBinPeripherals GetBit(CheckBox? c, IpBinPeripherals flag)
        => (c?.IsChecked ?? false) ? flag : IpBinPeripherals.None;

    private static int Clamp9(int n) => n < 1 ? 1 : n > 9 ? 9 : n;

    // ---- Change handlers + validation surface ----------------------------

    private void WireFieldChangeHandlers()
    {
        void OnChanged(object? _, EventArgs __)
        {
            if (_suppressFieldHandlers) return;
            UpdateValidationSurface();
        }

        // Stored as fields so PopulateFromMetadata can detach + reattach
        // them around the initial Text writes.
        _productNumberFilterHandler = (_, _) => LiveFilter(_productNumberInput!, 10, IsProductNumberChar, autoUpper: true);
        _versionFilterHandler = (_, _) => LiveFilter(_versionInput!, 6, IsVersionChar, autoUpper: true);
        _releaseDateFilterHandler = (_, _) => LiveFilter(_releaseDateInput!, 8, IsDigit, autoUpper: false);
        _bootFilenameFilterHandler = (_, _) => LiveFilter(_bootFilenameInput!, 16, IsPrintableAscii, autoUpper: true);
        _makerNameFilterHandler = (_, _) => LiveFilter(_makerNameInput!, 16, IsPrintableAscii, autoUpper: true);
        _gameTitleFilterHandler = (_, _) => LiveFilter(_gameTitleInput!, 128, IsPrintableAscii, autoUpper: true);
        AttachLiveFilters();

        foreach (var tb in new[] { _productNumberInput, _versionInput, _releaseDateInput,
                                    _makerNameInput, _gameTitleInput, _bootFilenameInput })
        {
            if (tb != null) tb.TextChanged += OnChanged;
        }
        if (_discNumberInput != null) _discNumberInput.ValueChanged += OnChanged;
        if (_discCountInput != null) _discCountInput.ValueChanged += OnChanged;
        if (_mediaGdRom != null) _mediaGdRom.IsCheckedChanged += OnChanged;
        if (_mediaCdRom != null) _mediaCdRom.IsCheckedChanged += OnChanged;

        foreach (var cb in new[] { _regionJ, _regionU, _regionE,
            _periphWindowsCe, _periphVgaBox, _periphOther, _periphPuruPuru, _periphMike,
            _periphMemoryCard, _periphStandardPad, _periphCButton, _periphDButton,
            _periphXButton, _periphYButton, _periphZButton, _periphExpandedDpad,
            _periphTriggerL, _periphTriggerR, _periphAnalogH, _periphAnalogV,
            _periphAnalogXH, _periphAnalogXV, _periphLightGun, _periphKeyboard, _periphMouse })
        {
            if (cb != null) cb.IsCheckedChanged += OnChanged;
        }
    }

    private void AttachLiveFilters()
    {
        if (_filtersAttached) return;
        if (_productNumberInput != null && _productNumberFilterHandler != null) _productNumberInput.TextChanged += _productNumberFilterHandler;
        if (_versionInput != null && _versionFilterHandler != null) _versionInput.TextChanged += _versionFilterHandler;
        if (_releaseDateInput != null && _releaseDateFilterHandler != null) _releaseDateInput.TextChanged += _releaseDateFilterHandler;
        if (_bootFilenameInput != null && _bootFilenameFilterHandler != null) _bootFilenameInput.TextChanged += _bootFilenameFilterHandler;
        if (_makerNameInput != null && _makerNameFilterHandler != null) _makerNameInput.TextChanged += _makerNameFilterHandler;
        if (_gameTitleInput != null && _gameTitleFilterHandler != null) _gameTitleInput.TextChanged += _gameTitleFilterHandler;
        _filtersAttached = true;
    }

    private void DetachLiveFilters()
    {
        if (!_filtersAttached) return;
        if (_productNumberInput != null && _productNumberFilterHandler != null) _productNumberInput.TextChanged -= _productNumberFilterHandler;
        if (_versionInput != null && _versionFilterHandler != null) _versionInput.TextChanged -= _versionFilterHandler;
        if (_releaseDateInput != null && _releaseDateFilterHandler != null) _releaseDateInput.TextChanged -= _releaseDateFilterHandler;
        if (_bootFilenameInput != null && _bootFilenameFilterHandler != null) _bootFilenameInput.TextChanged -= _bootFilenameFilterHandler;
        if (_makerNameInput != null && _makerNameFilterHandler != null) _makerNameInput.TextChanged -= _makerNameFilterHandler;
        if (_gameTitleInput != null && _gameTitleFilterHandler != null) _gameTitleInput.TextChanged -= _gameTitleFilterHandler;
        _filtersAttached = false;
    }

    private void LiveFilter(TextBox box, int maxLength, Func<char, bool> isAllowed, bool autoUpper)
    {
        if (_suppressFieldHandlers || _isFiltering) return;
        var current = box.Text ?? string.Empty;

        var filtered = new StringBuilder(current.Length);
        foreach (var ch in current)
        {
            var c = autoUpper ? char.ToUpperInvariant(ch) : ch;
            if (isAllowed(c)) filtered.Append(c);
        }
        if (filtered.Length > maxLength) filtered.Length = maxLength;

        var result = filtered.ToString();
        if (result == current) return;

        _isFiltering = true;
        var caret = box.CaretIndex;
        box.Text = result;
        box.CaretIndex = Math.Min(caret, result.Length);
        _isFiltering = false;
    }

    private static bool IsProductNumberChar(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == ' ';

    private static bool IsVersionChar(char c) =>
        c == 'V' || (c >= '0' && c <= '9') || c == '.';

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static bool IsPrintableAscii(char c) => c >= 0x20 && c <= 0x7E;

    private static string ToUpper(string? s) => (s ?? string.Empty).ToUpperInvariant();

    private void UpdateValidationSurface()
    {
        if (_source == null)
        {
            ClearAllInvalidHighlights();
            return;
        }

        var meta = GatherMetadataFromUi();
        var issues = meta.Validate();
        var blockingFields = new HashSet<IpBinField>(
            issues.Where(i => i.Severity == ValidationSeverity.Block).Select(i => i.Field));

        SetInvalid(_productNumberInput, blockingFields.Contains(IpBinField.ProductNumber));
        SetInvalid(_versionInput, blockingFields.Contains(IpBinField.Version));
        SetInvalid(_releaseDateInput, blockingFields.Contains(IpBinField.ReleaseDate));
        SetInvalid(_makerNameInput, blockingFields.Contains(IpBinField.MakerName));
        SetInvalid(_gameTitleInput, blockingFields.Contains(IpBinField.SoftwareTitle));
        SetInvalid(_bootFilenameInput, blockingFields.Contains(IpBinField.BootFilename));
        SetInvalid(_discNumberInput, blockingFields.Contains(IpBinField.DiscNumber));
        SetInvalid(_discCountInput, blockingFields.Contains(IpBinField.DiscCount));

        if (_regionWarning != null)
            _regionWarning.IsVisible = issues.Any(i => i.Field == IpBinField.Region);

        if (_saveChangesButton != null)
            _saveChangesButton.IsEnabled = blockingFields.Count == 0;
    }

    private static void SetInvalid(StyledElement? element, bool isInvalid)
    {
        if (element == null) return;
        if (isInvalid)
        {
            if (!element.Classes.Contains("invalid")) element.Classes.Add("invalid");
        }
        else
        {
            element.Classes.Remove("invalid");
        }
    }

    private void ClearAllInvalidHighlights()
    {
        foreach (var ctl in new StyledElement?[] {
            _productNumberInput, _versionInput, _releaseDateInput,
            _makerNameInput, _gameTitleInput, _bootFilenameInput,
            _discNumberInput, _discCountInput })
        {
            SetInvalid(ctl, false);
        }
        if (_regionWarning != null) _regionWarning.IsVisible = false;
    }

    private static string FieldDisplayName(IpBinField field) => field switch
    {
        IpBinField.ProductNumber => "Product number",
        IpBinField.Version => "Version",
        IpBinField.ReleaseDate => "Release date",
        IpBinField.MakerName => "Maker name",
        IpBinField.SoftwareTitle => "Game title",
        IpBinField.BootFilename => "Boot filename",
        IpBinField.DiscNumber => "Disc number",
        IpBinField.DiscCount => "Disc count",
        IpBinField.MediaType => "Media",
        IpBinField.Region => "Region",
        IpBinField.Peripherals => "Peripherals",
        _ => field.ToString(),
    };

    // ---- Reset all -------------------------------------------------------

    // ---- Save changes ----------------------------------------------------

    private async Task SaveChangesClicked()
    {
        if (_source == null) return;
        var meta = GatherMetadataFromUi();
        // Save shouldn't be clickable with blocking issues outstanding.
        // Bail anyway in case the gating got out of sync.
        if (meta.Validate().Any(i => i.Severity == ValidationSeverity.Block)) return;

        var warnings = meta.Validate().Where(i => i.Severity == ValidationSeverity.Warn).ToList();
        if (warnings.Count > 0)
        {
            var lines = string.Join("\n", warnings.Select(w => $"- {FieldDisplayName(w.Field)}: {w.Reason}"));
            ButtonResult result = await DialogBox.ShowAsync(
                this,
                "Confirmation",
                "The following fields don't match the Dreamcast spec.\n\n" +
                "They'll still save and the disc will likely boot, but the bytes won't be exactly what a retail-format disc would have.\n\n" +
                lines +
                "\n\nSave anyway?",
                ButtonEnum.OkCancel);
            if (result != ButtonResult.Ok) return;
        }

        ShowBusy(true, _source.Format == IpBinFileFormat.ChdGdRom
            ? "Recompressing CHD..."
            : "Saving...");

        try
        {
            var progress = new Progress<int>(p =>
            {
                if (_saveProgressBar != null) _saveProgressBar.Value = p;
            });
            await _source.SaveAllAsync(meta, progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ShowBusy(false);
            await ShowDialog("Error", $"Could not write the modified IP.BIN.\n\n{ex.Message}");
            return;
        }

        ShowBusy(false);

        var copies = _source.CopyCount == 1 ? "1 IP.BIN copy" : $"{_source.CopyCount} IP.BIN copies";
        await ShowDialog("Information",
            $"Changes saved.\n\n{copies} updated in:\n{_source.SourcePath}");
    }

    private void ShowBusy(bool busy, string? label = null)
    {
        if (_saveChangesButton != null) _saveChangesButton.IsVisible = !busy;
        if (_saveBusyPanel != null) _saveBusyPanel.IsVisible = busy;
        if (_browseSource != null) _browseSource.IsEnabled = !busy;
        if (_editorTabs != null) _editorTabs.IsEnabled = !busy;
        if (_saveProgressLabel != null) _saveProgressLabel.Text = label ?? string.Empty;
        if (_saveProgressBar != null)
        {
            _saveProgressBar.IsIndeterminate = busy;
            _saveProgressBar.Value = 0;
        }

        var mainWindow = TopLevel.GetTopLevel(this) as MainWindow;
        if (busy) mainWindow?.StartBusyAnimation();
        else mainWindow?.StopBusyAnimation();
    }

    private async Task ShowDialog(string title, string message)
        => await DialogBox.ShowAsync(this, title, message);
}
