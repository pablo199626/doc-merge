using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DocMerge.Wpf.ViewModels;

namespace DocMerge.Wpf;

/// <summary>Hosts the desktop workflow and handles presentation-only window behavior.</summary>
public partial class MainWindow : Window
{
    private const double StudentDetailPanelWidth = 1000;
    private const double PanelOuterSpacing = 10;
    private const double MinimumStudentsWidth = 600;
    private const double ClassesPanelWidth = 260;
    private const double SideDetailMinimumWidth = StudentDetailPanelWidth + PanelOuterSpacing + MinimumStudentsWidth;
    private const double ThreeColumnMinimumWidth = SideDetailMinimumWidth + ClassesPanelWidth;
    private const double ClassListMinimumWidth = 1060;
    private static readonly DependencyProperty DetailExtentProperty = DependencyProperty.Register(
        "DetailExtent", typeof(double), typeof(MainWindow),
        new PropertyMetadata(0d, OnDetailExtentChanged));
    private readonly MainWindowViewModel _viewModel;
    private readonly Stopwatch _sidebarAnimationClock = new();
    private readonly ReviewRowActivationTracker _reviewRowActivation = new();
    private IInputElement? _detailFocusTarget;
    private bool _isClosePending;
    private bool _isCloseAuthorized;
    private bool _isSidebarAnimating;
    private int _studentDetailAnimationSequence;
    private double _sidebarFromWidth;
    private double _sidebarToWidth;
    private double _columnFromWidth;
    private double _columnToWidth;
    private static readonly TimeSpan SidebarAnimationDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan StudentDetailAnimationDuration = TimeSpan.FromMilliseconds(420);

    /// <summary>Initializes the main window.</summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        viewModel.IssueDetailRequested += ShowIssueDetail;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.IsBusy || _isCloseAuthorized)
        {
            _viewModel.IssueDetailRequested -= ShowIssueDetail;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
            StopSidebarAnimation();
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_isClosePending)
        {
            return;
        }

        if (!_viewModel.CanCancelOperation)
        {
            MessageBox.Show(
                this,
                "Talos está guardando o terminando una operación. La ventana se cerrará cuando finalice de forma segura.",
                "Operación en curso",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            _isClosePending = true;
            CloseAfterOperationAsync();
            return;
        }

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

        _isClosePending = true;
        _viewModel.CancelOperation();
        CloseAfterOperationAsync();
    }

