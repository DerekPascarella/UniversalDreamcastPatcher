using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UniversalDreamcastPatcher.App.Views.Converter;
using UniversalDreamcastPatcher.App.Views.Shared;
using UniversalDreamcastPatcher.Core;
using UniversalDreamcastPatcher.Core.Patching;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views;

public partial class ConverterView : UserControl
{
    private RadioButton? _internalRadio;
    private RadioButton? _externalRadio;
    private Button? _manageButton;
    private Button? _infoButton;

    public ConverterView()
    {
        InitializeComponent();

        _internalRadio = this.FindControl<RadioButton>("DatSourceInternalRadio");
        _externalRadio = this.FindControl<RadioButton>("DatSourceExternalRadio");
        _manageButton = this.FindControl<Button>("ManageExternalDatsButton");
        _infoButton = this.FindControl<Button>("DatSourceInfoButton");

        LoadFromSettings();

        if (_internalRadio != null) _internalRadio.IsCheckedChanged += (_, _) => OnDatSourceChanged();
        if (_externalRadio != null) _externalRadio.IsCheckedChanged += (_, _) => OnDatSourceChanged();
        if (_manageButton != null) _manageButton.Click += async (_, _) => await OpenManageWindow();
        if (_infoButton != null) _infoButton.Click += async (_, _) => await DatSourceInfoDialog.ShowAsync(this);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void LoadFromSettings()
    {
        var settings = AppSettings.Load();
        bool isExternal = string.Equals(settings.DatSource, "External", System.StringComparison.OrdinalIgnoreCase);
        if (_internalRadio != null) _internalRadio.IsChecked = !isExternal;
        if (_externalRadio != null) _externalRadio.IsChecked = isExternal;
        if (_manageButton != null) _manageButton.IsEnabled = isExternal;
    }

    private void OnDatSourceChanged()
    {
        bool isExternal = _externalRadio?.IsChecked == true;
        if (_manageButton != null) _manageButton.IsEnabled = isExternal;

        var settings = AppSettings.Load();
        settings.DatSource = isExternal ? "External" : "Internal";
        settings.Save();

        ExternalDatRegistry.IsEnabled = isExternal;
    }

    private async System.Threading.Tasks.Task OpenManageWindow()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var win = new ManageExternalDatsWindow();
        if (owner != null) await win.ShowDialog(owner);
        else win.Show();
    }
}
