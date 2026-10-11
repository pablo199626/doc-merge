using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Generation;
using DocMerge.Application.Pdf;
using DocMerge.Application.Sorting;
using DocMerge.Application.Transfer;
using DocMerge.Application.Validation;
using DocMerge.Console.Cli;
using DocMerge.Console.Interactive;
using DocMerge.Console.IO;
using DocMerge.Domain.Generation;
using DocMerge.Domain.Transfer;
using DocMerge.Infrastructure.Docx;
using DocMerge.Infrastructure.Files;
using DocMerge.Infrastructure.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace DocMerge.Console;

/// <summary>Provides the process entry point and dependency composition root.</summary>
public static class Program
{
    /// <summary>Runs the command-line application.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        System.Console.InputEncoding = System.Text.Encoding.UTF8;
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        System.Console.CancelKeyPress += cancelHandler;
        await using var provider = BuildServices();
        try
        {
            var application = provider.GetRequiredService<ConsoleApplication>();
            return await application.RunAsync(args, cancellation.Token);
        }
        finally
        {
            System.Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConsoleIO, SystemConsoleIO>();
        services.AddSingleton<InteractiveWizard>();
        services.AddSingleton<CliParser>();
        services.AddSingleton<PdfPageParser>();
        services.AddSingleton<GroupRosterPageParser>();
        services.AddSingleton<IPdfNameReader, PdfPigNameReader>();
        services.AddSingleton<IClassRosterReader, PdfPigClassRosterReader>();
        services.AddSingleton<IDocumentReader, DocxDocumentReader>();
        services.AddSingleton<IGenerationTemplateReader, DocxTemplateReader>();
        services.AddSingleton<IHistoricalStudentReader, DocxHistoricalStudentReader>();
        services.AddSingleton<IDocumentSorter, DocxDocumentSorter>();
        services.AddSingleton<ITransferPlanEditor, DocxTransferEditor>();
        services.AddSingleton<IFileStore, PhysicalFileStore>();
        services.AddSingleton<IGenerationInputDiscovery, GenerationInputDiscovery>();
        services.AddSingleton<IGenerationBatchPublisher, GenerationBatchPublisher>();
        services.AddSingleton<ITransferResultValidator, DocxTransferValidator>();
        services.AddSingleton<IClassDocumentGenerator, DocxClassDocumentGenerator>();
        services.AddSingleton<IClassGenerationValidator, DocxClassGenerationValidator>();
        services.AddSingleton<TransferPlanner>();
        services.AddSingleton<HistoricalStudentResolver>();
        services.AddSingleton<ClassGenerationPlanner>();
        services.AddSingleton<TransferService>();
        services.AddSingleton<ClassGenerationService>();
        services.AddSingleton<ClassGenerationExecutionService>();
        services.AddSingleton<SortDocumentService>();
        services.AddSingleton<ConsoleApplication>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
