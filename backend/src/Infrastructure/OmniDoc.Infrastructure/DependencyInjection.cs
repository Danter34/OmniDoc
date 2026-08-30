using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Jobs;
using OmniDoc.Infrastructure.Services;
using OmniDoc.Infrastructure.Services.Ai;

namespace OmniDoc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IPdfParserService, PdfPigParserService>();
        services.AddSingleton<ITextChunkerService, RecursiveTextChunkerService>();
        services.AddScoped<IRetrievalService, VectorRetrievalService>();
        services.AddScoped<IDocumentProcessingJob, DocumentProcessingJob>();

        var aiSettingsSection = configuration.GetSection(AiSettings.SectionName);
        services.Configure<AiSettings>(aiSettingsSection);
        services.AddHttpClient("GeminiClient", client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        var provider = aiSettingsSection[nameof(AiSettings.Provider)] ?? "Mock";

        if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
            services.AddScoped<IChatCompletionService, GeminiChatCompletionService>();
        }
        else
        {
            services.AddScoped<IEmbeddingService, MockEmbeddingService>();
            services.AddScoped<IChatCompletionService, MockChatCompletionService>();
        }

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