    private async void CloseAfterOperationAsync()
    {
        await _viewModel.WaitForActiveOperationAsync();
        if (!IsLoaded)
        {
            return;
        }

        _isCloseAuthorized = true;
        Close();
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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        ApplySidebarLayout(useAnimation: false);
        await _viewModel.InitializeAsync();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.UpdateWindowWidth(e.NewSize.Width);
        if (IsLoaded)
        {
            ApplySidebarLayout(useAnimation: false);
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.IsStudentDetailOpen)
        {
            _viewModel.StudentDetail.CloseCommand.Execute(parameter: null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape || !_viewModel.IsSidebarOverlayVisible)
        {
            return;
        }

        _viewModel.CloseSidebarOverlayCommand.Execute(parameter: null);
        ApplySidebarLayout(useAnimation: true);
        SidebarToggleButton.Focus();
        e.Handled = true;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsStudentDetailOpen))
        {
            if (!_viewModel.IsStudentDetailOpen
                && Keyboard.FocusedElement is FrameworkElement focused
                && focused is TextBox or ComboBox)
            {
                _detailFocusTarget = focused;
            }

            Dispatcher.BeginInvoke(_viewModel.IsStudentDetailOpen
                ? OpenStudentDetailPanel
                : CloseStudentDetailPanel);
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedStudent)
            && _viewModel.SelectedStudent is not null)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                ReviewGrid.ScrollIntoView(_viewModel.SelectedStudent)));
            return;
        }

        if (e.PropertyName != nameof(MainWindowViewModel.WorkflowState))
        {
            return;
        }

        Dispatcher.BeginInvoke(FocusCurrentWorkflowState);
    }

    private void FocusCurrentWorkflowState()
    {
        switch (_viewModel.WorkflowState)
        {
            case DesktopWorkflowState.Selecting:
            case DesktopWorkflowState.AnalysisCancelled:
            case DesktopWorkflowState.AnalysisFailed:
                SelectionHeading.Focus();
                return;
            case DesktopWorkflowState.Reviewing:
                ReviewGrid.Focus();
                return;
            case DesktopWorkflowState.Configuring:
            case DesktopWorkflowState.GenerationCancelled:
            case DesktopWorkflowState.GenerationFailed:
                FindVisualChildren<TextBox>(GenerationStep)
                    .FirstOrDefault(textBox => textBox.IsVisible && textBox.IsEnabled)
                    ?.Focus();
                return;
            case DesktopWorkflowState.Completed:
                ResultHeading.Focus();
                return;
        }
    }

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleSidebarCommand.Execute(parameter: null);
        ApplySidebarLayout(useAnimation: true);
    }

    private void SidebarOverlay_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseSidebarOverlayCommand.Execute(parameter: null);
        ApplySidebarLayout(useAnimation: true);
        SidebarToggleButton.Focus();
    }

    private void ThemeMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void ShowIssueDetail(object? sender, IssueDetailRequestedEventArgs e)
    {
        var session = _viewModel.SessionSequence;
        var invoker = _detailFocusTarget ?? Keyboard.FocusedElement;
        var dialog = new IssueDetailWindow(e.Title, e.Body) { Owner = this };
        dialog.ShowDialog();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (session == _viewModel.SessionSequence)
            {
                RestoreDetailInvoker(invoker);
            }
        }));
    }

    private void ReviewTableBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var size = ReviewTableSurface.RenderSize;
        ReviewTableSurface.Clip = size.Width > 0 && size.Height > 0
            ? new RectangleGeometry(new Rect(size), radiusX: 9, radiusY: 9)
            : null;
    }

    private void ReviewGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (source is null || IsInteractiveReviewSource(source))
        {
            _reviewRowActivation.Reset();
            return;
        }

        var row = FindVisualAncestor<DataGridRow>(source);
        if (row?.Item is not StudentReviewViewModel student)
        {
            _reviewRowActivation.Reset();
            return;
        }

        _reviewRowActivation.Begin(student, e.GetPosition(ReviewGrid));
    }

    private void ReviewGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _reviewRowActivation.Update(
            e.GetPosition(ReviewGrid),
            GetSystemDragThreshold());
    }

    private void ReviewGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (source is null || IsInteractiveReviewSource(source))
        {
            _reviewRowActivation.Reset();
            return;
        }

        var row = FindVisualAncestor<DataGridRow>(source);
        if (row?.Item is not StudentReviewViewModel releasedStudent)
        {
            _reviewRowActivation.Reset();
            return;
        }

        var student = _reviewRowActivation.Complete(
            releasedStudent,
            e.GetPosition(ReviewGrid),
            GetSystemDragThreshold());
        if (student is null)
        {
            return;
        }

        ReviewGrid.SelectedItem = student;
        row.Focus();
        OpenStudentDetail(student);
        e.Handled = true;
    }

    private void ReviewGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter
            || Keyboard.FocusedElement is DependencyObject focusedElement
                && IsInteractiveReviewSource(focusedElement)
            || ReviewGrid.SelectedItem is not StudentReviewViewModel student)
        {
            return;
        }

        OpenStudentDetail(student);
        e.Handled = true;
    }

    private void OpenStudentDetail(StudentReviewViewModel student)
    {
        if (!_viewModel.OpenStudentDetailCommand.CanExecute(student))
        {
            return;
        }

        _detailFocusTarget = ReviewGrid;
        _viewModel.OpenStudentDetailCommand.Execute(student);
    }

    private void OpenStudentDetailPanel()
    {
        if (!_viewModel.IsStudentDetailOpen)
        {
            return;
        }

        StudentDetailPanel.Visibility = Visibility.Visible;
        AnimateStudentDetail(1);
        if (BackToStudentListButton.IsVisible)
        {
            BackToStudentListButton.Focus();
            return;
        }

        CloseStudentDetailButton.Focus();
    }

    private void CloseStudentDetailPanel()
    {
        if (_viewModel.IsStudentDetailOpen)
        {
            return;
        }

        AnimateStudentDetail(0);
    }

    private void AnimateStudentDetail(double target)
    {
        var sequence = ++_studentDetailAnimationSequence;
        var current = (double)GetValue(DetailExtentProperty);
        SetValue(DetailExtentProperty, current);
        BeginAnimation(DetailExtentProperty, null);
        if (!SystemParameters.ClientAreaAnimation)
        {
            SetValue(DetailExtentProperty, target);
            CompleteStudentDetailClose(sequence);
            return;
        }

        var animation = new DoubleAnimation(current, target, StudentDetailAnimationDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        animation.Completed += (_, _) =>
        {
            if (sequence != _studentDetailAnimationSequence)
            {
                return;
            }

            SetValue(DetailExtentProperty, target);
            BeginAnimation(DetailExtentProperty, null);
            CompleteStudentDetailClose(sequence);
        };
        BeginAnimation(DetailExtentProperty, animation);
    }

    private void CompleteStudentDetailClose(int sequence)
    {
        if (sequence != _studentDetailAnimationSequence || _viewModel.IsStudentDetailOpen)
        {
            return;
        }

        StudentDetailPanel.Visibility = Visibility.Collapsed;
        ApplyReviewLayout();
        RestoreStudentDetailFocus();
    }

    private static void OnDetailExtentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((MainWindow)sender).ApplyReviewLayout();

    private void ReviewWorkspace_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyReviewLayout();

    private void ApplyReviewLayout()
    {
        if (_viewModel is null || ReviewWorkspace is null)
        {
            return;
        }

        var width = ReviewWorkspace.ActualWidth;
        ReviewInputSummary.Visibility = Content is FrameworkElement { ActualHeight: < 650 }
            ? Visibility.Collapsed : Visibility.Visible;
        var extent = (double)GetValue(DetailExtentProperty);
        var hasDetail = _viewModel.IsStudentDetailOpen || extent > 0;
        var fullDetail = hasDetail && width < SideDetailMinimumWidth;
        var compactClasses = width < (hasDetail ? ThreeColumnMinimumWidth : ClassListMinimumWidth);
        var classesWidth = compactClasses
            ? (width < ClassListMinimumWidth ? 0 : ClassesPanelWidth * (1 - extent))
            : ClassesPanelWidth;
        ClassesColumn.Width = new GridLength(classesWidth);
        ClassListPanel.Visibility = classesWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
        CompactClassSelector.Visibility = classesWidth == 0 ? Visibility.Visible : Visibility.Collapsed;
        StudentsPanel.Visibility = fullDetail && extent >= 1 ? Visibility.Collapsed : Visibility.Visible;
        StudentsColumn.Width = fullDetail && extent >= 1
            ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        var detailWidth = fullDetail ? width : StudentDetailPanelWidth + PanelOuterSpacing;
        StudentDetailColumn.Width = new GridLength(detailWidth * extent);
        StudentDetailPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        ReviewSourceColumn.Visibility = width - classesWidth - detailWidth * extent >= 780
            ? Visibility.Visible : Visibility.Collapsed;
        BackToStudentListButton.Visibility = fullDetail ? Visibility.Visible : Visibility.Collapsed;
        CloseStudentDetailButton.Visibility = fullDetail ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RestoreStudentDetailFocus()
    {
        var target = _detailFocusTarget;
        _detailFocusTarget = null;
        if (target is FrameworkElement { IsVisible: true, IsEnabled: true } element)
        {
            element.Focus();
            return;
        }

        ReviewGrid.Focus();
    }

    private bool IsInteractiveReviewSource(DependencyObject source) =>
        FindVisualAncestor<ButtonBase>(source) is not null
        || FindVisualAncestor<TextBoxBase>(source) is not null
        || FindVisualAncestor<ComboBox>(source) is not null
        || FindVisualAncestor<ScrollBar>(source) is not null
        || FindVisualAncestor<Thumb>(source) is not null
        || FindVisualAncestor<DataGridColumnHeader>(source) is not null;

    private static Size GetSystemDragThreshold() => new(
        SystemParameters.MinimumHorizontalDragDistance,
        SystemParameters.MinimumVerticalDragDistance);

    private void ApplySidebarLayout(bool useAnimation)
    {
        var targetWidth = _viewModel.SidebarWidth;
        var targetColumnWidth = _viewModel.SidebarColumnWidth;
        if (!useAnimation || !SystemParameters.ClientAreaAnimation)
        {
            StopSidebarAnimation();
            SetSidebarWidths(targetWidth, targetColumnWidth);
            SetSidebarLabelOpacity(_viewModel.IsSidebarExpanded ? 1 : 0, animate: false);
            return;
        }

        StopSidebarAnimation();
        _sidebarFromWidth = SidebarBorder.ActualWidth > 0 ? SidebarBorder.ActualWidth : SidebarBorder.Width;
        _columnFromWidth = SidebarColumn.ActualWidth;
        _sidebarToWidth = targetWidth;
        _columnToWidth = targetColumnWidth;
        _sidebarAnimationClock.Restart();
        _isSidebarAnimating = true;
        CompositionTarget.Rendering += AnimateSidebarFrame;
        SetSidebarLabelOpacity(_viewModel.IsSidebarExpanded ? 1 : 0, animate: true);
    }

    private void AnimateSidebarFrame(object? sender, EventArgs e)
    {
        var progress = Math.Min(1, _sidebarAnimationClock.Elapsed.TotalMilliseconds
            / SidebarAnimationDuration.TotalMilliseconds);
        var easedProgress = 1 - Math.Pow(1 - progress, 3);
        SetSidebarWidths(
            Interpolate(_sidebarFromWidth, _sidebarToWidth, easedProgress),
            Interpolate(_columnFromWidth, _columnToWidth, easedProgress));
        if (progress < 1)
        {
            return;
        }

        StopSidebarAnimation();
        SetSidebarWidths(_sidebarToWidth, _columnToWidth);
    }

    private void StopSidebarAnimation()
    {
        if (!_isSidebarAnimating)
        {
            return;
        }

        CompositionTarget.Rendering -= AnimateSidebarFrame;
        _sidebarAnimationClock.Stop();
        _isSidebarAnimating = false;
    }

    private void SetSidebarWidths(double sidebarWidth, double columnWidth)
    {
        SidebarBorder.Width = sidebarWidth;
        SidebarColumn.Width = new GridLength(columnWidth);
    }

    private void SetSidebarLabelOpacity(double targetOpacity, bool animate)
    {
        var labelStyle = FindResource("SidebarLabel") as Style;
        foreach (var element in FindVisualChildren<FrameworkElement>(SidebarBorder)
                     .Where(element => ReferenceEquals(element.Style, labelStyle)))
        {
            var currentOpacity = element.Opacity;
            element.BeginAnimation(UIElement.OpacityProperty, animation: null);
            if (!animate)
            {
                element.Opacity = targetOpacity;
                continue;
            }

            element.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(currentOpacity, targetOpacity, SidebarAnimationDuration)
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                },
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)
            || SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        ApplySidebarLayout(useAnimation: false);
        AnimateStudentDetail(_viewModel.IsStudentDetailOpen ? 1 : 0);
    }

    private void RestoreDetailInvoker(IInputElement? invoker)
    {
        if (invoker is UIElement { IsVisible: true, IsEnabled: true } element)
        {
            element.Focus();
            return;
        }

        ReviewGrid.Focus();
    }

    private static double Interpolate(double start, double end, double progress) =>
        start + ((end - start) * progress);

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindVisualAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
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
