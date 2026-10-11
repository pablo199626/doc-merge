using System.Windows;

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
}
