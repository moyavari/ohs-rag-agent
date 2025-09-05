using Microsoft.Extensions.DependencyInjection;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Infrastructure.Services;

namespace OHS.Copilot.Infrastructure.Extensions;

public static class GovernanceServiceExtensions
{
    public static IServiceCollection AddGovernanceServices(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton<IAuditService, InMemoryAuditService>();
        services.AddSingleton<IPromptVersionService, InMemoryPromptVersionService>();
        services.AddSingleton<IMemoryService, InMemoryMemoryService>();
        services.AddScoped<IContentRedactionService, ContentRedactionService>();
        
        services.AddScoped<IDemoModeService, DemoModeService>();
        services.AddScoped<IEvaluationService, EvaluationService>();

        if (!settings.DemoMode && !string.IsNullOrEmpty(settings.ContentSafety.Endpoint))
        {
            services.AddScoped<IContentModerationService, AzureContentModerationService>();
        }
        else
        {
            services.AddScoped<IContentModerationService, DemoContentModerationService>();
        }

        return services;
    }
}
