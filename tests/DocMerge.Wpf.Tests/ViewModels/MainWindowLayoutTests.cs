using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DocMerge.Wpf.Tests.ViewModels;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public async Task ReviewLayout_OpenDetail_UsesSiblingColumnAndRestoresTableSpace()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => RunLayoutVerification(completion));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task;
    }

    private static void RunLayoutVerification(TaskCompletionSource completion)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await VerifyReviewLayoutAsync();
                completion.SetResult();
            }
            catch (Exception error)
            {
                completion.SetException(error);
            }
            finally
            {
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }
        }));
        Dispatcher.Run();
    }

    private static async Task VerifyReviewLayoutAsync()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary());
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Talos;component/Resources/Styles.xaml"),
        });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Talos;component/Resources/LightTheme.xaml"),
        });
        using var fixture = new ViewModelFixture { UseMixedReview = true, TemplateSupportsHeader = true, IncludeRichMeasures = true };
        using var viewModel = fixture.CreateViewModel();
        var window = new MainWindow(viewModel) { Width = 1920, Height = 1080 };
        window.Show();
        try
        {
            await SettleLayoutAsync(window);
            fixture.SelectInputs(viewModel);
            await viewModel.AnalyzeCommand.ExecuteAsync();
            Assert.True(viewModel.VisibleStudents.Count > 0, $"{viewModel.WorkflowState}: {viewModel.GeneralError}; class={viewModel.SelectedClass?.Code}; filter={viewModel.SelectedFilter}");
            await VerifyDockedWidthAsync(window, 1920, "wide-light");
            await VerifyFilteredNavigationAndEscapeAsync(window, viewModel);
            await VerifyDockedWidthAsync(window, 1366, "medium-light");
            viewModel.OpenStudentDetailCommand.Execute(viewModel.VisibleStudents[1]);
            await SettleLayoutAsync(window);
            global::Wpf.Ui.Appearance.ApplicationThemeManager.Apply(global::Wpf.Ui.Appearance.ApplicationTheme.Dark);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Talos;component/Resources/DarkTheme.xaml"),
            });
            await SettleLayoutAsync(window);
            CaptureLayout(window, "theme-change-open");
            viewModel.StudentDetail.Close();
            await SettleLayoutAsync(window);
            await VerifyDockedWidthAsync(window, 1366, "medium-dark");
            ((Button)window.FindName("SidebarToggleButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await VerifyDockedWidthAsync(window, 1920, "wide-sidebar-dark");
            ((Button)window.FindName("SidebarToggleButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var content = (FrameworkElement)window.Content;
            foreach (var scale in new[] { 1.25, 1.5, 2.0 })
            {
                content.LayoutTransform = new ScaleTransform(scale, scale);
                await VerifyDockedWidthAsync(window, 1920, $"scaled-{scale * 100}-dark");
            }
            content.LayoutTransform = Transform.Identity;
            window.Width = 800;
            viewModel.OpenStudentDetailCommand.Execute(viewModel.VisibleStudents[0]);
            await SettleLayoutAsync(window);
            Assert.Equal(Visibility.Collapsed, ((Border)window.FindName("StudentsPanel")).Visibility);
            Assert.True(((Button)window.FindName("BackToStudentListButton")).IsVisible);
            CaptureLayout(window, "narrow-dark");
            viewModel.StudentDetail.Close();
            await SettleLayoutAsync(window);
            Assert.True(((Border)window.FindName("StudentsPanel")).IsVisible);
        }
        finally
        {
            window.Close();
            app.Shutdown();
        }
    }

    private static async Task VerifyDockedWidthAsync(MainWindow window, double width, string captureName)
    {
        var viewModel = (DocMerge.Wpf.ViewModels.MainWindowViewModel)window.DataContext;
        window.Width = width;
        window.Height = width > 1400 ? 1080 : 768;
        await SettleLayoutAsync(window);
        Assert.True(viewModel.VisibleStudents.Count > 0, $"{viewModel.WorkflowState}: {viewModel.GeneralError}; class={viewModel.SelectedClass?.Code}; filter={viewModel.SelectedFilter}");
        var workspace = (Grid)window.FindName("ReviewWorkspace");
        var students = (Border)window.FindName("StudentsPanel");
        var detail = (Border)window.FindName("StudentDetailPanel");
        var closedWidth = students.ActualWidth;
        viewModel.OpenStudentDetailCommand.Execute(viewModel.VisibleStudents[0]);
        viewModel.StudentDetail.Close();
        viewModel.OpenStudentDetailCommand.Execute(viewModel.VisibleStudents[0]);
        await Task.Delay(80);
        window.UpdateLayout();
        CaptureLayout(window, captureName + "-transition");
        await SettleLayoutAsync(window);

        Assert.Same(workspace, detail.Parent);
        Assert.Same(workspace, students.Parent);
        Assert.Equal(2, Grid.GetColumn(detail));
        Assert.Equal(1, Grid.GetColumnSpan(detail));
        if (workspace.ActualWidth < 1210)
        {
            Assert.False(students.IsVisible);
            Assert.True(((Button)window.FindName("BackToStudentListButton")).IsVisible);
            VerifyDetailScroll(window);
            CaptureLayout(window, captureName);
            await CloseVisibleDetailAsync(window, viewModel);
            Assert.True(students.IsVisible);
            return;
        }

        Assert.InRange(detail.ActualWidth, 599, 601);
        Assert.True(students.IsVisible);
        Assert.True(students.ActualWidth < closedWidth);
        var detailOrigin = detail.TranslatePoint(new Point(), workspace);
        var studentsOrigin = students.TranslatePoint(new Point(), workspace);
        Assert.True(studentsOrigin.X + students.ActualWidth <= detailOrigin.X);
        Assert.InRange(detailOrigin.X + detail.ActualWidth, workspace.ActualWidth - 6, workspace.ActualWidth - 4);
        Assert.Equal(studentsOrigin.Y, detailOrigin.Y);
        Assert.InRange(detail.ActualHeight, students.ActualHeight - 1, students.ActualHeight + 1);
        Assert.Equal(new CornerRadius(12), detail.CornerRadius);
        VerifyClassViewport(window);
        VerifyClassBadges(window);
        VerifyIssueAccess(window);
        VerifyStudentNavigation(window, "1 de 7", canNavigatePrevious: false, canNavigateNext: true);
        viewModel.OpenStudentDetailCommand.Execute(viewModel.VisibleStudents[1]);
        await SettleLayoutAsync(window);
        Assert.True(detail.IsVisible);
        VerifyStudentNavigation(window, "2 de 7", canNavigatePrevious: true, canNavigateNext: true);
        var status = (TextBlock)window.FindName("DetailStatusText");
        Assert.Equal(((SolidColorBrush)window.FindResource("ErrorBrush")).Color, ((SolidColorBrush)status.Foreground).Color);
        foreach (var rowStatus in VisualChildren<TextBlock>((DataGrid)window.FindName("ReviewGrid"))
            .Where(text => text.Name == "StatusText" && text.Text == "Conflicto"))
        {
            Assert.Equal(((SolidColorBrush)status.Foreground).Color, ((SolidColorBrush)rowStatus.Foreground).Color);
        }

        VerifyDetailScroll(window);
        CaptureLayout(window, captureName);
        foreach (var expander in VisualChildren<Expander>(detail))
        {
            expander.IsExpanded = true;
        }

        ((ScrollViewer)window.FindName("DetailBodyScroll")).ScrollToEnd();
        await SettleLayoutAsync(window);
        CaptureLayout(window, captureName + "-bottom");
        await CloseVisibleDetailAsync(window, viewModel);
        Assert.InRange(students.ActualWidth, closedWidth - 1, closedWidth + 1);
    }

    private static void VerifyClassViewport(MainWindow window)
    {
        var list = (ListBox)window.FindName("ClassesList");
        if (!list.IsVisible)
        {
            return;
        }

        var scroll = VisualChildren<ScrollViewer>(list).First();
        Assert.InRange(scroll.ScrollableWidth, 0, 0.1);
        var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.True(item.ActualWidth <= scroll.ViewportWidth);
    }

    private static void VerifyClassBadges(MainWindow window)
    {
        var list = (ListBox)window.FindName("ClassesList");
        if (!list.IsVisible)
        {
            return;
        }

        var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        var names = VisualChildren<Button>(item)
            .Select(AutomationProperties.GetName)
            .Where(name => name.Contains("alumno", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(
            ["2 alumnos encontrados", "1 alumno nuevo", "4 alumnos en conflicto"],
            names.Take(3));
    }

    private static void VerifyStudentNavigation(
        MainWindow window,
        string expectedPosition,
        bool canNavigatePrevious,
        bool canNavigateNext)
    {
        var detail = (Border)window.FindName("StudentDetailPanel");
        var viewModel = (DocMerge.Wpf.ViewModels.MainWindowViewModel)window.DataContext;
        var previous = Assert.Single(VisualChildren<Button>(detail), button =>
            ReferenceEquals(button.Command, viewModel.StudentDetail.PreviousStudentCommand));
        var next = Assert.Single(VisualChildren<Button>(detail), button =>
            ReferenceEquals(button.Command, viewModel.StudentDetail.NextStudentCommand));
        Assert.Equal(canNavigatePrevious, previous.IsEnabled);
        Assert.Equal(canNavigateNext, next.IsEnabled);
        Assert.InRange(previous.ActualWidth, 39, 41);
        Assert.InRange(next.ActualWidth, 39, 41);
        Assert.Equal(expectedPosition, ((TextBlock)window.FindName("StudentPositionText")).Text);
        Assert.Single(VisualChildren<Button>(detail), button =>
            ReferenceEquals(button.Command, viewModel.StudentDetail.CloseCommand) && button.IsVisible);
        Assert.DoesNotContain(VisualChildren<Button>(detail), button =>
            Equals(button.Content, "Anterior") || Equals(button.Content, "Siguiente"));
    }

    private static void VerifyIssueAccess(MainWindow window)
    {
        var visibleButtons = VisualChildren<Button>(window).Where(button => button.IsVisible).ToArray();
        Assert.DoesNotContain(visibleButtons, button => Equals(button.Content, "Ver todas las incidencias"));
        Assert.Contains(visibleButtons, button => Equals(button.Content, "Consultar incidencias del lote"));
    }

    private static async Task VerifyFilteredNavigationAndEscapeAsync(
        MainWindow window,
        DocMerge.Wpf.ViewModels.MainWindowViewModel viewModel)
    {
        viewModel.SelectedFilter = "Encontrado";
        viewModel.OpenStudentDetailCommand.Execute(viewModel.VisibleStudents[0]);
        await SettleLayoutAsync(window);
        Assert.Equal("1 de 2 resultados", ((TextBlock)window.FindName("StudentPositionText")).Text);
        CaptureLayout(window, "wide-filtered-light");

        var keyEvent = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(window),
            Environment.TickCount,
            Key.Escape)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        window.RaiseEvent(keyEvent);
        await SettleLayoutAsync(window);

        Assert.True(keyEvent.Handled);
        Assert.False(viewModel.IsStudentDetailOpen);
        Assert.True(((DataGrid)window.FindName("ReviewGrid")).IsKeyboardFocusWithin);
        viewModel.ClearReviewFiltersCommand.Execute(parameter: null);
    }

    private static async Task CloseVisibleDetailAsync(
        MainWindow window,
        DocMerge.Wpf.ViewModels.MainWindowViewModel viewModel)
    {
        var detail = (Border)window.FindName("StudentDetailPanel");
        var close = Assert.Single(VisualChildren<Button>(detail), button =>
            ReferenceEquals(button.Command, viewModel.StudentDetail.CloseCommand) && button.IsVisible);
        var peer = new ButtonAutomationPeer(close);
        var invoker = Assert.IsAssignableFrom<IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke));
        invoker.Invoke();
        await SettleLayoutAsync(window);

        Assert.False(viewModel.IsStudentDetailOpen);
        Assert.True(((DataGrid)window.FindName("ReviewGrid")).IsKeyboardFocusWithin);
    }

    private static void VerifyDetailScroll(MainWindow window)
    {
        var scroll = (ScrollViewer)window.FindName("DetailBodyScroll");
        var body = (StackPanel)window.FindName("DetailBodyContent");
        var detail = (Border)window.FindName("StudentDetailPanel");
        Assert.True(scroll.ActualHeight >= 80, "The fixed sections must leave a readable scrolling viewport.");
        var bar = VisualChildren<ScrollBar>(scroll).First(item => item.Orientation == Orientation.Vertical && item.IsVisible);
        var barPosition = bar.TranslatePoint(new Point(), scroll);
        var bodyPosition = body.TranslatePoint(new Point(), scroll);
        Assert.True(bodyPosition.X + body.ActualWidth + 8 <= barPosition.X);
        var scrollBottom = scroll.TranslatePoint(new Point(0, scroll.ActualHeight), detail).Y;
        Assert.InRange(scrollBottom, detail.ActualHeight - 14, detail.ActualHeight);
        Assert.True(scroll.ScrollableHeight > 0);
        scroll.ScrollToEnd();
        window.UpdateLayout();
        Assert.InRange(scroll.VerticalOffset, scroll.ScrollableHeight - 1, scroll.ScrollableHeight + 1);
        scroll.ScrollToTop();
        window.UpdateLayout();
    }

    private static IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in VisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static async Task SettleLayoutAsync(Window window)
    {
        await Task.Delay(300);
        window.UpdateLayout();
    }

    private static void CaptureLayout(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("TALOS_LAYOUT_CAPTURE_DIRECTORY");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, $"{name}.png"));
        encoder.Save(stream);
    }
}
