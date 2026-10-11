using System.Windows;
using System.Windows.Controls;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf;

public partial class MainWindow : Window
{
    private const double WideLayoutMinimumWidth = 1320;

    /// <summary>Initializes the desktop review prototype.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        UpdateLayoutMode(Width);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        UpdateLayoutMode(eventArgs.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var isWide = width >= WideLayoutMinimumWidth;
        WideLayout.Visibility = isWide ? Visibility.Visible : Visibility.Collapsed;
        CompactLayout.Visibility = isWide ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (sender is not ComboBox { SelectedItem: ComboBoxItem { Tag: string themeName } })
        {
            return;
        }

        Application.Current.ThemeMode = themeName switch
        {
            "Light" => ThemeMode.Light,
            "Dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
    }
}
