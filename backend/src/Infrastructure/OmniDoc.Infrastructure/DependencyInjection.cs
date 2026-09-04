using Hangfire;
using Hangfire.PostgreSql;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Jobs;
using OmniDoc.Infrastructure.Services;
using OmniDoc.Infrastructure.Services.Ai;
using OmniDoc.Infrastructure.Services.Security;
using OmniDoc.Infrastructure.Services.Email;

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
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailTemplateBuilder, OmniDocEmailTemplateBuilder>();
        services.AddSingleton<IEmailVerificationOtpService, EmailVerificationOtpService>();
        services.AddSingleton<IEmailVerificationFeatureOptions, EmailVerificationFeatureOptions>();
        services.AddSingleton<IPasswordResetTokenService, PasswordResetTokenService>();
        services.AddSingleton<IPasswordResetLinkService, PasswordResetLinkService>();
        services.AddScoped<IEmailOutboxScheduler, HangfireEmailOutboxScheduler>();
        services.AddScoped<IEmailOutboxJob, SendEmailJob>();
        services.AddScoped<IEmailOutboxDispatcher, EmailOutboxDispatcher>();

        services.AddOptions<EmailSettings>()
            .Bind(configuration.GetSection(EmailSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Host),
                "EmailSettings:Host is required.")
            .Validate(settings => settings.Port is > 0 and <= 65535,
                "EmailSettings:Port must be between 1 and 65535.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.FromEmail),
                "EmailSettings:FromEmail is required.")
            .ValidateOnStart();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                settings => Encoding.UTF8.GetByteCount(settings.Secret) >= 32,
                "JwtSettings:Secret must be at least 32 bytes.")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Issuer),
                "JwtSettings:Issuer is required.")
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.Audience),
                "JwtSettings:Audience is required.")
            .Validate(
                settings => settings.ExpiryMinutes > 0,
                "JwtSettings:ExpiryMinutes must be greater than zero.")
            .ValidateOnStart();

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
