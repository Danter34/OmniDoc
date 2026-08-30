using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Jobs;
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
        services.AddScoped<IRetrievalService, VectorRetrievalService>();
        services.AddScoped<IChatCompletionService, MockChatCompletionService>();
        services.AddScoped<IDocumentProcessingJob, DocumentProcessingJob>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(postgres => postgres.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options => options.WorkerCount = Environment.ProcessorCount * 2);

        return services;
    }
}
