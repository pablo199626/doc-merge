using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf;

/// <summary>Hosts the desktop workflow and handles presentation-only window behavior.</summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _closeAfterCancellation;

    /// <summary>Initializes the main window.</summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        viewModel.DetailRequested += ShowDetail;
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.IsBusy || _closeAfterCancellation)
        {
            _viewModel.DetailRequested -= ShowDetail;
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        var decision = MessageBox.Show(
            this,
            "Hay una operación en curso. ¿Deseas cancelarla y cerrar cuando termine de limpiarse?",
            "Operación en curso",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (decision != MessageBoxResult.Yes)
        {
            return;
        }

        _closeAfterCancellation = true;
        _viewModel.CancelOperation();
        _viewModel.PropertyChanged += CloseWhenReady;
    }

    private void CloseWhenReady(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsBusy) || _viewModel.IsBusy)
        {
            return;
        }

        _viewModel.PropertyChanged -= CloseWhenReady;
        Dispatcher.BeginInvoke(Close);
    }

    private void InputCard_DragEnter(object sender, DragEventArgs e)
    {
        var canDrop = e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 };
        e.Effects = canDrop
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        if (sender is Border border && canDrop)
        {
            border.BorderBrush = (Brush)FindResource("AccentBrush");
            border.BorderThickness = new Thickness(2);
        }

        e.Handled = true;
    }

    private void InputCard_DragLeave(object sender, DragEventArgs e)
    {
        ResetDropHighlight(sender);
        e.Handled = true;
    }

    private void InputCard_Drop(object sender, DragEventArgs e)
    {
        ResetDropHighlight(sender);
        if (sender is not FrameworkElement { DataContext: InputSelectionViewModel input })
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } paths)
        {
            _viewModel.RejectDroppedPath(
                input.Kind,
                "Arrastra una sola carpeta o archivo sobre esta tarjeta.");
            e.Handled = true;
            return;
        }

        _viewModel.SelectDroppedPath(input.Kind, paths[0]);
        e.Handled = true;
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) =>
        _viewModel.UpdateWindowWidth(e.NewSize.Width);

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_viewModel.IsSidebarOverlayVisible)
        {
            return;
        }

        _viewModel.CloseSidebarOverlayCommand.Execute(parameter: null);
        SidebarToggleButton.Focus();
        e.Handled = true;
    }

    private void SidebarOverlay_Click(object sender, RoutedEventArgs e) => SidebarToggleButton.Focus();

    private void ThemeMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void ShowDetail(object? sender, DetailRequestedEventArgs e)
    {
        var dialog = new DetailWindow(e.Title, e.Body) { Owner = this };
        dialog.ShowDialog();
    }

    private void ResetDropHighlight(object sender)
    {
        if (sender is not Border border)
        {
            return;
        }

        border.ClearValue(Border.BorderBrushProperty);
        border.ClearValue(Border.BorderThicknessProperty);
    }
}
