using Microsoft.Extensions.DependencyInjection;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Application.Services;
using OHS.Copilot.Infrastructure.Parsers;
using OHS.Copilot.Infrastructure.Services;

namespace OHS.Copilot.Infrastructure.Extensions;

public static class DocumentServiceExtensions
{
    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services)
    {
        services.AddScoped<IDocumentParser, PdfDocumentParser>();
        services.AddScoped<IDocumentParser, HtmlDocumentParser>();
        services.AddScoped<IDocumentParser, MarkdownDocumentParser>();
        services.AddScoped<IDocumentParser, TextDocumentParser>();

        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddScoped<DocumentIngestService>();

        return services;
    }
}
