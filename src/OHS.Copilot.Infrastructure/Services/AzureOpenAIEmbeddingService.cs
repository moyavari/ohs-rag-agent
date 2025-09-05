using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Configuration;
using OHS.Copilot.Infrastructure.Extensions;

namespace OHS.Copilot.Infrastructure.Services;

public class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private readonly Kernel _kernel;
    private readonly ILogger<AzureOpenAIEmbeddingService> _logger;
    private readonly AppSettings _settings;

    public AzureOpenAIEmbeddingService(AppSettings settings, ILogger<AzureOpenAIEmbeddingService> logger, Kernel kernel)
    {
        _settings = settings;
        _logger = logger;
        _kernel = kernel;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);
            
            var embeddingGenerator = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var embedding = await embeddingGenerator.GenerateEmbeddingAsync(text, _kernel, cancellationToken);
            return embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text");
            throw;
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        
        try
        {
            var tasks = textList.Select(text => GenerateEmbeddingAsync(text, cancellationToken));
            var results = await Task.WhenAll(tasks);
            
            _logger.LogDebug("Generated {Count} embeddings", results.Length);
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate batch embeddings for {Count} texts", textList.Count);
            throw;
        }
    }

    public int GetEmbeddingDimensions() => 1536;

    public string GetModelName() => _settings.AzureOpenAI.EmbeddingDeployment;
}
