using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OmniDoc.Application.Common.Behaviors;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Services;
using OmniDoc.Application.Features.Chat.Services;

namespace OmniDoc.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IWorkspaceAuthorizationService, WorkspaceAuthorizationService>();
        services.AddScoped<ITokenVersionValidator, TokenVersionValidator>();
        services.AddSingleton(TimeProvider.System);

        // Stateless between calls — all scanner state lives in ProcessStreamAsync locals.
        services.AddSingleton<CitationStreamStateMachine>();

        return services;
    }
}
