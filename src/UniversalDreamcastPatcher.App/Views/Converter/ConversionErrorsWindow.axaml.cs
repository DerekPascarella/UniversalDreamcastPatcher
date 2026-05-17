using System.Collections.Generic;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.App.Views.Converter;

public partial class ConversionErrorsWindow : Window
{
    public ConversionErrorsWindow()
    {
        InitializeComponent();
    }

    public ConversionErrorsWindow(IReadOnlyList<(string Path, string Error)> failures) : this()
    {
        var box = this.FindControl<TextBox>("LogBox");
        var okButton = this.FindControl<Button>("OkButton");

        if (box != null) box.Text = BuildLog(failures);
        if (okButton != null) okButton.Click += (_, _) => Close();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private static string BuildLog(IReadOnlyList<(string Path, string Error)> failures)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < failures.Count; i++)
        {
            var (path, err) = failures[i];
            sb.AppendLine(path);
            sb.AppendLine(err);
            if (i < failures.Count - 1) sb.AppendLine();
        }
        return sb.ToString();
    }
}
