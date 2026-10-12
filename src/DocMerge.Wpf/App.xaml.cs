using System.Windows;
using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Generation;
using DocMerge.Application.Pdf;
using DocMerge.Domain.Generation;
using DocMerge.Infrastructure.Docx;
using DocMerge.Infrastructure.Files;
using DocMerge.Infrastructure.Pdf;
using DocMerge.Wpf.Services;
using DocMerge.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DocMerge.Wpf;

/// <summary>Provides the desktop application entry point and composition root.</summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _services = BuildServices();
        var themeService = _services.GetRequiredService<IThemeService>();
        themeService.ApplySavedTheme();
        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GroupRosterPageParser>();
        services.AddSingleton<IClassRosterReader, PdfPigClassRosterReader>();
        services.AddSingleton<IGenerationTemplateReader, DocxTemplateReader>();
        services.AddSingleton<IHistoricalStudentReader, DocxHistoricalStudentReader>();
        services.AddSingleton<IFileStore, PhysicalFileStore>();
        services.AddSingleton<IGenerationInputDiscovery, GenerationInputDiscovery>();
        services.AddSingleton<IGenerationBatchPublisher, GenerationBatchPublisher>();
        services.AddSingleton<IClassDocumentGenerator, DocxClassDocumentGenerator>();
        services.AddSingleton<IDocumentReader, DocxDocumentReader>();
        services.AddSingleton<IClassGenerationValidator, DocxClassGenerationValidator>();
        services.AddSingleton<IBundledGenerationTemplateProvider, BundledGenerationTemplateProvider>();
        services.AddSingleton<HistoricalStudentResolver>();
        services.AddSingleton<ClassGenerationPlanner>();
        services.AddSingleton<ClassGenerationService>();
        services.AddSingleton<ClassGenerationExecutionService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IDesktopPreferencesService, DesktopPreferencesService>();
        services.AddSingleton<ISystemThemeService, WindowsSystemThemeService>();
        services.AddSingleton<IThemeApplicator, WpfThemeApplicator>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISystemFolderService, SystemFolderService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
