using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OHS.Copilot.Infrastructure.Configuration;

namespace OHS.Copilot.Infrastructure.Services;

public class SemanticKernelService
{
    private readonly AppSettings _settings;
    private readonly ILogger<SemanticKernelService> _logger;
    private Kernel? _kernel;

    public SemanticKernelService(AppSettings settings, ILogger<SemanticKernelService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Kernel GetKernel()
    {
        if (_kernel == null)
        {
            _kernel = CreateKernel();
        }
        return _kernel;
    }

    private Kernel CreateKernel()
    {
        var builder = Kernel.CreateBuilder();

        if (_settings.DemoMode)
        {
            _logger.LogInformation("Creating Semantic Kernel in Demo Mode");
            ConfigureDemoMode(builder);
        }
        else
        {
            _logger.LogInformation("Creating Semantic Kernel with Azure OpenAI");
            ConfigureAzureOpenAI(builder);
        }

        builder.Services.AddLogging(config => config.AddConsole());

        var kernel = builder.Build();
        _logger.LogInformation("Semantic Kernel created successfully");
        
        return kernel;
    }

    private void ConfigureDemoMode(IKernelBuilder builder)
    {
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: "demo-chat-model",
            endpoint: "https://demo.openai.azure.com/",
            apiKey: "demo-key");
    }

    private void ConfigureAzureOpenAI(IKernelBuilder builder)
    {
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: _settings.AzureOpenAI.ChatDeployment,
            endpoint: _settings.AzureOpenAI.Endpoint,
            apiKey: _settings.AzureOpenAI.ApiKey);

#pragma warning disable CS0618 // Type or member is obsolete
        builder.AddAzureOpenAITextEmbeddingGeneration(
            deploymentName: _settings.AzureOpenAI.EmbeddingDeployment,
            endpoint: _settings.AzureOpenAI.Endpoint,
            apiKey: _settings.AzureOpenAI.ApiKey);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public TokenBudget CreateTokenBudget(int maxTokens = 4096)
    {
        return new TokenBudget(maxTokens);
    }
}

public class TokenBudget
{
    private readonly int _maxTokens;
    private int _usedTokens;

    public TokenBudget(int maxTokens)
    {
        _maxTokens = maxTokens;
        _usedTokens = 0;
    }

    public int MaxTokens => _maxTokens;
    public int UsedTokens => _usedTokens;
    public int RemainingTokens => _maxTokens - _usedTokens;

    public bool CanConsume(int tokens)
    {
        return _usedTokens + tokens <= _maxTokens;
    }

    public void Consume(int tokens)
    {
        if (!CanConsume(tokens))
        {
            throw new InvalidOperationException($"Cannot consume {tokens} tokens. Only {RemainingTokens} tokens remaining.");
        }
        _usedTokens += tokens;
    }

    public void Reset()
    {
        _usedTokens = 0;
    }
}
