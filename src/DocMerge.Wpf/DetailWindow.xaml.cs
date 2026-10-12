using System.Windows;
using System.Windows.Input;

namespace DocMerge.Wpf;

/// <summary>Displays temporary read-only review details.</summary>
public partial class DetailWindow : Window
{
    /// <summary>Initializes a detail dialog.</summary>
    public DetailWindow(string title, string body)
    {
        InitializeComponent();
        Title = title;
        DataContext = new { DialogTitle = title, DialogBody = body };
    }

    private void DetailWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        MaxWidth = Math.Max(MinWidth, workArea.Width - 48);
        MaxHeight = Math.Max(MinHeight, workArea.Height - 48);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
        CloseButton.Focus();
    }

    private void DetailWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Close();
        e.Handled = true;
    }
}
