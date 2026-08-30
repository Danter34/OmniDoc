using OmniDoc.API.Services;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.API;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddOpenApi();
        services.AddSignalR();

        // IHubContext is a singleton, so the notifier can be resolved from the Hangfire
        // job scope without capturing a shorter-lived dependency.
        services.AddSingleton<IDocumentProgressNotifier, SignalRDocumentProgressNotifier>();

        return services;
    }
}
