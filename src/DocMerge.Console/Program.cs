using DocMerge.Application.Documents;
using DocMerge.Application.Files;
using DocMerge.Application.Pdf;
using DocMerge.Application.Sorting;
using DocMerge.Application.Transfer;
using DocMerge.Application.Validation;
using DocMerge.Console.Cli;
using DocMerge.Console.IO;
using DocMerge.Domain.Transfer;
using DocMerge.Infrastructure.Docx;
using DocMerge.Infrastructure.Files;
using DocMerge.Infrastructure.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace DocMerge.Console;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        System.Console.InputEncoding = System.Text.Encoding.UTF8;
        await using var provider = BuildServices();
        var application = provider.GetRequiredService<ConsoleApplication>();
        return await application.RunAsync(args, CancellationToken.None);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConsoleIO, SystemConsoleIO>();
        services.AddSingleton<CliParser>();
        services.AddSingleton<PdfPageParser>();
        services.AddSingleton<IPdfNameReader, PdfPigNameReader>();
        services.AddSingleton<IDocumentReader, DocxDocumentReader>();
        services.AddSingleton<IDocumentEditor, DocxDocumentEditor>();
        services.AddSingleton<IFileStore, PhysicalFileStore>();
        services.AddSingleton<ITransferResultValidator, DocxTransferValidator>();
        services.AddSingleton<TransferPlanner>();
        services.AddSingleton<TransferService>();
        services.AddSingleton<SortDocumentService>();
        services.AddSingleton<ConsoleApplication>();
        return services.BuildServiceProvider();
    }
}
