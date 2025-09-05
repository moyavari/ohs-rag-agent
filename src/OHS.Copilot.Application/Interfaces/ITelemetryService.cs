using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OHS.Copilot.Application.Interfaces;

public interface ITelemetryService
{
    ActivitySource GetActivitySource(string name);
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
    Activity? StartAgentActivity(string agentName, string operation);
    Activity? StartLlmActivity(string model, string provider);
    Activity? StartVectorStoreActivity(string operation, string storeName);
    
    void RecordRequestMetrics(string method, string endpoint, int statusCode, TimeSpan duration, long requestSize, long responseSize);
    void RecordAgentMetrics(string agentName, TimeSpan duration, bool success);
    void RecordVectorStoreMetrics(string operation, string storeName, int count, TimeSpan duration);
    void RecordLlmMetrics(string model, string provider, int promptTokens, int completionTokens, TimeSpan duration);
    void RecordDocumentProcessingMetrics(string documentType, int chunkCount, TimeSpan duration, bool success);
    void RecordMemoryMetrics(string memoryType, string operation, TimeSpan duration, long sizeBytes);
    void RecordContentModerationMetrics(string[] categories, int severity);
    void RecordContentRedactionMetrics(int redactionCount, TimeSpan duration);
    
    void IncrementCounter(string name, int value = 1, params KeyValuePair<string, object?>[] tags);
    void RecordValue(string name, double value, params KeyValuePair<string, object?>[] tags);
}

public static class ActivityExtensions
{
    public static Activity? SetAgentAttributes(this Activity? activity, string agentName, string operation)
    {
        return activity?
            .SetTag("agent.name", agentName)
            .SetTag("agent.operation", operation)
            .SetTag("agent.type", "rag_agent");
    }
    
    public static Activity? SetLlmAttributes(this Activity? activity, string model, string provider, int? maxTokens = null, double? temperature = null)
    {
        activity?
            .SetTag("llm.model", model)
            .SetTag("llm.provider", provider);
            
        if (maxTokens.HasValue)
            activity?.SetTag("llm.max_tokens", maxTokens.Value);
            
        if (temperature.HasValue)
            activity?.SetTag("llm.temperature", temperature.Value);
            
        return activity;
    }
    
    public static Activity? SetLlmTokenMetrics(this Activity? activity, int promptTokens, int completionTokens)
    {
        return activity?
            .SetTag("llm.tokens.prompt", promptTokens)
            .SetTag("llm.tokens.completion", completionTokens)
            .SetTag("llm.tokens.total", promptTokens + completionTokens);
    }
    
    public static Activity? SetVectorStoreAttributes(this Activity? activity, string operation, string storeName, int? count = null, int? dimensions = null)
    {
        activity?
            .SetTag("vector_store.operation", operation)
            .SetTag("vector_store.name", storeName);
            
        if (count.HasValue)
            activity?.SetTag("vector_store.count", count.Value);
            
        if (dimensions.HasValue)
            activity?.SetTag("vector_store.dimensions", dimensions.Value);
            
        return activity;
    }
    
    public static Activity? SetDocumentAttributes(this Activity? activity, string documentPath, string documentType, long? sizeBytes = null, int? pageCount = null)
    {
        activity?
            .SetTag("document.path", documentPath)
            .SetTag("document.type", documentType);
            
        if (sizeBytes.HasValue)
            activity?.SetTag("document.size_bytes", sizeBytes.Value);
            
        if (pageCount.HasValue)
            activity?.SetTag("document.page_count", pageCount.Value);
            
        return activity;
    }
    
    public static Activity? SetMemoryAttributes(this Activity? activity, string memoryType, string key, int? ttlSeconds = null)
    {
        activity?
            .SetTag("memory.type", memoryType)
            .SetTag("memory.key", key);
            
        if (ttlSeconds.HasValue)
            activity?.SetTag("memory.ttl_seconds", ttlSeconds.Value);
            
        return activity;
    }
    
    public static Activity? SetErrorAttributes(this Activity? activity, Exception ex)
    {
        return activity?
            .SetTag("error.type", ex.GetType().Name)
            .SetTag("error.message", ex.Message)
            .SetTag("error.stack_trace", ex.StackTrace)
            .SetStatus(ActivityStatusCode.Error, ex.Message);
    }
    
    public static Activity? SetConversationAttributes(this Activity? activity, string? conversationId, int? turnCount = null)
    {
        if (!string.IsNullOrEmpty(conversationId))
        {
            activity?.SetTag("conversation.id", conversationId);
            
            if (turnCount.HasValue)
                activity?.SetTag("conversation.turn_count", turnCount.Value);
        }
        
        return activity;
    }
}
