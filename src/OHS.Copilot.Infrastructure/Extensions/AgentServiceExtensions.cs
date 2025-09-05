using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Application.Services;
using OHS.Copilot.Infrastructure.Agents;
using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Infrastructure.Services;

namespace OHS.Copilot.Infrastructure.Extensions;

public static class AgentServiceExtensions
{
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton<SemanticKernelService>();
        
        services.AddSingleton<Kernel>(provider =>
        {
            var kernelService = provider.GetRequiredService<SemanticKernelService>();
            return kernelService.GetKernel();
        });

        return services;
    }

    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        services.AddScoped<IAgent, RouterAgent>();
        services.AddScoped<IAgent, RetrieverAgent>();
        services.AddScoped<IAgent, DrafterAgent>();
        services.AddScoped<IAgent, CiteCheckerAgent>();

        services.AddScoped<AgentOrchestrationService>();

        return services;
    }

    public static IServiceCollection AddAzureOpenAIEmbedding(this IServiceCollection services, AppSettings settings)
    {
        if (settings.DemoMode || settings.VectorStore.Type.ToLower() == "json")
        {
            return services;
        }

        services.AddSingleton<AzureOpenAIEmbeddingService>();
        
        services.AddSingleton<IEmbeddingService>(provider =>
        {
            var azureService = provider.GetRequiredService<AzureOpenAIEmbeddingService>();
            return azureService;
        });

        return services;
    }
}
