using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Services;

namespace OmniDoc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IPdfParserService, PdfPigParserService>();
        services.AddSingleton<ITextChunkerService, RecursiveTextChunkerService>();
        services.AddScoped<IEmbeddingService, MockEmbeddingService>();

        return services;
    }
}
