using System.ComponentModel;
using System.Windows;
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
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void InputCard_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: InputSelectionViewModel input }
            || e.Data.GetData(DataFormats.FileDrop) is not string[] paths
            || paths.Length != 1)
        {
            MessageBox.Show(
                this,
                "Arrastra una sola carpeta o archivo sobre la tarjeta correspondiente.",
                "Selección no válida",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _viewModel.SelectDroppedPath(input.Kind, paths[0]);
    }

    private void ShowDetail(object? sender, DetailRequestedEventArgs e)
    {
        var dialog = new DetailWindow(e.Title, e.Body) { Owner = this };
        dialog.ShowDialog();
    }
}
