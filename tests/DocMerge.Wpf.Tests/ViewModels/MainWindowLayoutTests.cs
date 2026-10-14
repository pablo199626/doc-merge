using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
        using var fixture = new ViewModelFixture { UseMixedReview = true, TemplateSupportsHeader = true };
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
            await VerifyDockedWidthAsync(window, 1366, "medium-light");
            global::Wpf.Ui.Appearance.ApplicationThemeManager.Apply(global::Wpf.Ui.Appearance.ApplicationTheme.Dark);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Talos;component/Resources/DarkTheme.xaml"),
            });
            await VerifyDockedWidthAsync(window, 1366, "medium-dark");
            var content = (FrameworkElement)window.Content;
            content.LayoutTransform = new ScaleTransform(1.5, 1.5);
            await VerifyDockedWidthAsync(window, 1920, "scaled-150-dark");
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
        await SettleLayoutAsync(window);

        Assert.Same(workspace, detail.Parent);
        Assert.Same(workspace, students.Parent);
        Assert.Equal(2, Grid.GetColumn(detail));
        Assert.Equal(1, Grid.GetColumnSpan(detail));
        Assert.InRange(detail.ActualWidth, 439, 441);
        Assert.True(students.IsVisible);
        Assert.True(students.ActualWidth < closedWidth);
        var detailOrigin = detail.TranslatePoint(new Point(), workspace);
        var studentsOrigin = students.TranslatePoint(new Point(), workspace);
        Assert.True(studentsOrigin.X + students.ActualWidth <= detailOrigin.X);
        Assert.InRange(detailOrigin.X + detail.ActualWidth, workspace.ActualWidth - 1, workspace.ActualWidth + 1);
        Assert.InRange(detail.ActualHeight, workspace.ActualHeight - 1, workspace.ActualHeight + 1);
        viewModel.OpenStudentDetailCommand.Execute(viewModel.VisibleStudents[1]);
        await SettleLayoutAsync(window);
        Assert.True(detail.IsVisible);
        CaptureLayout(window, captureName);
        viewModel.StudentDetail.Close();
        await SettleLayoutAsync(window);
        Assert.InRange(students.ActualWidth, closedWidth - 1, closedWidth + 1);
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
