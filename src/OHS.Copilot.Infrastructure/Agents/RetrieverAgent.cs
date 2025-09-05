using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using OHS.Copilot.Application.Interfaces;
using OHS.Copilot.Infrastructure.Services;
using OHS.Copilot.Domain.ValueObjects;

namespace OHS.Copilot.Infrastructure.Agents;

public class RetrieverAgent : BaseAgent
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;

    public override string Name => "Retriever";

    public RetrieverAgent(
        Kernel kernel, 
        ILogger<RetrieverAgent> logger,
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ITelemetryService? telemetryService = null) : base(kernel, logger, telemetryService)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
    }

    protected override async Task<AgentResult> ExecuteInternalAsync(AgentContext context, CancellationToken cancellationToken)
    {
        var parameters = context.GetData<Dictionary<string, object>>("parameters");
        var requestType = context.GetData<string>("request_type");
        
        if (parameters == null || requestType == null)
        {
            return AgentResult.Failed("Missing parameters or request type from Router");
        }

        var searchQuery = ExtractSearchQuery(parameters, requestType);
        var topK = ExtractTopK(parameters);
        var tokenBudget = CreateTokenBudget(parameters);

        if (string.IsNullOrEmpty(searchQuery))
        {
            return AgentResult.Failed("Could not extract search query from request");
        }

        LogAction("extract_query", new { searchQuery, topK, maxTokens = tokenBudget.MaxTokens });

        var searchResults = await PerformVectorSearchAsync(searchQuery, topK, cancellationToken);
        var contextChunks = AssembleContext(searchResults, tokenBudget);
        var citations = CreateCitations(searchResults);

        LogAction("assemble_context", new { 
            totalResults = searchResults.Count, 
            selectedChunks = contextChunks.Count,
            citationCount = citations.Count,
            tokensUsed = tokenBudget.UsedTokens 
        });

        context.SetData("context_chunks", contextChunks);
        context.SetData("citations", citations);
        context.SetData("search_results", searchResults);

        return AgentResult.Successful(new Dictionary<string, object>
        {
            ["context_chunks"] = contextChunks,
            ["citations"] = citations,
            ["search_results_count"] = searchResults.Count
        });
    }

    private string ExtractSearchQuery(Dictionary<string, object> parameters, string requestType)
    {
        return requestType switch
        {
            "ask" => GetParameterValueCaseInsensitive(parameters, "Question") ?? "",
            "draft" => GetParameterValueCaseInsensitive(parameters, "Purpose") ?? "",
            _ => ""
        };
    }

    private int ExtractTopK(Dictionary<string, object> parameters)
    {
        var topK = GetParameterValueCaseInsensitive(parameters, "TopK");
        return topK != null && int.TryParse(topK.ToString(), out var k) ? k : 10;
    }

    private TokenBudget CreateTokenBudget(Dictionary<string, object> parameters)
    {
        var maxTokens = GetParameterValueCaseInsensitive(parameters, "MaxTokens");
        var budget = maxTokens != null && int.TryParse(maxTokens.ToString(), out var tokens) ? tokens : 4096;
        Logger.LogInformation("Creating TokenBudget with {Budget} tokens (MaxTokens parameter: {MaxTokens})", budget, maxTokens);
        return new TokenBudget(budget);
    }

    private string? GetParameterValue(Dictionary<string, object> parameters, string key)
    {
        return parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private string? GetParameterValueCaseInsensitive(Dictionary<string, object> parameters, string key)
    {
        var kvp = parameters.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        return kvp.Key != null ? kvp.Value?.ToString() : null;
    }

    private async Task<List<VectorSearchResult>> PerformVectorSearchAsync(string query, int topK, CancellationToken cancellationToken)
    {
        try
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            var results = await _vectorStore.SearchAsync(queryEmbedding, topK, minScore: 0.1, cancellationToken);
            
            LogAction("vector_search", new { query = query.Substring(0, Math.Min(50, query.Length)), resultCount = results.Count });
            
            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Vector search failed for query: {Query}", query.Substring(0, Math.Min(50, query.Length)));
            return [];
        }
    }

    private List<string> AssembleContext(List<VectorSearchResult> searchResults, TokenBudget tokenBudget)
    {
        var contextChunks = new List<string>();
        var reservedTokens = 300; // Reduced from 500 to avoid exceeding budget
        
        Logger.LogInformation("Attempting to consume {ReservedTokens} reserved tokens. Current budget: {RemainingTokens}/{MaxTokens}", 
            reservedTokens, tokenBudget.RemainingTokens, tokenBudget.MaxTokens);
        
        if (!tokenBudget.CanConsume(reservedTokens))
        {
            Logger.LogWarning("Cannot reserve {ReservedTokens} tokens, only {RemainingTokens} remaining. Using all remaining tokens.", 
                reservedTokens, tokenBudget.RemainingTokens);
            reservedTokens = tokenBudget.RemainingTokens;
        }
        
        tokenBudget.Consume(reservedTokens);

        foreach (var result in searchResults)
        {
            var chunkText = $"[Source: {result.Chunk.Title} - {result.Chunk.Section}]\n{result.Chunk.Text}";
            var estimatedTokens = EstimateTokenCount(chunkText);

            if (tokenBudget.CanConsume(estimatedTokens))
            {
                tokenBudget.Consume(estimatedTokens);
                contextChunks.Add(chunkText);
            }
            else
            {
                break;
            }
        }

        return contextChunks;
    }

    private List<Citation> CreateCitations(List<VectorSearchResult> searchResults)
    {
        return searchResults.Select((result, index) => 
            Citation.Create(
                id: $"c{index + 1}",
                score: result.Score,
                title: result.Chunk.Title,
                text: result.Chunk.Text.Substring(0, Math.Min(200, result.Chunk.Text.Length)) + "...",
                url: null
            )).ToList();
    }

    private static int EstimateTokenCount(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 4 / 3;
    }
}
